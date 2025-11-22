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
    /// Streams PS2 games from archive.org - downloads and plays on demand
    /// </summary>
    public class StreamingPS2Player : IDisposable
    {
        private const string DEFAULT_ARCHIVE_URL = "https://archive.org/download/playstation2_essentials";
        private const string DEFAULT_EMULATOR_PATH = @"C:\Users\rob\Games\apps\pcsx2\pcsx2-qt.exe";
        private const string DEFAULT_GAMES_DIR = @"C:\Users\rob\Games\PS2";
        private const int MAX_PREPARED_GAMES = 2;

        private readonly string archiveUrl;
        private readonly string emulatorPath;
        private readonly string gamesDirectory;
        private readonly string tempDirectory;
        private readonly string windowConfigPath;

        private readonly HttpClient httpClient;
        private readonly System.Net.CookieContainer cookieContainer;
        private readonly ConcurrentQueue<PreparedPS2Game> preparedGames = new();
        private readonly HashSet<string> preparingGames = new();
        private readonly object prepareLock = new();

        private List<RemotePS2Game> availableGames = new();
        private List<RemotePS2Game> remainingGames = new();
        private Random random = new();
        private CancellationTokenSource? cts;
        private Task? preparationTask;
        private Process? currentProcess;
        private bool isDisposed;
        private bool sessionInitialized = false;

        public StreamingPS2Player(string? archiveUrl = null, string? emulatorPath = null, string? gamesDirectory = null)
        {
            this.archiveUrl = archiveUrl ?? DEFAULT_ARCHIVE_URL;
            this.emulatorPath = emulatorPath ?? DEFAULT_EMULATOR_PATH;
            this.gamesDirectory = gamesDirectory ?? DEFAULT_GAMES_DIR;
            this.tempDirectory = Path.Combine(this.gamesDirectory, ".streaming_temp");
            this.windowConfigPath = Path.Combine(this.gamesDirectory, "pcsx2_window.json");

            Directory.CreateDirectory(this.gamesDirectory);
            Directory.CreateDirectory(this.tempDirectory);

            cookieContainer = new System.Net.CookieContainer();
            var handler = new HttpClientHandler
            {
                AllowAutoRedirect = false,
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
            Console.ForegroundColor = ConsoleColor.Magenta;
            Console.WriteLine("╔════════════════════════════════════════════════════════════════╗");
            Console.WriteLine("║           PS2 Streaming Player - Archive.org Edition           ║");
            Console.WriteLine("╚════════════════════════════════════════════════════════════════╝");
            Console.ResetColor();
            Console.WriteLine();
            Console.WriteLine($"Archive: {archiveUrl}");
            Console.WriteLine();

            if (!File.Exists(emulatorPath))
            {
                Console.WriteLine($"ERROR: PCSX2 not found at: {emulatorPath}");
                return;
            }

            Console.WriteLine("Fetching game list from archive.org...");
            await LoadGameList();

            if (availableGames.Count == 0)
            {
                Console.WriteLine("No games found!");
                return;
            }

            Console.WriteLine($"Found {availableGames.Count} PS2 games available for streaming\n");

            cts = new CancellationTokenSource();
            Console.CancelKeyPress += (s, e) =>
            {
                e.Cancel = true;
                Console.WriteLine("\n\nShutting down...");
                cts.Cancel();
            };

            remainingGames = availableGames.OrderBy(x => random.Next()).ToList();
            preparationTask = PrepareGamesInBackground(cts.Token);

            int gamesPlayed = 0;

            try
            {
                while (!cts.Token.IsCancellationRequested)
                {
                    Console.WriteLine("\nWaiting for next game to be ready...");

                    PreparedPS2Game? game = null;
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

                    ShowGameTitle(game, gamesPlayed);
                    await PlayGame(game, cts.Token);
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

                if (isDownload)
                {
                    request.Headers.Add("Accept", "*/*");
                    request.Headers.Add("Accept-Encoding", "identity");
                }
                else
                {
                    request.Headers.Add("Accept", "text/html,application/xhtml+xml,application/xml;q=0.9,image/avif,image/webp,*/*;q=0.8");
                }

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

                if ((int)response.StatusCode >= 300 && (int)response.StatusCode < 400)
                {
                    var location = response.Headers.Location;
                    if (location == null)
                    {
                        return response;
                    }

                    previousHost = uri.Host;

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
                await InitializeSession();

                string metadataUrl = archiveUrl.Replace("/download/", "/metadata/");

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

                    // PS2 games are typically .iso, .chd, .bin/cue, or compressed (.7z, .zip)
                    bool isGameFile = fileName.EndsWith(".iso", StringComparison.OrdinalIgnoreCase) ||
                                     fileName.EndsWith(".chd", StringComparison.OrdinalIgnoreCase) ||
                                     fileName.EndsWith(".7z", StringComparison.OrdinalIgnoreCase) ||
                                     fileName.EndsWith(".zip", StringComparison.OrdinalIgnoreCase) ||
                                     fileName.EndsWith(".rar", StringComparison.OrdinalIgnoreCase);

                    if (!isGameFile) continue;

                    // Skip metadata files
                    if (fileName.Contains("torrent", StringComparison.OrdinalIgnoreCase))
                        continue;

                    long size = 0;
                    if (file.TryGetProperty("size", out var sizeProp))
                    {
                        if (sizeProp.ValueKind == JsonValueKind.String)
                            long.TryParse(sizeProp.GetString(), out size);
                        else if (sizeProp.ValueKind == JsonValueKind.Number)
                            size = sizeProp.GetInt64();
                    }

                    string fullUrl = $"{archiveUrl}/{fileName.Replace(" ", "%20")}";

                    availableGames.Add(new RemotePS2Game
                    {
                        FileName = fileName,
                        Url = fullUrl,
                        Size = size,
                        Title = CleanGameTitle(fileName),
                        IsCompressed = fileName.EndsWith(".7z", StringComparison.OrdinalIgnoreCase) ||
                                      fileName.EndsWith(".zip", StringComparison.OrdinalIgnoreCase) ||
                                      fileName.EndsWith(".rar", StringComparison.OrdinalIgnoreCase)
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
            string name = Path.GetFileNameWithoutExtension(fileName);
            name = Regex.Replace(name, @"\s*[\(\[].*?[\)\]]", "");
            name = name.Replace("_", " ");
            name = Regex.Replace(name, @"\s+", " ");
            return name.Trim();
        }

        private string CreateSafeFolderName(string title)
        {
            string safe = Regex.Replace(title, @"[<>:""/\\|?*]", "");
            safe = Regex.Replace(safe, @"\s+", " ").Trim();
            if (safe.Length > 60) safe = safe.Substring(0, 60).Trim();
            return safe;
        }

        private async Task PrepareGamesInBackground(CancellationToken ct)
        {
            while (!ct.IsCancellationRequested)
            {
                try
                {
                    int queueCount = preparedGames.Count;
                    int preparingCount;
                    lock (prepareLock) { preparingCount = preparingGames.Count; }

                    if (queueCount + preparingCount < MAX_PREPARED_GAMES)
                    {
                        RemotePS2Game? nextGame = GetNextRandomGame();
                        if (nextGame != null)
                        {
                            lock (prepareLock)
                            {
                                if (preparingGames.Contains(nextGame.FileName))
                                    continue;
                                preparingGames.Add(nextGame.FileName);
                            }

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

        private RemotePS2Game? GetNextRandomGame()
        {
            lock (prepareLock)
            {
                if (remainingGames.Count == 0)
                {
                    remainingGames = availableGames.OrderBy(x => random.Next()).ToList();
                }

                if (remainingGames.Count == 0) return null;

                var game = remainingGames[0];
                remainingGames.RemoveAt(0);
                return game;
            }
        }

        private async Task<PreparedPS2Game?> PrepareGame(RemotePS2Game game, CancellationToken ct)
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
                        return new PreparedPS2Game
                        {
                            Title = game.Title,
                            GameFilePath = existingGameFile,
                            ExtractedPath = extractPath
                        };
                    }
                    else
                    {
                        Console.WriteLine($"[PREP] No game file found in existing folder, re-downloading...");
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

                string? gameFile;

                if (game.IsCompressed)
                {
                    // Extract compressed files
                    Console.WriteLine($"[PREP] Extracting to: {folderName}");
                    Directory.CreateDirectory(extractPath);
                    gameFile = await ExtractArchive(tempFile, extractPath, ct);

                    // Clean up temp file after successful extraction
                    try { File.Delete(tempFile); } catch { }
                }
                else
                {
                    // Direct ISO/CHD - just move to destination
                    Directory.CreateDirectory(extractPath);
                    string destFile = Path.Combine(extractPath, game.FileName);

                    Console.WriteLine($"[PREP] Moving to: {folderName}");
                    File.Move(tempFile, destFile, true);
                    gameFile = destFile;
                }

                if (gameFile == null)
                {
                    Console.WriteLine($"[PREP] No playable file found");
                    return null;
                }

                return new PreparedPS2Game
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

        private string? FindGameFile(string folderPath)
        {
            if (!Directory.Exists(folderPath))
                return null;

            var files = Directory.GetFiles(folderPath, "*.*", SearchOption.AllDirectories);

            var gameFiles = files.Where(f =>
                !f.EndsWith(".dll", StringComparison.OrdinalIgnoreCase) &&
                !f.EndsWith(".exe", StringComparison.OrdinalIgnoreCase) &&
                !f.EndsWith(".txt", StringComparison.OrdinalIgnoreCase) &&
                !f.EndsWith(".nfo", StringComparison.OrdinalIgnoreCase)
            ).ToArray();

            // Priority: .iso, .chd, .bin (>100MB for PS2)
            return gameFiles.FirstOrDefault(f => f.EndsWith(".iso", StringComparison.OrdinalIgnoreCase))
                ?? gameFiles.FirstOrDefault(f => f.EndsWith(".chd", StringComparison.OrdinalIgnoreCase))
                ?? gameFiles.FirstOrDefault(f => f.EndsWith(".bin", StringComparison.OrdinalIgnoreCase) && new FileInfo(f).Length > 100 * 1024 * 1024)
                ?? gameFiles.FirstOrDefault(f => f.EndsWith(".cue", StringComparison.OrdinalIgnoreCase));
        }

        private async Task DownloadFile(string url, string outputPath, long expectedSize, CancellationToken ct)
        {
            string tempPath = outputPath + ".downloading";
            long existingSize = 0;

            if (File.Exists(tempPath))
            {
                existingSize = new FileInfo(tempPath).Length;
            }

            await InitializeSession();

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
                        string statusInfo = $"{(int)response.StatusCode} {response.StatusCode}";

                        if (response.StatusCode == System.Net.HttpStatusCode.Unauthorized ||
                            response.StatusCode == System.Net.HttpStatusCode.Forbidden)
                        {
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

                        await fileStream.FlushAsync(ct);
                    }
                    finally
                    {
                        stream.Dispose();
                    }

                    Console.WriteLine();

                    await Task.Delay(100, ct);
                    File.Move(tempPath, outputPath, true);
                    return;
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
                    await Extract7z(archivePath, extractPath, ct);
                }
                else
                {
                    throw new Exception($"Unsupported archive format: {ext}");
                }

                var files = Directory.GetFiles(extractPath, "*.*", SearchOption.AllDirectories);
                Console.WriteLine($"[PREP] Found {files.Length} files in archive");

                return FindGameFile(extractPath);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[PREP] Extraction error: {ex.Message}");
                return null;
            }
        }

        private async Task Extract7z(string archivePath, string extractPath, CancellationToken ct)
        {
            string[] sevenZipPaths = {
                @"C:\Program Files\7-Zip\7z.exe",
                @"C:\Program Files (x86)\7-Zip\7z.exe",
                "7z.exe"
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

        private void ShowGameTitle(PreparedPS2Game game, int gameNumber)
        {
            try { Console.Clear(); } catch { }
            Console.ForegroundColor = ConsoleColor.Magenta;
            Console.WriteLine("╔════════════════════════════════════════════════════════════════╗");
            Console.WriteLine($"║{CenterText("PS2 Streaming - NOW PLAYING", 64)}║");
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

            Console.ForegroundColor = ConsoleColor.Cyan;
            Console.WriteLine($"  Games ready in queue: {preparedGames.Count}");
            Console.ResetColor();
            Console.WriteLine();
        }

        private async Task PlayGame(PreparedPS2Game game, CancellationToken ct)
        {
            try
            {
#if !CROSS_PLATFORM
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
                // Apply window position and display options
                var hWnd = await WindowManager.WaitForProcessWindowAsync(currentProcess, 5000);
                if (hWnd != IntPtr.Zero)
                {
                    if (savedPosition != null)
                    {
                        WindowManager.SetWindowPosition(hWnd, savedPosition);
                    }

                    // Apply display options with PCSX2's Alt+Enter fullscreen
                    DisplayOptions.Current?.ApplyToWindow(hWnd, WindowManager.FullscreenMethod.AltEnter);
                }
#endif

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

                // Close emulator
                if (!currentProcess.HasExited)
                {
                    currentProcess.CloseMainWindow();

                    // Auto-confirm any dialogs
                    await WindowManager.AutoConfirmDialogsAsync(currentProcess, maxWaitMs: 3000);

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

    class RemotePS2Game
    {
        public string FileName { get; set; } = string.Empty;
        public string Url { get; set; } = string.Empty;
        public string Title { get; set; } = string.Empty;
        public long Size { get; set; }
        public bool IsCompressed { get; set; }
    }

    class PreparedPS2Game
    {
        public string Title { get; set; } = string.Empty;
        public string GameFilePath { get; set; } = string.Empty;
        public string ExtractedPath { get; set; } = string.Empty;
    }
}
