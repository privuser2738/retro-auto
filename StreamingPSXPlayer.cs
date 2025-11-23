using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net.Http;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace RetroAuto
{
    /// <summary>
    /// Streams PSX games from archive.org - downloads, extracts, and plays on demand
    /// </summary>
    public class StreamingPSXPlayer : IDisposable
    {
        // Note: Use a public archive - archives with "private":true files require authentication
        // Default archive contains a small PSX collection that's publicly accessible
        private const string DEFAULT_ARCHIVE_URL = "https://archive.org/download/tekken-3-usa.-7z";
        private const string DEFAULT_EMULATOR_PATH = @"C:\Users\rob\Games\Apps\Duckstation\duckstation-qt-x64-ReleaseLTCG.exe";
        private const string DEFAULT_GAMES_DIR = @"C:\Users\rob\Games\PS1"; // Save extracted games for future use
        private const int MAX_PREPARED_GAMES = 2;

        private readonly string archiveUrl;
        private readonly string emulatorPath;
        private readonly string gamesDirectory;
        private readonly string tempDirectory;
        private readonly string windowConfigPath;
        private readonly string? localeFilter;

        private readonly HttpClient httpClient;
        private readonly System.Net.CookieContainer cookieContainer;
        private readonly ConcurrentQueue<PreparedGame> preparedGames = new();
        private readonly HashSet<string> preparingGames = new();
        private readonly object prepareLock = new();

        private List<RemoteGame> availableGames = new();
        private List<RemoteGame> remainingGames = new();
        private Random random = new();
        private CancellationTokenSource? cts;
        private Task? preparationTask;
        private Process? currentProcess;
        private bool isDisposed;
        private bool sessionInitialized = false;

        // Playlist state for persistence
        private PlaylistState? playlistState;
        private readonly bool forceReset;
        private readonly bool resetProgressOnly;

        public StreamingPSXPlayer(string? archiveUrl = null, string? emulatorPath = null, string? gamesDirectory = null, string? locale = null, bool forceReset = false, bool resetProgressOnly = false)
        {
            this.archiveUrl = archiveUrl ?? DEFAULT_ARCHIVE_URL;
            this.emulatorPath = emulatorPath ?? DEFAULT_EMULATOR_PATH;
            this.gamesDirectory = gamesDirectory ?? DEFAULT_GAMES_DIR;
            this.tempDirectory = Path.Combine(this.gamesDirectory, ".streaming_temp");
            this.windowConfigPath = Path.Combine(this.gamesDirectory, "duckstation_window.json");
            this.localeFilter = locale;
            this.forceReset = forceReset;
            this.resetProgressOnly = resetProgressOnly;

            Directory.CreateDirectory(this.gamesDirectory);
            Directory.CreateDirectory(this.tempDirectory);

            // Use CookieContainer to maintain session cookies across requests
            cookieContainer = new System.Net.CookieContainer();
            var handler = new HttpClientHandler
            {
                AllowAutoRedirect = false, // Handle redirects manually to preserve headers
                AutomaticDecompression = System.Net.DecompressionMethods.All,
                CookieContainer = cookieContainer,
                UseCookies = true,
                MaxConnectionsPerServer = 4
            };

            httpClient = new HttpClient(handler) { Timeout = TimeSpan.FromHours(4) };
            httpClient.DefaultRequestHeaders.UserAgent.ParseAdd(
                "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0.0.0 Safari/537.36");
            httpClient.DefaultRequestHeaders.Add("Accept-Language", "en-US,en;q=0.9");
        }

        public async Task RunAsync()
        {
            try { Console.Clear(); } catch { }
            Console.ForegroundColor = ConsoleColor.Blue;
            Console.WriteLine("╔════════════════════════════════════════════════════════════════╗");
            Console.WriteLine("║           PSX Streaming Player - Archive.org Edition           ║");
            Console.WriteLine("╚════════════════════════════════════════════════════════════════╝");
            Console.ResetColor();
            Console.WriteLine();
            Console.WriteLine($"Archive: {archiveUrl}");
            Console.WriteLine();

            // Validate emulator
            if (!File.Exists(emulatorPath))
            {
                Console.WriteLine($"ERROR: DuckStation not found at: {emulatorPath}");
                return;
            }

            Console.WriteLine("Fetching game list from archive.org...");
            await LoadGameList();

            if (availableGames.Count == 0)
            {
                Console.WriteLine("No games found!");
                return;
            }

            Console.WriteLine($"Found {availableGames.Count} PSX games available for streaming\n");

            // Initialize playlist state for persistence
            playlistState = new PlaylistState(gamesDirectory, "stream_psx_progress.json");
            var gameFileNames = availableGames.Select(g => g.FileName).ToList();
            bool hadSavedState = playlistState.HasSavedState;
            playlistState.Initialize(gameFileNames, forceReset, resetProgressOnly);

            if (forceReset)
            {
                Console.WriteLine("Playlist reset with new random order");
            }
            else if (resetProgressOnly)
            {
                Console.WriteLine("Progress reset - starting from beginning (same order)");
            }
            else if (hadSavedState && playlistState.GamesPlayed > 0)
            {
                Console.WriteLine($"Resuming: {playlistState.GamesPlayed}/{playlistState.TotalGames} games played");
            }

            cts = new CancellationTokenSource();
            Console.CancelKeyPress += (s, e) =>
            {
                e.Cancel = true;
                Console.WriteLine("\n\nShutting down...");
                cts.Cancel();
            };

            // Build remaining games list from playlist state order
            var gamesByFileName = availableGames.ToDictionary(g => g.FileName, StringComparer.OrdinalIgnoreCase);
            remainingGames = playlistState.GetRemainingGames()
                .Where(fn => gamesByFileName.ContainsKey(fn))
                .Select(fn => gamesByFileName[fn])
                .ToList();

            // Start background preparation
            preparationTask = PrepareGamesInBackground(cts.Token);

            int gamesPlayed = playlistState.GamesPlayed;

            try
            {
                while (!cts.Token.IsCancellationRequested)
                {
                    // Wait for a game to be ready
                    Console.WriteLine("\nWaiting for next game to be ready...");

                    PreparedGame? game = null;
                    while (game == null && !cts.Token.IsCancellationRequested)
                    {
                        if (preparedGames.TryDequeue(out game))
                            break;

                        await Task.Delay(500, cts.Token);
                        Console.Write(".");
                    }

                    if (game == null || cts.Token.IsCancellationRequested)
                        break;

                    Console.WriteLine();
                    gamesPlayed++;

                    // Show game info
                    ShowGameTitle(game, gamesPlayed);

                    // Launch the game
                    await PlayGame(game, cts.Token);

                    // Clean up played game (optional - keep for replay)
                    // Directory.Delete(game.ExtractedPath, true);
                }
            }
            catch (OperationCanceledException)
            {
                Console.WriteLine("\nPlayback cancelled.");
            }
            finally
            {
                Console.WriteLine($"\nSession ended. Games played: {gamesPlayed}");
                CleanupTempFiles();
            }
        }

        private async Task InitializeSession()
        {
            if (sessionInitialized) return;

            try
            {
                Console.WriteLine("Initializing archive.org session...");

                // Visit the main archive page first to get session cookies
                var response = await SendRequestWithRedirects(archiveUrl, isDownload: false);
                response?.Dispose();

                sessionInitialized = true;
                Console.WriteLine("Session initialized.");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Warning: Could not initialize session: {ex.Message}");
            }
        }

        private async Task<HttpResponseMessage?> SendRequestWithRedirects(
            string url,
            bool isDownload,
            long rangeStart = 0,
            CancellationToken ct = default,
            int maxRedirects = 10)
        {
            string currentUrl = url;
            string? previousHost = null;

            for (int redirect = 0; redirect < maxRedirects; redirect++)
            {
                var request = new HttpRequestMessage(HttpMethod.Get, currentUrl);
                var uri = new Uri(currentUrl);

                // Set appropriate headers
                if (isDownload)
                {
                    request.Headers.Add("Accept", "*/*");
                    request.Headers.Add("Accept-Encoding", "identity"); // Don't compress for downloads
                }
                else
                {
                    request.Headers.Add("Accept", "text/html,application/xhtml+xml,application/xml;q=0.9,image/avif,image/webp,*/*;q=0.8");
                }

                // Set referer based on previous URL or archive URL
                if (previousHost != null)
                {
                    request.Headers.Add("Referer", $"https://{previousHost}/");
                }
                else
                {
                    request.Headers.Add("Referer", archiveUrl + "/");
                }

                request.Headers.Add("Sec-Fetch-Dest", "document");
                request.Headers.Add("Sec-Fetch-Mode", "navigate");
                request.Headers.Add("Sec-Fetch-Site", previousHost == null ? "same-origin" : "cross-site");
                request.Headers.Add("Sec-Fetch-User", "?1");
                request.Headers.Add("Upgrade-Insecure-Requests", "1");
                request.Headers.Add("Cache-Control", "no-cache");

                if (rangeStart > 0)
                {
                    request.Headers.Range = new System.Net.Http.Headers.RangeHeaderValue(rangeStart, null);
                }

                var response = await httpClient.SendAsync(request,
                    isDownload ? HttpCompletionOption.ResponseHeadersRead : HttpCompletionOption.ResponseContentRead, ct);

                // Check for redirects
                if ((int)response.StatusCode >= 300 && (int)response.StatusCode < 400)
                {
                    var location = response.Headers.Location;
                    if (location == null)
                    {
                        return response;
                    }

                    previousHost = uri.Host;

                    // Handle relative and absolute redirects
                    if (location.IsAbsoluteUri)
                    {
                        currentUrl = location.ToString();
                    }
                    else
                    {
                        currentUrl = new Uri(uri, location).ToString();
                    }

                    response.Dispose();
                    continue;
                }

                return response;
            }

            throw new Exception("Too many redirects");
        }

        private async Task LoadGameList()
        {
            try
            {
                // Initialize session first
                await InitializeSession();

                // Use archive.org metadata API for reliable JSON data
                string metadataUrl = archiveUrl.Replace("/download/", "/metadata/");

                // Get metadata with redirect handling
                using var response = await SendRequestWithRedirects(metadataUrl, isDownload: false);
                if (response == null || !response.IsSuccessStatusCode)
                {
                    Console.WriteLine($"Failed to fetch metadata: {response?.StatusCode}");
                    return;
                }

                var json = await response.Content.ReadAsStringAsync();

                using var doc = JsonDocument.Parse(json);
                var root = doc.RootElement;

                if (!root.TryGetProperty("files", out var filesArray))
                {
                    Console.WriteLine("No files found in metadata");
                    return;
                }

                foreach (var file in filesArray.EnumerateArray())
                {
                    if (!file.TryGetProperty("name", out var nameProp))
                        continue;

                    string fileName = nameProp.GetString() ?? "";

                    // Only include compressed game files
                    bool isCompressed = fileName.EndsWith(".zip", StringComparison.OrdinalIgnoreCase) ||
                                       fileName.EndsWith(".7z", StringComparison.OrdinalIgnoreCase) ||
                                       fileName.EndsWith(".rar", StringComparison.OrdinalIgnoreCase);

                    if (!isCompressed) continue;

                    // Skip metadata and BIOS files
                    if (fileName.Contains("torrent", StringComparison.OrdinalIgnoreCase))
                        continue;
                    if (fileName.Contains("scph", StringComparison.OrdinalIgnoreCase))
                        continue;
                    if (fileName.Contains("bitmap", StringComparison.OrdinalIgnoreCase))
                        continue;
                    if (fileName.Contains("bios", StringComparison.OrdinalIgnoreCase))
                        continue;
                    if (fileName.Contains("_files", StringComparison.OrdinalIgnoreCase))
                        continue;

                    long size = 0;
                    if (file.TryGetProperty("size", out var sizeProp))
                    {
                        if (sizeProp.ValueKind == JsonValueKind.String)
                            long.TryParse(sizeProp.GetString(), out size);
                        else if (sizeProp.ValueKind == JsonValueKind.Number)
                            size = sizeProp.GetInt64();
                    }

                    // Don't double-encode - archive.org handles the filename directly
                    string fullUrl = $"{archiveUrl}/{fileName.Replace(" ", "%20")}";

                    availableGames.Add(new RemoteGame
                    {
                        FileName = fileName,
                        Url = fullUrl,
                        Size = size,
                        Title = CleanGameTitle(fileName)
                    });
                }

                Console.WriteLine($"Loaded {availableGames.Count} games from archive.org");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error loading game list: {ex.Message}");
            }
        }

        private string CleanGameTitle(string fileName)
        {
            // Remove extension
            string name = Path.GetFileNameWithoutExtension(fileName);

            // Remove common patterns like (USA), [SLUS-00001], etc.
            name = Regex.Replace(name, @"\s*[\(\[].*?[\)\]]", "");

            // Remove underscores and extra spaces
            name = name.Replace("_", " ");
            name = Regex.Replace(name, @"\s+", " ");

            return name.Trim();
        }

        private string CreateSafeFolderName(string title)
        {
            // Remove invalid characters
            string safe = Regex.Replace(title, @"[<>:""/\\|?*]", "");
            safe = Regex.Replace(safe, @"\s+", " ").Trim();

            // Limit length
            if (safe.Length > 60) safe = safe.Substring(0, 60).Trim();

            return safe;
        }

        private async Task PrepareGamesInBackground(CancellationToken ct)
        {
            while (!ct.IsCancellationRequested)
            {
                try
                {
                    // Check if we need to prepare more games
                    int queueCount = preparedGames.Count;
                    int preparingCount;
                    lock (prepareLock) { preparingCount = preparingGames.Count; }

                    if (queueCount + preparingCount < MAX_PREPARED_GAMES)
                    {
                        // Get next random game
                        RemoteGame? nextGame = GetNextRandomGame();
                        if (nextGame != null)
                        {
                            lock (prepareLock)
                            {
                                if (preparingGames.Contains(nextGame.FileName))
                                    continue;
                                preparingGames.Add(nextGame.FileName);
                            }

                            // Prepare the game
                            var prepared = await PrepareGame(nextGame, ct);
                            if (prepared != null)
                            {
                                preparedGames.Enqueue(prepared);
                                Console.WriteLine($"\n[READY] {prepared.Title} is ready to play!");
                            }

                            lock (prepareLock)
                            {
                                preparingGames.Remove(nextGame.FileName);
                            }
                        }
                    }

                    await Task.Delay(1000, ct);
                }
                catch (OperationCanceledException)
                {
                    break;
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"\n[PREP ERROR] {ex.Message}");
                    await Task.Delay(5000, ct);
                }
            }
        }

        private RemoteGame? GetNextRandomGame()
        {
            lock (prepareLock)
            {
                if (playlistState != null)
                {
                    // Loop to skip over games that aren't in available list (may have been removed)
                    while (true)
                    {
                        var nextFileName = playlistState.GetNext();
                        if (nextFileName == null)
                        {
                            // Playlist exhausted - reshuffle and continue
                            Console.WriteLine("\n[INFO] Playlist complete, reshuffling...");
                            playlistState.FullReset(availableGames.Select(g => g.FileName));
                            nextFileName = playlistState.GetNext();
                            if (nextFileName == null) return null;
                        }

                        var game = availableGames.FirstOrDefault(g =>
                            g.FileName.Equals(nextFileName, StringComparison.OrdinalIgnoreCase));

                        if (game != null)
                            return game;

                        // Game not found in available list, skip to next
                        Console.WriteLine($"[WARN] Game not in current list, skipping: {nextFileName}");
                    }
                }

                // Fallback for compatibility
                if (remainingGames.Count == 0)
                {
                    // Reshuffle
                    remainingGames = availableGames.OrderBy(x => random.Next()).ToList();
                }

                if (remainingGames.Count == 0) return null;

                var fallbackGame = remainingGames[0];
                remainingGames.RemoveAt(0);
                return fallbackGame;
            }
        }

        private async Task<PreparedGame?> PrepareGame(RemoteGame game, CancellationToken ct)
        {
            string folderName = CreateSafeFolderName(game.Title);
            string extractPath = Path.Combine(gamesDirectory, folderName);
            string tempFile = Path.Combine(tempDirectory, game.FileName);

            Console.WriteLine($"\n[PREP] Preparing: {game.Title}");

            try
            {
                // Check if game folder already exists (previously downloaded)
                if (Directory.Exists(extractPath))
                {
                    Console.WriteLine($"[PREP] Found existing game folder, using cached version");
                    string? existingGameFile = FindGameFile(extractPath);

                    if (existingGameFile != null)
                    {
                        Console.WriteLine($"[PREP] Using: {Path.GetFileName(existingGameFile)}");
                        return new PreparedGame
                        {
                            Title = game.Title,
                            GameFilePath = existingGameFile,
                            ExtractedPath = extractPath
                        };
                    }
                    else
                    {
                        Console.WriteLine($"[PREP] No game file found in existing folder, re-downloading...");
                        // Delete the incomplete folder and re-download
                        try { Directory.Delete(extractPath, true); } catch { }
                    }
                }

                Console.WriteLine($"[PREP] Size: {FormatSize(game.Size)}");

                // Download if not already downloaded
                if (!File.Exists(tempFile))
                {
                    Console.WriteLine($"[PREP] Downloading...");
                    await DownloadFile(game.Url, tempFile, game.Size, ct);
                }

                // Extract
                Console.WriteLine($"[PREP] Extracting to: {folderName}");
                Directory.CreateDirectory(extractPath);

                string? gameFile = await ExtractArchive(tempFile, extractPath, ct);

                if (gameFile == null)
                {
                    Console.WriteLine($"[PREP] No playable file found in archive");
                    return null;
                }

                // Clean up temp file after successful extraction
                try { File.Delete(tempFile); } catch { }

                return new PreparedGame
                {
                    Title = game.Title,
                    GameFilePath = gameFile,
                    ExtractedPath = extractPath
                };
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[PREP] Failed: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// Finds a playable game file in an already-extracted folder
        /// </summary>
        private string? FindGameFile(string folderPath)
        {
            if (!Directory.Exists(folderPath))
                return null;

            var files = Directory.GetFiles(folderPath, "*.*", SearchOption.AllDirectories);

            // Filter out non-game files
            var gameFiles = files.Where(f =>
                !f.Contains("retroarch", StringComparison.OrdinalIgnoreCase) &&
                !f.Contains("\\cores\\", StringComparison.OrdinalIgnoreCase) &&
                !f.Contains("/cores/", StringComparison.OrdinalIgnoreCase) &&
                !f.EndsWith(".dll", StringComparison.OrdinalIgnoreCase) &&
                !f.EndsWith(".exe", StringComparison.OrdinalIgnoreCase) &&
                !f.EndsWith(".so", StringComparison.OrdinalIgnoreCase)
            ).ToArray();

            // Priority order: .cue, .chd, .iso, .bin (>1MB), .img, .pbp
            return gameFiles.FirstOrDefault(f => f.EndsWith(".cue", StringComparison.OrdinalIgnoreCase))
                ?? gameFiles.FirstOrDefault(f => f.EndsWith(".chd", StringComparison.OrdinalIgnoreCase))
                ?? gameFiles.FirstOrDefault(f => f.EndsWith(".iso", StringComparison.OrdinalIgnoreCase))
                ?? gameFiles.FirstOrDefault(f => f.EndsWith(".bin", StringComparison.OrdinalIgnoreCase) && new FileInfo(f).Length > 1024 * 1024)
                ?? gameFiles.FirstOrDefault(f => f.EndsWith(".img", StringComparison.OrdinalIgnoreCase))
                ?? gameFiles.FirstOrDefault(f => f.EndsWith(".pbp", StringComparison.OrdinalIgnoreCase));
        }

        private async Task DownloadFile(string url, string outputPath, long expectedSize, CancellationToken ct)
        {
            string tempPath = outputPath + ".downloading";
            long existingSize = 0;

            if (File.Exists(tempPath))
            {
                existingSize = new FileInfo(tempPath).Length;
            }

            // Ensure session is initialized before download
            await InitializeSession();

            // Try download with retries
            int maxRetries = 3;
            for (int attempt = 1; attempt <= maxRetries; attempt++)
            {
                try
                {
                    using var response = await SendRequestWithRedirects(url, isDownload: true, rangeStart: existingSize, ct: ct);

                    if (response == null)
                    {
                        throw new Exception("No response received");
                    }

                    bool isPartial = response.StatusCode == System.Net.HttpStatusCode.PartialContent;

                    if (!response.IsSuccessStatusCode && !isPartial)
                    {
                        // Log more details for debugging
                        string statusInfo = $"{(int)response.StatusCode} {response.StatusCode}";

                        if (response.StatusCode == System.Net.HttpStatusCode.Unauthorized ||
                            response.StatusCode == System.Net.HttpStatusCode.Forbidden)
                        {
                            // Reset session and retry
                            sessionInitialized = false;

                            if (attempt < maxRetries)
                            {
                                Console.WriteLine($"\n[PREP] Auth error ({statusInfo}), reinitializing session... (attempt {attempt}/{maxRetries})");
                                await Task.Delay(1000 * attempt, ct);
                                await InitializeSession();
                                continue;
                            }
                        }

                        throw new Exception($"Download failed: {statusInfo}");
                    }

                    if (!isPartial && existingSize > 0)
                    {
                        existingSize = 0;
                    }

                    long totalSize = (response.Content.Headers.ContentLength ?? 0) + existingSize;
                    if (expectedSize == 0) expectedSize = totalSize;

                    var stream = await response.Content.ReadAsStreamAsync(ct);
                    FileMode mode = isPartial ? FileMode.Append : FileMode.Create;

                    const int bufferSize = 1024 * 1024;
                    byte[] buffer = new byte[bufferSize];
                    long totalRead = existingSize;
                    int bytesRead;
                    var lastUpdate = DateTime.Now;
                    long lastBytes = existingSize;

                    // Use explicit try-finally to ensure file is closed before move
                    try
                    {
                        using var fileStream = new FileStream(tempPath, mode, FileAccess.Write, FileShare.None,
                            bufferSize, FileOptions.Asynchronous);

                        while ((bytesRead = await stream.ReadAsync(buffer, ct)) > 0)
                        {
                            await fileStream.WriteAsync(buffer.AsMemory(0, bytesRead), ct);
                            totalRead += bytesRead;

                            var now = DateTime.Now;
                            if ((now - lastUpdate).TotalMilliseconds >= 1000)
                            {
                                double speed = (totalRead - lastBytes) / (now - lastUpdate).TotalSeconds;
                                double percent = expectedSize > 0 ? (double)totalRead / expectedSize * 100 : 0;
                                Console.Write($"\r[PREP] Download: {percent:F1}% ({FormatSize(totalRead)}) - {FormatSize((long)speed)}/s   ");
                                lastBytes = totalRead;
                                lastUpdate = now;
                            }
                        }

                        // Flush and close explicitly
                        await fileStream.FlushAsync(ct);
                    }
                    finally
                    {
                        stream.Dispose();
                    }

                    Console.WriteLine();

                    // Small delay to ensure file handle is fully released
                    await Task.Delay(100, ct);
                    File.Move(tempPath, outputPath, true);
                    return; // Success - exit the retry loop
                }
                catch (HttpRequestException ex) when (attempt < maxRetries)
                {
                    Console.WriteLine($"\n[PREP] Network error: {ex.Message}, retrying... (attempt {attempt}/{maxRetries})");
                    await Task.Delay(1000 * attempt, ct);
                }
            }
        }

        private async Task<string?> ExtractArchive(string archivePath, string extractPath, CancellationToken ct)
        {
            string ext = Path.GetExtension(archivePath).ToLower();

            try
            {
                if (ext == ".zip")
                {
                    ZipFile.ExtractToDirectory(archivePath, extractPath, overwriteFiles: true);
                }
                else if (ext == ".7z" || ext == ".rar")
                {
                    // Use 7z command line for .7z and .rar files
                    await Extract7z(archivePath, extractPath, ct);
                }
                else
                {
                    throw new Exception($"Unsupported archive format: {ext}");
                }

                // Find playable file (prefer .cue, then .chd, then .iso, then .bin)
                var files = Directory.GetFiles(extractPath, "*.*", SearchOption.AllDirectories);
                Console.WriteLine($"[PREP] Found {files.Length} files in archive");

                // Filter out non-game files (RetroArch cores, executables, etc.)
                var gameFiles = files.Where(f =>
                    !f.Contains("retroarch", StringComparison.OrdinalIgnoreCase) &&
                    !f.Contains("\\cores\\", StringComparison.OrdinalIgnoreCase) &&
                    !f.Contains("/cores/", StringComparison.OrdinalIgnoreCase) &&
                    !f.EndsWith(".dll", StringComparison.OrdinalIgnoreCase) &&
                    !f.EndsWith(".exe", StringComparison.OrdinalIgnoreCase) &&
                    !f.EndsWith(".so", StringComparison.OrdinalIgnoreCase)
                ).ToArray();

                // .cue files are best - they reference the .bin tracks properly
                string? cueFile = gameFiles.FirstOrDefault(f => f.EndsWith(".cue", StringComparison.OrdinalIgnoreCase));
                if (cueFile != null)
                {
                    Console.WriteLine($"[PREP] Using .cue file: {Path.GetFileName(cueFile)}");
                    return cueFile;
                }

                // .chd is a compressed disc format, very common for PSX
                string? chdFile = gameFiles.FirstOrDefault(f => f.EndsWith(".chd", StringComparison.OrdinalIgnoreCase));
                if (chdFile != null)
                {
                    Console.WriteLine($"[PREP] Using .chd file: {Path.GetFileName(chdFile)}");
                    return chdFile;
                }

                // .iso is standard disc image
                string? isoFile = gameFiles.FirstOrDefault(f => f.EndsWith(".iso", StringComparison.OrdinalIgnoreCase));
                if (isoFile != null)
                {
                    Console.WriteLine($"[PREP] Using .iso file: {Path.GetFileName(isoFile)}");
                    return isoFile;
                }

                // .bin - look for large files (>1MB) to avoid small binaries
                string? binFile = gameFiles.FirstOrDefault(f =>
                    f.EndsWith(".bin", StringComparison.OrdinalIgnoreCase) &&
                    new FileInfo(f).Length > 1024 * 1024); // > 1MB
                if (binFile != null)
                {
                    Console.WriteLine($"[PREP] Using .bin file: {Path.GetFileName(binFile)}");
                    return binFile;
                }

                string? imgFile = gameFiles.FirstOrDefault(f => f.EndsWith(".img", StringComparison.OrdinalIgnoreCase));
                if (imgFile != null)
                {
                    Console.WriteLine($"[PREP] Using .img file: {Path.GetFileName(imgFile)}");
                    return imgFile;
                }

                // .pbp is PSP/PSX format
                string? pbpFile = gameFiles.FirstOrDefault(f => f.EndsWith(".pbp", StringComparison.OrdinalIgnoreCase));
                if (pbpFile != null)
                {
                    Console.WriteLine($"[PREP] Using .pbp file: {Path.GetFileName(pbpFile)}");
                    return pbpFile;
                }

                Console.WriteLine($"[PREP] No standard game file found");
                // List what we did find for debugging
                var extensions = gameFiles.Take(20).Select(f => Path.GetExtension(f)).Distinct();
                Console.WriteLine($"[PREP] File types found: {string.Join(", ", extensions)}");
                return null;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[PREP] Extraction error: {ex.Message}");
                return null;
            }
        }

        private async Task Extract7z(string archivePath, string extractPath, CancellationToken ct)
        {
            // Try common 7z locations
            string[] sevenZipPaths = {
                @"C:\Program Files\7-Zip\7z.exe",
                @"C:\Program Files (x86)\7-Zip\7z.exe",
                "7z.exe" // In PATH
            };

            string? sevenZip = sevenZipPaths.FirstOrDefault(File.Exists);
            if (sevenZip == null && !sevenZipPaths.Any(p => p == "7z.exe"))
            {
                throw new Exception("7-Zip not found. Please install 7-Zip to extract .7z and .rar files.");
            }

            sevenZip ??= "7z.exe";

            var psi = new ProcessStartInfo
            {
                FileName = sevenZip,
                Arguments = $"x \"{archivePath}\" -o\"{extractPath}\" -y",
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true
            };

            using var process = Process.Start(psi);
            if (process == null)
                throw new Exception("Failed to start 7z process");

            await process.WaitForExitAsync(ct);

            if (process.ExitCode != 0)
            {
                string error = await process.StandardError.ReadToEndAsync(ct);
                throw new Exception($"7z extraction failed: {error}");
            }
        }

        private void ShowGameTitle(PreparedGame game, int gameNumber)
        {
            try { Console.Clear(); } catch { }
            Console.ForegroundColor = ConsoleColor.Blue;
            Console.WriteLine("╔════════════════════════════════════════════════════════════════╗");
            Console.WriteLine($"║{CenterText("PSX Streaming - NOW PLAYING", 64)}║");
            Console.WriteLine("╚════════════════════════════════════════════════════════════════╝");
            Console.ResetColor();
            Console.WriteLine();

            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.WriteLine($"  Game #{gameNumber}");
            Console.ResetColor();

            Console.WriteLine();
            Console.ForegroundColor = ConsoleColor.White;
            Console.WriteLine($"  {game.Title}");
            Console.ResetColor();
            Console.ForegroundColor = ConsoleColor.DarkGray;
            Console.WriteLine($"  {Path.GetFileName(game.GameFilePath)}");
            Console.ResetColor();
            Console.WriteLine();

            // Show queue status
            Console.ForegroundColor = ConsoleColor.Cyan;
            Console.WriteLine($"  Games ready in queue: {preparedGames.Count}");
            Console.ResetColor();
            Console.WriteLine();
        }

        private async Task PlayGame(PreparedGame game, CancellationToken ct)
        {
            try
            {
#if !CROSS_PLATFORM
                // Load window position
                WindowManager.WindowPosition? savedPosition = null;
                try
                {
                    savedPosition = WindowManager.LoadWindowPosition(windowConfigPath);
                }
                catch { }
#endif

                var psi = new ProcessStartInfo
                {
                    FileName = emulatorPath,
                    Arguments = $"\"{game.GameFilePath}\"",
                    UseShellExecute = false
                };

                Console.WriteLine($"Launching: {emulatorPath}");
                Console.WriteLine($"Game file: {game.GameFilePath}");
                Console.WriteLine("Press any key to stop and continue to next game...\n");

                currentProcess = Process.Start(psi);
                if (currentProcess == null)
                {
                    Console.WriteLine("Failed to start emulator");
                    return;
                }

#if !CROSS_PLATFORM
                // Apply window position
                if (savedPosition != null)
                {
                    var hWnd = await WindowManager.WaitForProcessWindowAsync(currentProcess, 5000);
                    if (hWnd != IntPtr.Zero)
                    {
                        WindowManager.SetWindowPosition(hWnd, savedPosition);
                    }
                }
#endif

                // Wait for key press or process exit
                while (!Console.KeyAvailable && !currentProcess.HasExited && !ct.IsCancellationRequested)
                {
                    await Task.Delay(100);
                }

                if (Console.KeyAvailable)
                {
                    Console.ReadKey(true);
                }

#if !CROSS_PLATFORM
                // Save window position
                if (!currentProcess.HasExited)
                {
                    try
                    {
                        currentProcess.Refresh();
                        if (currentProcess.MainWindowHandle != IntPtr.Zero)
                        {
                            var pos = WindowManager.GetWindowPosition(currentProcess.MainWindowHandle);
                            if (pos != null)
                            {
                                WindowManager.SaveWindowPosition(pos, windowConfigPath);
                            }
                        }
                    }
                    catch { }
                }
#endif

                // Close emulator with auto-confirm for any dialogs
                if (!currentProcess.HasExited)
                {
                    currentProcess.CloseMainWindow();

                    // Auto-confirm any "Close game?" or "Save state?" dialogs
                    await WindowManager.AutoConfirmDialogsAsync(currentProcess, maxWaitMs: 3000);

                    // If still not exited after auto-confirm, force kill
                    if (!currentProcess.HasExited)
                    {
                        if (!currentProcess.WaitForExit(1000))
                        {
                            currentProcess.Kill();
                        }
                    }
                }
            }
            finally
            {
                currentProcess?.Dispose();
                currentProcess = null;
            }
        }

        private void CleanupTempFiles()
        {
            try
            {
                if (Directory.Exists(tempDirectory))
                {
                    foreach (var file in Directory.GetFiles(tempDirectory))
                    {
                        try { File.Delete(file); } catch { }
                    }
                }
            }
            catch { }
        }

        private static string CenterText(string text, int width)
        {
            if (text.Length >= width) return text.Substring(0, width);
            int padding = (width - text.Length) / 2;
            return text.PadLeft(padding + text.Length).PadRight(width);
        }

        private static string FormatSize(long bytes)
        {
            string[] sizes = { "B", "KB", "MB", "GB" };
            double len = bytes;
            int order = 0;
            while (len >= 1024 && order < sizes.Length - 1) { order++; len /= 1024; }
            return $"{len:0.##} {sizes[order]}";
        }

        public void Dispose()
        {
            if (isDisposed) return;
            isDisposed = true;

            cts?.Cancel();
            currentProcess?.Kill();
            currentProcess?.Dispose();
            httpClient.Dispose();

            GC.SuppressFinalize(this);
        }
    }

    class RemoteGame
    {
        public string FileName { get; set; } = string.Empty;
        public string Url { get; set; } = string.Empty;
        public string Title { get; set; } = string.Empty;
        public long Size { get; set; }
    }

    class PreparedGame
    {
        public string Title { get; set; } = string.Empty;
        public string GameFilePath { get; set; } = string.Empty;
        public string ExtractedPath { get; set; } = string.Empty;
    }
}
