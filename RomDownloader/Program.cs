using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using HtmlAgilityPack;

namespace RomDownloader
{
    class Program
    {
        // Predefined sources
        private static readonly Dictionary<string, SourceConfig> SOURCES = new()
        {
            ["sega_saturn"] = new SourceConfig
            {
                Name = "Sega Saturn",
                Url = "https://myrient.erista.me/files/Redump/Sega%20-%20Saturn/",
                OutputDir = @"C:\Users\rob\Games\Sega_Saturn",
                Extensions = new[] { ".chd", ".zip", ".7z" }
            },
            ["ps1"] = new SourceConfig
            {
                Name = "PlayStation 1",
                Url = "https://myrient.erista.me/files/Redump/Sony%20-%20PlayStation/",
                OutputDir = @"C:\Users\rob\Games\PS1",
                Extensions = new[] { ".chd", ".zip", ".7z" }
            },
            ["ps2"] = new SourceConfig
            {
                Name = "PlayStation 2",
                Url = "https://myrient.erista.me/files/Redump/Sony%20-%20PlayStation%202/",
                OutputDir = @"C:\Users\rob\Games\PS2",
                Extensions = new[] { ".chd", ".zip", ".7z" }
            },
            ["dreamcast"] = new SourceConfig
            {
                Name = "Sega Dreamcast",
                Url = "https://myrient.erista.me/files/Redump/Sega%20-%20Dreamcast/",
                OutputDir = @"C:\Users\rob\Games\Dreamcast",
                Extensions = new[] { ".chd", ".zip", ".7z" }
            },
            ["gamecube"] = new SourceConfig
            {
                Name = "Nintendo GameCube",
                Url = "https://myrient.erista.me/files/Redump/Nintendo%20-%20GameCube%20-%20NKit%20RVZ%20[zstd-19-128k]/",
                OutputDir = @"C:\Users\rob\Games\GameCube",
                Extensions = new[] { ".rvz", ".zip", ".7z" }
            }
        };

        static async Task<int> Main(string[] args)
        {
            Console.WriteLine("=== ROM Downloader ===\n");

            if (args.Length == 0 || args[0] == "help" || args[0] == "--help" || args[0] == "-h")
            {
                ShowUsage();
                return 0;
            }

            string command = args[0].ToLower();

            // List available sources
            if (command == "list")
            {
                Console.WriteLine("Available sources:\n");
                foreach (var (key, config) in SOURCES)
                {
                    Console.WriteLine($"  {key,-15} {config.Name}");
                    Console.WriteLine($"                  URL: {config.Url}");
                    Console.WriteLine($"                  Output: {config.OutputDir}\n");
                }
                return 0;
            }

            // Custom URL download
            if (command == "custom" && args.Length >= 3)
            {
                string url = args[1];
                string outputDir = args[2];
                return await DownloadFromUrl(url, outputDir, args);
            }

            // Predefined source download
            if (SOURCES.TryGetValue(command, out var source))
            {
                Console.WriteLine($"Downloading: {source.Name}");
                Console.WriteLine($"Source: {source.Url}");
                Console.WriteLine($"Output: {source.OutputDir}\n");

                return await DownloadFromUrl(source.Url, source.OutputDir, args, source.Extensions);
            }

            Console.WriteLine($"Unknown command or source: {command}");
            ShowUsage();
            return 1;
        }

