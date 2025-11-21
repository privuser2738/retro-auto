using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace RetroAuto
{
    /// <summary>
    /// Base class for interactive ROM players with safe resource management
    /// </summary>
    public abstract class BaseInteractivePlayer : IDisposable
    {
        protected readonly string emulatorPath;
        protected readonly string romDirectory;
        protected readonly string gamesListPath;
        protected readonly string systemName;
        protected readonly string[] romExtensions;
        protected readonly ConsoleColor themeColor;

        protected List<string> allGames;
        protected List<string> remainingGames;
        protected Random random;
        protected Process? currentProcess;
        protected CancellationTokenSource? cts;
        protected bool isDisposed;
        protected readonly object lockObject = new object();

        protected BaseInteractivePlayer(
            string emulatorPath,
            string romDirectory,
            string gamesListFile,
            string systemName,
            string[] romExtensions,
            ConsoleColor themeColor = ConsoleColor.Cyan)
        {
            this.emulatorPath = emulatorPath ?? throw new ArgumentNullException(nameof(emulatorPath));
            this.romDirectory = romDirectory ?? throw new ArgumentNullException(nameof(romDirectory));
            this.gamesListPath = Path.Combine(romDirectory, gamesListFile);
            this.systemName = systemName;
            this.romExtensions = romExtensions;
            this.themeColor = themeColor;

            this.allGames = new List<string>();
            this.remainingGames = new List<string>();
            this.random = new Random();

            ValidateSetup();
        }

        /// <summary>
        /// Validates emulator and ROM directory exist
        /// </summary>
        protected virtual void ValidateSetup()
        {
            var errors = new List<string>();

            if (!File.Exists(emulatorPath))
            {
                errors.Add($"Emulator not found: {emulatorPath}");
            }

            if (!Directory.Exists(romDirectory))
            {
                errors.Add($"ROM directory not found: {romDirectory}");
            }

            if (errors.Count > 0)
            {
                throw new InvalidOperationException(
                    $"Setup validation failed:\n" + string.Join("\n", errors.Select(e => $"  - {e}")));
            }
        }

        /// <summary>
        /// Scans for ROMs and initializes the playlist
        /// </summary>
        public virtual void Initialize()
        {
            Console.WriteLine($"Scanning for {systemName} ROMs...");

            try
            {
                allGames = romExtensions
                    .SelectMany(ext => SafeGetFiles(romDirectory, ext))
                    .Distinct()
                    .OrderBy(f => Path.GetFileNameWithoutExtension(f))
                    .ToList();

                Console.WriteLine($"Found {allGames.Count} games");

                if (allGames.Count > 0)
                {
                    SafeWriteGamesList();
                    remainingGames = allGames.OrderBy(x => random.Next()).ToList();
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Warning: Error during initialization: {ex.Message}");
                allGames = new List<string>();
                remainingGames = new List<string>();
            }
        }

        /// <summary>
        /// Safely gets files, handling access errors
        /// </summary>
        protected IEnumerable<string> SafeGetFiles(string directory, string pattern)
        {
            try
            {
                return Directory.GetFiles(directory, pattern, SearchOption.AllDirectories);
            }
            catch (UnauthorizedAccessException)
            {
                Console.WriteLine($"Warning: Access denied to some directories");
                return SafeGetFilesRecursive(directory, pattern);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Warning: Error scanning for {pattern}: {ex.Message}");
                return Enumerable.Empty<string>();
            }
        }

        /// <summary>
        /// Recursively gets files, skipping inaccessible directories
        /// </summary>
        protected IEnumerable<string> SafeGetFilesRecursive(string directory, string pattern)
        {
            var files = new List<string>();

            try
            {
                files.AddRange(Directory.GetFiles(directory, pattern));

                foreach (var subDir in Directory.GetDirectories(directory))
                {
                    try
                    {
                        files.AddRange(SafeGetFilesRecursive(subDir, pattern));
                    }
                    catch { /* Skip inaccessible directories */ }
                }
            }
            catch { /* Skip if directory itself is inaccessible */ }

            return files;
        }

        /// <summary>
        /// Safely writes the games list to file
        /// </summary>
        protected void SafeWriteGamesList()
        {
            try
            {
                File.WriteAllLines(gamesListPath, allGames);
                Console.WriteLine($"Games list saved to: {gamesListPath}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Warning: Could not save games list: {ex.Message}");
            }
        }

        /// <summary>
        /// Main interactive loop
        /// </summary>
        public async Task RunInteractive()
        {
            Initialize();

            if (allGames.Count == 0)
            {
                Console.WriteLine("No games found!");
                Console.WriteLine("Press any key to exit...");
                Console.ReadKey(true);
                return;
            }

            cts = new CancellationTokenSource();

            // Handle Ctrl+C gracefully
            Console.CancelKeyPress += (s, e) =>
            {
                e.Cancel = true;
                cts?.Cancel();
                SafeCleanupProcess();
            };

            string? currentGame = GetRandomGame();
            string? nextGame = PeekNextGame();
            int gamesPlayed = 0;

            try
            {
                while (!cts.Token.IsCancellationRequested)
                {
                    Console.Clear();

                    if (currentGame == null || !File.Exists(currentGame))
                    {
                        currentGame = GetRandomGame();
                        nextGame = PeekNextGame();
                    }

                    if (currentGame == null)
                    {
                        Console.WriteLine("No valid games available!");
                        break;
                    }

                    string gameTitle = Path.GetFileNameWithoutExtension(currentGame);
                    string fileName = Path.GetFileName(currentGame);
                    string nextTitle = nextGame != null ? Path.GetFileNameWithoutExtension(nextGame) : "Random";

                    ShowTitle(gameTitle, fileName, gamesPlayed + 1, allGames.Count);

                    Console.WriteLine("What would you like to do?\n");

                    var options = new[]
                    {
                        $"1. Start - Play this game (Next: {Truncate(nextTitle, 40)})",
                        "2. Skip  - Pick another random game",
                        "3. Exit  - Quit RetroAuto",
                        "4. Search - Find a specific game"
                    };

                    int choice = InteractiveMenu.ShowMenu($"{systemName} Game Menu", options);

                    switch (choice)
                    {
                        case 0: // Start
                            Console.Clear();
                            Console.WriteLine($"Launching: {gameTitle}");
                            Console.WriteLine("Press any key when done playing to return to menu...\n");

                            bool success = await SafeLaunchGame(currentGame);
                            if (success) gamesPlayed++;

                            currentGame = GetRandomGame();
                            nextGame = PeekNextGame();
                            break;

                        case 1: // Skip
                            currentGame = GetRandomGame();
                            nextGame = PeekNextGame();
                            break;

                        case 2: // Exit
                            Console.Clear();
                            Console.WriteLine($"Thanks for playing! Games played: {gamesPlayed}");
                            return;

                        case 3: // Search
                            var selectedGame = InteractiveMenu.SearchAndSelect(
                                "Search for a game",
                                allGames,
                                path => Path.GetFileNameWithoutExtension(path)
                            );

                            if (selectedGame != null && File.Exists(selectedGame))
                            {
                                currentGame = selectedGame;
                                nextGame = PeekNextGame();
                            }
                            break;
                    }
                }
            }
            catch (OperationCanceledException)
            {
                Console.WriteLine("\nOperation cancelled.");
            }
            finally
            {
                SafeCleanupProcess();
                Console.WriteLine($"\nSession ended. Games played: {gamesPlayed}");
            }
        }

        /// <summary>
        /// Displays the game title with system-specific theming
        /// </summary>
        protected virtual void ShowTitle(string title, string filename, int gameNumber, int totalGames)
        {
            Console.Clear();
            Console.ForegroundColor = themeColor;
            Console.WriteLine("╔════════════════════════════════════════════════════════════════╗");
            Console.WriteLine($"║{CenterText($"{systemName} - NOW PLAYING", 64)}║");
            Console.WriteLine("╚════════════════════════════════════════════════════════════════╝");
            Console.ResetColor();
            Console.WriteLine();

            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.WriteLine($"  Game {gameNumber} of {totalGames}");
            Console.ResetColor();

            Console.WriteLine();
            Console.ForegroundColor = ConsoleColor.White;
            Console.WriteLine($"  {title}");
            Console.ResetColor();
            Console.ForegroundColor = ConsoleColor.DarkGray;
            Console.WriteLine($"  {filename}");
            Console.ResetColor();
            Console.WriteLine();
        }

        protected static string CenterText(string text, int width)
        {
            if (text.Length >= width) return text.Substring(0, width);
            int padding = (width - text.Length) / 2;
            return text.PadLeft(padding + text.Length).PadRight(width);
        }

        protected string? PeekNextGame()
        {
            lock (lockObject)
            {
                if (remainingGames.Count == 0 && allGames.Count > 0)
                {
                    remainingGames = allGames.OrderBy(x => random.Next()).ToList();
                }
                return remainingGames.Count > 0 ? remainingGames[0] : null;
            }
        }

        protected static string Truncate(string value, int maxLength)
        {
            if (string.IsNullOrEmpty(value)) return value ?? "";
            return value.Length <= maxLength ? value : value.Substring(0, maxLength - 3) + "...";
        }

        protected string? GetRandomGame()
        {
            lock (lockObject)
            {
                if (remainingGames.Count == 0 && allGames.Count > 0)
                {
                    remainingGames = allGames.OrderBy(x => random.Next()).ToList();
                }

                if (remainingGames.Count == 0) return null;

                var game = remainingGames[0];
                remainingGames.RemoveAt(0);
                return game;
            }
        }

        /// <summary>
        /// Safely launches a game with proper error handling
        /// </summary>
        protected async Task<bool> SafeLaunchGame(string romPath)
        {
            if (!File.Exists(romPath))
            {
                Console.WriteLine($"ERROR: ROM file not found: {romPath}");
                Console.WriteLine("Press any key to continue...");
                Console.ReadKey(true);
                return false;
            }

            await Task.Yield();

            try
            {
                var startInfo = new ProcessStartInfo
                {
                    FileName = emulatorPath,
                    Arguments = GetLaunchArguments(romPath),
                    UseShellExecute = false,
                    CreateNoWindow = false
                };

                Console.WriteLine($"Command: \"{emulatorPath}\" {startInfo.Arguments}");

                lock (lockObject)
                {
                    currentProcess = Process.Start(startInfo);
                }

                if (currentProcess == null)
                {
                    Console.WriteLine($"ERROR: Failed to start {systemName} emulator");
                    Console.WriteLine("Press any key to continue...");
                    Console.ReadKey(true);
                    return false;
                }

                Console.WriteLine($"Emulator launched (PID: {currentProcess.Id})");
                Console.WriteLine("\nPress any key to close the game and return to menu...");

                // Wait for user input or process exit
                while (!Console.KeyAvailable && !currentProcess.HasExited)
                {
                    await Task.Delay(100);
                }

                if (Console.KeyAvailable)
                {
                    Console.ReadKey(true);
                }

                SafeCleanupProcess();
                return true;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error launching game: {ex.Message}");
                Console.WriteLine("Press any key to continue...");
                Console.ReadKey(true);
                SafeCleanupProcess();
                return false;
            }
        }

        /// <summary>
        /// Gets the command line arguments for launching a ROM
        /// Override in derived classes for custom arguments
        /// </summary>
        protected virtual string GetLaunchArguments(string romPath)
        {
            return $"\"{romPath}\"";
        }

        /// <summary>
        /// Safely cleans up the current process
        /// </summary>
        protected void SafeCleanupProcess()
        {
            lock (lockObject)
            {
                if (currentProcess == null) return;

                try
                {
                    if (!currentProcess.HasExited)
                    {
                        Console.WriteLine("Closing emulator...");

                        // Try graceful close first
                        currentProcess.CloseMainWindow();

                        if (!currentProcess.WaitForExit(3000))
                        {
                            // Force kill if graceful close failed
                            try
                            {
                                currentProcess.Kill();
                                currentProcess.WaitForExit(1000);
                            }
                            catch { /* Process may have exited */ }
                        }
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Warning: Error during cleanup: {ex.Message}");
                }
                finally
                {
                    try
                    {
                        currentProcess.Dispose();
                    }
                    catch { }
                    currentProcess = null;
                }
            }
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (isDisposed) return;

            if (disposing)
            {
                SafeCleanupProcess();
                cts?.Cancel();
                cts?.Dispose();
            }

            isDisposed = true;
        }

        ~BaseInteractivePlayer()
        {
            Dispose(false);
        }
    }
}
