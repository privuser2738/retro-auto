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
    /// Represents a game from any system
    /// </summary>
    public class SystemGame
    {
        public string RomPath { get; set; } = string.Empty;
        public string SystemName { get; set; } = string.Empty;
        public string EmulatorPath { get; set; } = string.Empty;
        public string RomDirectory { get; set; } = string.Empty;
        public ConsoleColor ThemeColor { get; set; } = ConsoleColor.White;

        public string DisplayName => Path.GetFileNameWithoutExtension(RomPath);
        public string FileName => Path.GetFileName(RomPath);

        /// <summary>
        /// Path to window config file for this system's emulator
        /// </summary>
        public string WindowConfigPath => Path.Combine(RomDirectory,
            $"{Path.GetFileNameWithoutExtension(EmulatorPath).ToLower()}_window.json");
    }

    /// <summary>
    /// System configuration for the all-systems randomizer
    /// </summary>
    public class SystemConfig
    {
        public string Name { get; set; } = string.Empty;
        public string EmulatorPath { get; set; } = string.Empty;
        public string RomDirectory { get; set; } = string.Empty;
        public string[] Extensions { get; set; } = Array.Empty<string>();
        public ConsoleColor Color { get; set; } = ConsoleColor.White;
        public bool RecursiveScan { get; set; } = false;  // Scan subdirectories for ROMs
        public bool IsAvailable => File.Exists(EmulatorPath) && Directory.Exists(RomDirectory);
    }

    /// <summary>
    /// Multi-system randomizer that can play games from any configured system
    /// </summary>
    public class AllSystemsPlayer : IDisposable
    {
        private static readonly List<SystemConfig> ALL_SYSTEMS = new()
        {
            new SystemConfig
            {
                Name = "Game Boy",
                EmulatorPath = @"C:\Users\rob\Games\Mesen\Mesen.exe",
                RomDirectory = @"C:\Users\rob\Games\GameBoy",
                Extensions = new[] { "*.gb", "*.gbc", "*.gba" },
                Color = ConsoleColor.Cyan
            },
            new SystemConfig
            {
                Name = "NES",
                EmulatorPath = @"C:\Users\rob\Games\ares\ares-v146\ares.exe",
                RomDirectory = @"C:\Users\rob\Games\NES",
                Extensions = new[] { "*.nes", "*.zip" },
                Color = ConsoleColor.DarkYellow,
                RecursiveScan = true  // NES ROMs are in subdirectories
            },
            new SystemConfig
            {
                Name = "Amiga",
                EmulatorPath = @"C:\Users\rob\AppData\Local\Programs\FS-UAE\Launcher\Windows\x86-64\fs-uae-launcher.exe",
                RomDirectory = @"C:\Users\rob\Games\Amiga",
                Extensions = new[] { "*.adf", "*.ipf", "*.dms", "*.adz", "*.zip", "*.lha", "*.hdf" },
                Color = ConsoleColor.DarkCyan,
                RecursiveScan = true  // Amiga games may be in subdirectories
            },
            new SystemConfig
            {
                Name = "N64",
                EmulatorPath = @"C:\Users\rob\Games\N64\Project64\Project64.exe",
                RomDirectory = @"C:\Users\rob\Games\N64",
                Extensions = new[] { "*.n64", "*.z64", "*.v64" },
                Color = ConsoleColor.Red
            },
            new SystemConfig
            {
                Name = "SNES",
                EmulatorPath = @"C:\Users\rob\Games\ares\ares-v146\ares.exe",
                RomDirectory = @"C:\Users\rob\Games\SNES",
                Extensions = new[] { "*.sfc", "*.smc", "*.zip" },
                Color = ConsoleColor.Magenta
            },
            new SystemConfig
            {
                Name = "Sega Genesis",
                EmulatorPath = @"C:\Users\rob\Games\ares\ares-v146\ares.exe",
                RomDirectory = @"C:\Users\rob\Games\Genesis",
                Extensions = new[] { "*.zip", "*.bin", "*.md", "*.gen", "*.smd" },
                Color = ConsoleColor.DarkRed
            },
            new SystemConfig
            {
                Name = "PlayStation 1",
                EmulatorPath = @"C:\Users\rob\Games\Duckstation\duckstation-qt-x64-ReleaseLTCG.exe",
                RomDirectory = @"C:\Users\rob\Games\PS1",
                Extensions = new[] { "*.bin", "*.cue", "*.iso", "*.chd" },
                Color = ConsoleColor.Blue
            },
            new SystemConfig
            {
                Name = "PlayStation 2",
                EmulatorPath = @"C:\Program Files\PCSX2\pcsx2-qt.exe",
                RomDirectory = @"C:\Users\rob\Games\PS2",
                Extensions = new[] { "*.iso", "*.chd", "*.cso" },
                Color = ConsoleColor.DarkBlue
            },
            new SystemConfig
            {
                Name = "PlayStation 3",
                EmulatorPath = @"C:\Users\rob\Games\RCPS3\rpcs3.exe",
                RomDirectory = @"C:\Users\rob\Games\PS3",
                Extensions = new[] { "*.iso" },
                Color = ConsoleColor.DarkMagenta
            },
            new SystemConfig
            {
                Name = "Xbox 360",
                EmulatorPath = @"C:\Users\rob\Games\Xenia\xenia.exe",
                RomDirectory = @"C:\Users\rob\Games\Xbox360",
                Extensions = new[] { "*.iso", "*.xex" },
                Color = ConsoleColor.Green
            }
        };

        private List<SystemGame> allGames = new();
        private List<SystemGame> remainingGames = new();
        private List<SystemConfig> availableSystems = new();
        private Random random = new();
        private Process? currentProcess;
        private CancellationTokenSource? cts;
        private bool isDisposed;
        private readonly object lockObject = new();

        public AllSystemsPlayer() { }

        /// <summary>
        /// Scans all available systems for games
        /// </summary>
        public void Initialize()
        {
            Console.WriteLine("Scanning all available systems...\n");

            availableSystems = ALL_SYSTEMS.Where(s => s.IsAvailable).ToList();

            if (availableSystems.Count == 0)
            {
                Console.WriteLine("No systems available! Check emulator and ROM paths.");
                return;
            }

            Console.WriteLine($"Found {availableSystems.Count} available systems:\n");

            foreach (var system in availableSystems)
            {
                Console.ForegroundColor = system.Color;
                Console.Write($"  {system.Name}");
                Console.ResetColor();

                var games = ScanSystemGames(system);
                Console.WriteLine($" - {games.Count} games");

                allGames.AddRange(games);
            }

            Console.WriteLine($"\nTotal: {allGames.Count} games across {availableSystems.Count} systems\n");

            if (allGames.Count > 0)
            {
                remainingGames = allGames.OrderBy(x => random.Next()).ToList();
            }
        }

        private List<SystemGame> ScanSystemGames(SystemConfig system)
        {
            var games = new List<SystemGame>();

            try
            {
                foreach (var ext in system.Extensions)
                {
                    var files = SafeGetFiles(system.RomDirectory, ext, system.Name);
                    foreach (var file in files)
                    {
                        games.Add(new SystemGame
                        {
                            RomPath = file,
                            SystemName = system.Name,
                            EmulatorPath = system.EmulatorPath,
                            RomDirectory = system.RomDirectory,
                            ThemeColor = system.Color
                        });
                    }
                }

                // Special handling for PS3 folder-based games
                if (system.Name == "PlayStation 3")
                {
                    foreach (var dir in Directory.GetDirectories(system.RomDirectory))
                    {
                        var ebootPaths = new[]
                        {
                            Path.Combine(dir, "PS3_GAME", "USRDIR", "EBOOT.BIN"),
                            Path.Combine(dir, "USRDIR", "EBOOT.BIN"),
                            Path.Combine(dir, "EBOOT.BIN")
                        };

                        foreach (var eboot in ebootPaths)
                        {
                            if (File.Exists(eboot))
                            {
                                games.Add(new SystemGame
                                {
                                    RomPath = eboot,
                                    SystemName = system.Name,
                                    EmulatorPath = system.EmulatorPath,
                                    RomDirectory = system.RomDirectory,
                                    ThemeColor = system.Color
                                });
                                break;
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"    Warning: Error scanning {system.Name}: {ex.Message}");
            }

            return games.Distinct().ToList();
        }

        private IEnumerable<string> SafeGetFiles(string directory, string pattern, string systemName)
        {
            try
            {
                var files = Directory.GetFiles(directory, pattern, SearchOption.AllDirectories);

                // Exclude emulator folders
                if (systemName == "NES")
                {
                    files = files.Where(f => !f.Contains("ares", StringComparison.OrdinalIgnoreCase)).ToArray();
                }
                if (systemName == "Amiga")
                {
                    files = files.Where(f => !f.Contains("FS-UAE", StringComparison.OrdinalIgnoreCase)
                                          && !f.Contains("Kickstart", StringComparison.OrdinalIgnoreCase)).ToArray();
                }
                if (systemName == "SNES")
                {
                    files = files.Where(f => !f.Contains("BSNES", StringComparison.OrdinalIgnoreCase)
                                          && !f.Contains("ares", StringComparison.OrdinalIgnoreCase)).ToArray();
                }
                if (systemName == "N64")
                {
                    files = files.Where(f => !f.Contains("Project64", StringComparison.OrdinalIgnoreCase)).ToArray();
                }

                return files;
            }
            catch
            {
                return Enumerable.Empty<string>();
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
                Console.WriteLine("No games found across any system!");
                Console.WriteLine("Press any key to exit...");
                Console.ReadKey(true);
                return;
            }

            cts = new CancellationTokenSource();

            Console.CancelKeyPress += (s, e) =>
            {
                e.Cancel = true;
                cts?.Cancel();
                SafeCleanupProcess();
            };

            SystemGame? currentGame = GetRandomGame();
            SystemGame? nextGame = PeekNextGame();
            int gamesPlayed = 0;

            try
            {
                while (!cts.Token.IsCancellationRequested)
                {
                    Console.Clear();

                    if (currentGame == null || !File.Exists(currentGame.RomPath))
                    {
                        currentGame = GetRandomGame();
                        nextGame = PeekNextGame();
                    }

                    if (currentGame == null)
                    {
                        Console.WriteLine("No valid games available!");
                        break;
                    }

                    ShowTitle(currentGame, gamesPlayed + 1);

                    string nextInfo = nextGame != null
                        ? $"{nextGame.SystemName}: {Truncate(nextGame.DisplayName, 30)}"
                        : "Random";

                    var options = new[]
                    {
                        $"1. Start - Play this game (Next: {nextInfo})",
                        "2. Skip  - Pick another random game",
                        "3. Exit  - Quit RetroAuto",
                        "4. Search - Find a specific game"
                    };

                    Console.WriteLine("What would you like to do?\n");
                    int choice = InteractiveMenu.ShowMenu("All Systems Menu", options);

                    switch (choice)
                    {
                        case 0: // Start
                            Console.Clear();
                            Console.ForegroundColor = currentGame.ThemeColor;
                            Console.WriteLine($"[{currentGame.SystemName}]");
                            Console.ResetColor();
                            Console.WriteLine($"Launching: {currentGame.DisplayName}");
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
                            // Create searchable list of display strings
                            var searchList = allGames.Select(g => $"[{g.SystemName}] {g.DisplayName}").ToList();
                            var selectedStr = InteractiveMenu.SearchAndSelect(
                                "Search for a game (any system)",
                                searchList,
                                s => s
                            );

                            if (selectedStr != null)
                            {
                                // Find the matching game
                                int idx = searchList.IndexOf(selectedStr);
                                if (idx >= 0 && idx < allGames.Count)
                                {
                                    currentGame = allGames[idx];
                                    nextGame = PeekNextGame();
                                }
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

        private void ShowTitle(SystemGame game, int gameNumber)
        {
            Console.Clear();
            Console.ForegroundColor = game.ThemeColor;
            Console.WriteLine("╔════════════════════════════════════════════════════════════════╗");
            Console.WriteLine($"║{CenterText($"{game.SystemName} - NOW PLAYING", 64)}║");
            Console.WriteLine("╚════════════════════════════════════════════════════════════════╝");
            Console.ResetColor();
            Console.WriteLine();

            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.WriteLine($"  Game {gameNumber} of {allGames.Count} (across all systems)");
            Console.ResetColor();

            Console.WriteLine();
            Console.ForegroundColor = ConsoleColor.White;
            Console.WriteLine($"  {game.DisplayName}");
            Console.ResetColor();
            Console.ForegroundColor = ConsoleColor.DarkGray;
            Console.WriteLine($"  {game.FileName}");
            Console.ResetColor();
            Console.WriteLine();
        }

        private static string CenterText(string text, int width)
        {
            if (text.Length >= width) return text.Substring(0, width);
            int padding = (width - text.Length) / 2;
            return text.PadLeft(padding + text.Length).PadRight(width);
        }

        private static string Truncate(string value, int maxLength)
        {
            if (string.IsNullOrEmpty(value)) return value ?? "";
            return value.Length <= maxLength ? value : value.Substring(0, maxLength - 3) + "...";
        }

        private SystemGame? GetRandomGame()
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

        private SystemGame? PeekNextGame()
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

        private async Task<bool> SafeLaunchGame(SystemGame game)
        {
            if (!File.Exists(game.RomPath))
            {
                Console.WriteLine($"ERROR: ROM file not found: {game.RomPath}");
                Console.WriteLine("Press any key to continue...");
                Console.ReadKey(true);
                return false;
            }

            await Task.Yield();

            try
            {
                string arguments = game.SystemName == "Atari 2600"
                    ? $"-L stella2014_libretro.dll \"{game.RomPath}\""
                    : $"\"{game.RomPath}\"";

#if !CROSS_PLATFORM
                // Load saved window position for this emulator
                WindowManager.WindowPosition? savedPosition = null;
                try
                {
                    savedPosition = WindowManager.LoadWindowPosition(game.WindowConfigPath);
                    if (savedPosition != null)
                    {
                        Console.WriteLine($"Window preferences: {savedPosition.Width}x{savedPosition.Height}" +
                                        (savedPosition.IsMaximized ? " [Maximized]" : ""));
                    }
                }
                catch { /* Ignore errors loading window config */ }
#endif

                var startInfo = new ProcessStartInfo
                {
                    FileName = game.EmulatorPath,
                    Arguments = arguments,
                    UseShellExecute = false,
                    CreateNoWindow = false
                };

                Console.WriteLine($"Command: \"{game.EmulatorPath}\" {arguments}");

                lock (lockObject)
                {
                    currentProcess = Process.Start(startInfo);
                }

                if (currentProcess == null)
                {
                    Console.WriteLine($"ERROR: Failed to start emulator");
                    Console.WriteLine("Press any key to continue...");
                    Console.ReadKey(true);
                    return false;
                }

                Console.WriteLine($"Emulator launched (PID: {currentProcess.Id})");

#if !CROSS_PLATFORM
                // Wait for window
                var hWnd = await WindowManager.WaitForProcessWindowAsync(currentProcess, 5000);
                if (hWnd != IntPtr.Zero)
                {
                    // Check if display options override saved position
                    var displayOpts = DisplayOptions.Current;
                    if (displayOpts.Monitor.HasValue || displayOpts.Maximized || displayOpts.Fullscreen)
                    {
                        // Determine fullscreen method based on emulator
                        var fsMethod = GetFullscreenMethodForEmulator(game.EmulatorPath);
                        displayOpts.ApplyToWindow(hWnd, fsMethod);
                        Console.WriteLine($"Applied display options: {displayOpts}");
                    }
                    else if (savedPosition != null)
                    {
                        if (WindowManager.SetWindowPosition(hWnd, savedPosition))
                        {
                            Console.WriteLine("Window position restored");
                        }
                    }
                }
#endif

                Console.WriteLine("\nPress any key to close the game and return to menu...");

                while (!Console.KeyAvailable && !currentProcess.HasExited)
                {
                    await Task.Delay(100);
                }

                if (Console.KeyAvailable)
                {
                    Console.ReadKey(true);
                }

#if !CROSS_PLATFORM
                // Save window position before closing
                if (!currentProcess.HasExited)
                {
                    try
                    {
                        currentProcess.Refresh();
                        if (currentProcess.MainWindowHandle != IntPtr.Zero)
                        {
                            var currentPosition = WindowManager.GetWindowPosition(currentProcess.MainWindowHandle);
                            if (currentPosition != null)
                            {
                                WindowManager.SaveWindowPosition(currentPosition, game.WindowConfigPath);
                                Console.WriteLine("Window position saved");
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"Warning: Could not save window position: {ex.Message}");
                    }
                }
#endif

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

        private void SafeCleanupProcess()
        {
            lock (lockObject)
            {
                if (currentProcess == null) return;

                try
                {
                    if (!currentProcess.HasExited)
                    {
                        Console.WriteLine("Closing emulator...");
                        currentProcess.CloseMainWindow();

                        if (!currentProcess.WaitForExit(3000))
                        {
                            try
                            {
                                currentProcess.Kill();
                                currentProcess.WaitForExit(1000);
                            }
                            catch { }
                        }
                    }
                }
                catch { }
                finally
                {
                    try { currentProcess.Dispose(); } catch { }
                    currentProcess = null;
                }
            }
        }

        /// <summary>
        /// Determines the fullscreen toggle method based on emulator
        /// </summary>
        private static WindowManager.FullscreenMethod GetFullscreenMethodForEmulator(string emulatorPath)
        {
            var name = Path.GetFileNameWithoutExtension(emulatorPath).ToLower();

            // Project64, PCSX2 use Alt+Enter
            if (name.Contains("project64") || name.Contains("pcsx2"))
                return WindowManager.FullscreenMethod.AltEnter;

            // FS-UAE uses F12 for fullscreen
            if (name.Contains("fs-uae"))
                return WindowManager.FullscreenMethod.F12;

            // Most emulators (Ares, DuckStation, Mesen, etc.) use F11
            return WindowManager.FullscreenMethod.F11;
        }

        public void Dispose()
        {
            if (isDisposed) return;
            SafeCleanupProcess();
            cts?.Cancel();
            cts?.Dispose();
            isDisposed = true;
            GC.SuppressFinalize(this);
        }

        ~AllSystemsPlayer()
        {
            Dispose();
        }
    }
}