        static void ShowUsage()
        {
            Console.WriteLine(@"
Usage: RomDownloader.exe <source|command> [options]

=== PREDEFINED SOURCES ===
  sega_saturn    Sega Saturn (Redump CHD)
  ps1            PlayStation 1 (Redump CHD)
  ps2            PlayStation 2 (Redump CHD)
  dreamcast      Sega Dreamcast (Redump CHD)
  gamecube       Nintendo GameCube (NKit RVZ)

=== COMMANDS ===
  list           Show all predefined sources
  custom <url> <output_dir>   Download from custom URL

=== OPTIONS ===
  --filter <text>     Only download files containing this text
  --skip <n>          Skip first N files
  --limit <n>         Download only N files
  --dry-run           Show what would be downloaded without downloading

=== EXAMPLES ===
  RomDownloader.exe sega_saturn
  RomDownloader.exe sega_saturn --filter ""USA""
  RomDownloader.exe sega_saturn --skip 10 --limit 5
  RomDownloader.exe ps1 --filter ""Final Fantasy""
  RomDownloader.exe custom ""https://example.com/roms/"" ""C:\Games\Custom""

=== FEATURES ===
  - Automatic resume of interrupted downloads
  - Progress tracking with speed display
  - Sequential 1-by-1 downloading
  - Skip already downloaded files
");
        }

        static async Task<int> DownloadFromUrl(string url, string outputDir, string[] args, string[]? extensions = null)
        {
            // Parse options
            string? filter = null;
            int skip = 0;
            int limit = int.MaxValue;
            bool dryRun = false;

            for (int i = 0; i < args.Length; i++)
            {
                switch (args[i].ToLower())
                {
                    case "--filter" when i + 1 < args.Length:
                        filter = args[++i];
                        break;
                    case "--skip" when i + 1 < args.Length:
                        int.TryParse(args[++i], out skip);
                        break;
                    case "--limit" when i + 1 < args.Length:
                        int.TryParse(args[++i], out limit);
                        break;
                    case "--dry-run":
                        dryRun = true;
                        break;
                }
            }

            // Ensure output directory exists
            Directory.CreateDirectory(outputDir);

            // Create progress file path
            string progressFile = Path.Combine(outputDir, ".download_progress.txt");

            Console.WriteLine("Fetching file list...");

            try
            {
                var downloader = new RomDownloaderEngine();
                var files = await downloader.GetFileList(url, extensions);

                if (files.Count == 0)
                {
                    Console.WriteLine("No files found at the specified URL.");
                    return 1;
                }

                Console.WriteLine($"Found {files.Count} files\n");

                // Apply filter
                if (!string.IsNullOrEmpty(filter))
                {
                    files = files.Where(f => f.Name.Contains(filter, StringComparison.OrdinalIgnoreCase)).ToList();
                    Console.WriteLine($"After filter '{filter}': {files.Count} files\n");
                }

                // Apply skip and limit
                files = files.Skip(skip).Take(limit).ToList();

                if (dryRun)
                {
                    Console.WriteLine("=== DRY RUN - Files to download ===\n");
                    foreach (var file in files)
                    {
                        Console.WriteLine($"  {file.Name} ({FormatSize(file.Size)})");
                    }
                    Console.WriteLine($"\nTotal: {files.Count} files, {FormatSize(files.Sum(f => f.Size))}");
                    return 0;
                }

                // Load progress
                var completed = LoadProgress(progressFile);
                int downloaded = 0;
                int skipped = 0;

                Console.WriteLine($"Starting download of {files.Count} files...\n");

                var cts = new CancellationTokenSource();
                Console.CancelKeyPress += (s, e) =>
                {
                    e.Cancel = true;
                    Console.WriteLine("\n\nCancelling... (will finish current file)");
                    cts.Cancel();
                };

                foreach (var file in files)
                {
                    if (cts.Token.IsCancellationRequested) break;

                    string outputPath = Path.Combine(outputDir, file.Name);

                    // Skip if already completed
                    if (completed.Contains(file.Name))
                    {
                        Console.WriteLine($"[SKIP] {file.Name} (already downloaded)");
                        skipped++;
                        continue;
                    }

                    // Skip if file exists with correct size
                    if (File.Exists(outputPath))
                    {
                        var existingSize = new FileInfo(outputPath).Length;
                        if (existingSize == file.Size)
                        {
                            Console.WriteLine($"[SKIP] {file.Name} (already exists)");
                            SaveProgress(progressFile, file.Name);
                            skipped++;
                            continue;
                        }
                    }

                    Console.WriteLine($"\n[{downloaded + 1}/{files.Count - skipped}] Downloading: {file.Name}");
                    Console.WriteLine($"Size: {FormatSize(file.Size)}");

                    bool success = await downloader.DownloadFile(file.Url, outputPath, file.Size, cts.Token);

                    if (success)
                    {
                        SaveProgress(progressFile, file.Name);
                        downloaded++;
                        Console.WriteLine($"[OK] Downloaded successfully");
                    }
                    else if (!cts.Token.IsCancellationRequested)
                    {
                        Console.WriteLine($"[FAIL] Download failed");
                    }
                }

                Console.WriteLine($"\n=== Download Complete ===");
                Console.WriteLine($"Downloaded: {downloaded}");
                Console.WriteLine($"Skipped: {skipped}");
                Console.WriteLine($"Output: {outputDir}");

                return 0;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"\nError: {ex.Message}");
                return 1;
            }
        }

        static HashSet<string> LoadProgress(string path)
        {
            if (!File.Exists(path)) return new HashSet<string>();
            return File.ReadAllLines(path).ToHashSet();
        }

        static void SaveProgress(string path, string fileName)
        {
            File.AppendAllText(path, fileName + Environment.NewLine);
        }

        static string FormatSize(long bytes)
        {
            string[] sizes = { "B", "KB", "MB", "GB", "TB" };
            double len = bytes;
            int order = 0;
            while (len >= 1024 && order < sizes.Length - 1)
            {
                order++;
                len /= 1024;
            }
            return $"{len:0.##} {sizes[order]}";
        }
    }

    class SourceConfig
    {
        public string Name { get; set; } = string.Empty;
        public string Url { get; set; } = string.Empty;
        public string OutputDir { get; set; } = string.Empty;
        public string[] Extensions { get; set; } = Array.Empty<string>();
    }

    class RemoteFile
    {
        public string Name { get; set; } = string.Empty;
        public string Url { get; set; } = string.Empty;
        public long Size { get; set; }
    }

    class RomDownloaderEngine
    {
        private readonly HttpClient _client;

        public RomDownloaderEngine()
        {
            var handler = new HttpClientHandler
            {
                AllowAutoRedirect = true
            };
            _client = new HttpClient(handler)
            {
                Timeout = TimeSpan.FromHours(4) // Long timeout for large files
            };
            _client.DefaultRequestHeaders.UserAgent.ParseAdd("Mozilla/5.0 (Windows NT 10.0; Win64; x64) RomDownloader/1.0");
        }

        public async Task<List<RemoteFile>> GetFileList(string url, string[]? extensions = null)
        {
            var files = new List<RemoteFile>();

            var response = await _client.GetStringAsync(url);
            var doc = new HtmlDocument();
            doc.LoadHtml(response);

            // Parse directory listing - handle different formats
            var links = doc.DocumentNode.SelectNodes("//a[@href]");
            if (links == null) return files;

            foreach (var link in links)
            {
                string href = link.GetAttributeValue("href", "");
                string text = link.InnerText.Trim();

                // Skip parent directory and empty links
                if (string.IsNullOrEmpty(href) || href == "../" || href.StartsWith("?"))
                    continue;

                // Skip if doesn't match extensions
                if (extensions != null && extensions.Length > 0)
                {
                    bool matchesExt = extensions.Any(ext =>
                        href.EndsWith(ext, StringComparison.OrdinalIgnoreCase) ||
                        text.EndsWith(ext, StringComparison.OrdinalIgnoreCase));
                    if (!matchesExt) continue;
                }

                // Build full URL
                string fileUrl = href.StartsWith("http") ? href : new Uri(new Uri(url), href).ToString();
                string fileName = Uri.UnescapeDataString(Path.GetFileName(new Uri(fileUrl).LocalPath));

                // Try to get size from the page (varies by server)
                long size = 0;
                var parent = link.ParentNode;
                if (parent != null)
                {
                    string parentText = parent.InnerText;
                    size = ParseSize(parentText);
                }

                files.Add(new RemoteFile
                {
                    Name = fileName,
                    Url = fileUrl,
                    Size = size
                });
            }

            return files.OrderBy(f => f.Name).ToList();
        }

        private long ParseSize(string text)
        {
            // Try to extract size from text like "123M" or "1.5G" or "500K"
            var match = System.Text.RegularExpressions.Regex.Match(text, @"(\d+\.?\d*)\s*([KMGT])?B?\b",
                System.Text.RegularExpressions.RegexOptions.IgnoreCase);

            if (!match.Success) return 0;

            if (!double.TryParse(match.Groups[1].Value, out double value)) return 0;

            string unit = match.Groups[2].Value.ToUpper();
            return unit switch
            {
                "K" => (long)(value * 1024),
                "M" => (long)(value * 1024 * 1024),
                "G" => (long)(value * 1024 * 1024 * 1024),
                "T" => (long)(value * 1024 * 1024 * 1024 * 1024),
                _ => (long)value
            };
        }

        public async Task<bool> DownloadFile(string url, string outputPath, long expectedSize, CancellationToken ct)
        {
            string tempPath = outputPath + ".downloading";
            long existingSize = 0;

            // Check for partial download
            if (File.Exists(tempPath))
            {
                existingSize = new FileInfo(tempPath).Length;
                Console.WriteLine($"Resuming from {FormatSize(existingSize)}...");
            }

            try
            {
                var request = new HttpRequestMessage(HttpMethod.Get, url);

                // Add range header for resume
                if (existingSize > 0)
                {
                    request.Headers.Range = new System.Net.Http.Headers.RangeHeaderValue(existingSize, null);
                }

                using var response = await _client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, ct);

                // Check if server supports range requests
                bool isPartialContent = response.StatusCode == System.Net.HttpStatusCode.PartialContent;
                bool isOk = response.StatusCode == System.Net.HttpStatusCode.OK;

                if (!isPartialContent && !isOk)
                {
                    Console.WriteLine($"Server returned: {response.StatusCode}");
                    return false;
                }

                // If server doesn't support resume, start fresh
                if (!isPartialContent && existingSize > 0)
                {
                    Console.WriteLine("Server doesn't support resume, starting fresh...");
                    existingSize = 0;
                }

                long totalSize = response.Content.Headers.ContentLength ?? 0;
                if (isPartialContent)
                {
                    totalSize += existingSize;
                }

                if (expectedSize == 0) expectedSize = totalSize;

                using var stream = await response.Content.ReadAsStreamAsync(ct);

                FileMode mode = isPartialContent ? FileMode.Append : FileMode.Create;
                using var fileStream = new FileStream(tempPath, mode, FileAccess.Write, FileShare.None, 81920);

                byte[] buffer = new byte[81920];
                long totalRead = existingSize;
                int bytesRead;
                var lastUpdate = DateTime.Now;
                long lastBytes = existingSize;
                double speed = 0;

                while ((bytesRead = await stream.ReadAsync(buffer, 0, buffer.Length, ct)) > 0)
                {
                    await fileStream.WriteAsync(buffer, 0, bytesRead, ct);
                    totalRead += bytesRead;

                    // Update progress every 500ms
                    var now = DateTime.Now;
                    if ((now - lastUpdate).TotalMilliseconds >= 500)
                    {
                        double elapsed = (now - lastUpdate).TotalSeconds;
                        speed = (totalRead - lastBytes) / elapsed;
                        lastBytes = totalRead;
                        lastUpdate = now;

                        double percent = expectedSize > 0 ? (double)totalRead / expectedSize * 100 : 0;
                        string eta = speed > 0 ? FormatTime((expectedSize - totalRead) / speed) : "??";

                        Console.Write($"\r  Progress: {percent:F1}% ({FormatSize(totalRead)}/{FormatSize(expectedSize)}) - {FormatSize((long)speed)}/s - ETA: {eta}   ");
                    }
                }

                Console.WriteLine(); // New line after progress

                // Rename temp file to final
                if (File.Exists(outputPath))
                {
                    File.Delete(outputPath);
                }
                File.Move(tempPath, outputPath);

                return true;
            }
            catch (OperationCanceledException)
            {
                Console.WriteLine("\nDownload cancelled (partial file saved for resume)");
                return false;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"\nDownload error: {ex.Message}");
                return false;
            }
        }

        private static string FormatSize(long bytes)
        {
            string[] sizes = { "B", "KB", "MB", "GB", "TB" };
            double len = bytes;
            int order = 0;
            while (len >= 1024 && order < sizes.Length - 1)
            {
                order++;
                len /= 1024;
            }
            return $"{len:0.##} {sizes[order]}";
        }

        private static string FormatTime(double seconds)
        {
            if (seconds < 60) return $"{seconds:F0}s";
            if (seconds < 3600) return $"{seconds / 60:F0}m {seconds % 60:F0}s";
            return $"{seconds / 3600:F0}h {(seconds % 3600) / 60:F0}m";
        }
    }
}
