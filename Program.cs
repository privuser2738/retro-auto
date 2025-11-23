using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
#if !CROSS_PLATFORM
using System.Windows.Forms;
#endif

namespace RetroAuto
{
    class Program
    {
        private const string DEFAULT_ROM_DIR = @"C:\Users\rob\Games\ATARI2600";
        private const string DEFAULT_RETROARCH = @"C:\RetroArch-Win64\retroarch.exe";
        private const string CORE_NAME = "stella2014";

        // Game Boy / Mesen settings
        private const string DEFAULT_GAMEBOY_DIR = @"C:\Users\rob\Games\Gameboy";
        private const string DEFAULT_MESEN = @"C:\Users\rob\Games\Apps\Mesen\Mesen.exe";

        // N64 / Ares settings
        private const string DEFAULT_N64_DIR = @"C:\Users\rob\Games\N64";
        private const string DEFAULT_N64_EMULATOR = @"C:\Users\rob\Games\Apps\Ares\ares.exe";

        [STAThread]
        static async Task<int> Main(string[] args)
        {
            // Parse display options (--monitor=X, --maximized, --fullscreen)
            DisplayOptions.Current = DisplayOptions.Parse(args);
            if (DisplayOptions.Current.Monitor.HasValue || DisplayOptions.Current.Maximized || DisplayOptions.Current.Fullscreen)
            {
                Console.WriteLine($"Display options: {DisplayOptions.Current}");
            }

            // Check for --monitors flag to list displays
            if (args.Any(a => a.Equals("--monitors", StringComparison.OrdinalIgnoreCase) ||
                             a.Equals("--list-monitors", StringComparison.OrdinalIgnoreCase)))
            {
                WindowManager.PrintMonitors();
                if (args.Length == 1) return 0;  // Only listing monitors, exit
            }

            // Check for special modes
            string? mode = args.FirstOrDefault(a => !a.StartsWith("-"))?.ToLower();

            switch (mode)
            {
                case "all":
                    return await RunAllSystemsMode(args);
                case "gameboy":
                case "gb":
                    return await RunGameBoyMode(args);
                case "n64":
                    return await RunN64Mode(args);
                case "nes":
                    return await RunNESMode(args);
                case "snes":
                    return await RunSNESMode(args);
                case "amiga":
                    return await RunAmigaMode(args);
                case "genesis":
                case "megadrive":
                case "md":
                    return await RunGenesisMode(args);
                case "saturn":
                    return await RunSaturnMode(args);
                case "ps1":
                case "psx":
                    return await RunPS1Mode(args);
                case "ps2":
                    return await RunPS2Mode(args);
                case "ps3":
                    return await RunPS3Mode(args);
                case "xbox360":
                case "x360":
                    return await RunXbox360Mode(args);
                case "stream-psx":
                case "streampsx":
                    return await RunStreamingPSXMode(args);
                case "stream-ps2":
                case "streamps2":
                    return await RunStreamingPS2Mode(args);
                case "stream-xbox":
                case "streamxbox":
                    return await RunStreamingXboxMode(args);
                case "stream-xbox360":
                case "stream-x360":
                case "streamxbox360":
                    return await RunStreamingXbox360Mode(args);
                case "stream-saturn":
                case "streamsaturn":
                    return await RunStreamingSaturnMode(args);
            }

            // Also check with -- prefix
            if (args.Any(a => a.Equals("--all", StringComparison.OrdinalIgnoreCase)))
                return await RunAllSystemsMode(args);
            if (args.Any(a => a.Equals("--gameboy", StringComparison.OrdinalIgnoreCase)))
                return await RunGameBoyMode(args);
            if (args.Any(a => a.Equals("--n64", StringComparison.OrdinalIgnoreCase)))
                return await RunN64Mode(args);
            if (args.Any(a => a.Equals("--nes", StringComparison.OrdinalIgnoreCase)))
                return await RunNESMode(args);
            if (args.Any(a => a.Equals("--snes", StringComparison.OrdinalIgnoreCase)))
                return await RunSNESMode(args);
            if (args.Any(a => a.Equals("--amiga", StringComparison.OrdinalIgnoreCase)))
                return await RunAmigaMode(args);
            if (args.Any(a => a.Equals("--genesis", StringComparison.OrdinalIgnoreCase) || a.Equals("--megadrive", StringComparison.OrdinalIgnoreCase)))
                return await RunGenesisMode(args);
            if (args.Any(a => a.Equals("--ps1", StringComparison.OrdinalIgnoreCase) || a.Equals("--psx", StringComparison.OrdinalIgnoreCase)))
                return await RunPS1Mode(args);
            if (args.Any(a => a.Equals("--ps2", StringComparison.OrdinalIgnoreCase)))
                return await RunPS2Mode(args);
            if (args.Any(a => a.Equals("--ps3", StringComparison.OrdinalIgnoreCase)))
                return await RunPS3Mode(args);
            if (args.Any(a => a.Equals("--xbox360", StringComparison.OrdinalIgnoreCase) || a.Equals("--x360", StringComparison.OrdinalIgnoreCase)))
                return await RunXbox360Mode(args);

            Console.WriteLine("=== RetroAuto - RetroArch Playlist Automation ===\n");

            try
            {
                // Parse command line arguments
                string command = "continue";  // Default behavior
                string romDir = DEFAULT_ROM_DIR;
                int minSeconds = 20;  // Default minimum play time
                int maxSeconds = 60;  // Default maximum play time

                for (int i = 0; i < args.Length; i++)
                {
                    var arg = args[i].ToLower();

                    if (arg == "play" || arg == "continue" || arg == "--continue")
                    {
                        command = "continue";
                    }
                    else if (arg == "reset" || arg == "--reset")
                    {
                        command = "reset";
                    }
                    else if (arg == "restart" || arg == "--restart")
                    {
                        command = "restart";
                    }
                    else if (arg == "status" || arg == "--status")
                    {
                        command = "status";
                    }
                    else if (arg == "help" || arg == "--help" || arg == "-h")
                    {
                        ShowUsage();
                        return 0;
                    }
                    else if (arg == "--min-max" && i + 1 < args.Length)
                    {
                        // Parse min,max format (e.g., "20,60" or "10,30")
                        var parts = args[++i].Split(',');
                        if (parts.Length == 2 &&
                            int.TryParse(parts[0], out int min) &&
                            int.TryParse(parts[1], out int max))
                        {
                            minSeconds = Math.Max(1, min);  // Minimum 1 second
                            maxSeconds = Math.Max(minSeconds + 1, max);  // Max must be > min
                            Console.WriteLine($"Play duration set to {minSeconds}-{maxSeconds} seconds");
                        }
                        else
                        {
                            Console.WriteLine($"Warning: Invalid --min-max format. Use: --min-max 20,60");
                        }
                    }
                    else if (!arg.StartsWith("-"))
                    {
                        // Treat as ROM directory
                        romDir = args[i];
                    }
                }

                // Validate ROM directory
                if (!Directory.Exists(romDir))
                {
                    Console.WriteLine($"Error: ROM directory not found: {romDir}");
                    ShowUsage();
                    return 1;
                }

                var playlist = new GamePlaylist(romDir);

                switch (command)
                {
                    case "continue":
                        await PlayGames(playlist, minSeconds, maxSeconds);
                        break;

                    case "reset":
                        playlist.ResetPlaylist();
                        Console.WriteLine("Playlist has been reset and randomized!");
                        Console.WriteLine("Starting playback with new random order...\n");
                        await PlayGames(playlist, minSeconds, maxSeconds);
                        break;

                    case "restart":
                        playlist.RestartPlaylist();
                        Console.WriteLine("Restarting playlist from beginning (same order)...\n");
                        await PlayGames(playlist, minSeconds, maxSeconds);
                        break;

                    case "status":
                        playlist.Initialize();
                        ShowStatus(playlist);
                        break;

                    default:
                        Console.WriteLine($"Unknown command: {command}");
                        ShowUsage();
                        return 1;
                }

                return 0;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"\nError: {ex.Message}");
                Console.WriteLine(ex.StackTrace);
                return 1;
            }
        }

        static async Task PlayGames(GamePlaylist playlist, int minSeconds = 20, int maxSeconds = 60)
        {
            playlist.Initialize();

            if (playlist.RemainingGames == 0)
            {
                Console.WriteLine("\n=== PLAYLIST COMPLETED! ===");
                Console.WriteLine($"You've played all {playlist.TotalGames} games!");
                Console.WriteLine("\nRun 'RetroAuto.exe reset' to create a new randomized playlist.");
                return;
            }

            Console.WriteLine($"\nStarting playback...");
            Console.WriteLine($"Progress: {playlist.PlayedGames}/{playlist.TotalGames} games completed");
            Console.WriteLine($"Remaining: {playlist.RemainingGames} games\n");

            var launcher = new RetroArchLauncher(
                DEFAULT_RETROARCH,
                CORE_NAME,
                playlist.RomDirectory,  // Save window config in ROM directory
                enableWindowMemory: true
            );
            var random = new Random();
            var cts = new CancellationTokenSource();

            // Handle Ctrl+C gracefully
            Console.CancelKeyPress += (s, e) =>
            {
                e.Cancel = true;
                Console.WriteLine("\n\nStopping playback...");
                cts.Cancel();
            };

            try
            {
                while (playlist.RemainingGames > 0 && !cts.Token.IsCancellationRequested)
                {
                    var gamePath = playlist.GetNextGame();
                    if (gamePath == null) break;

                    var gameNumber = playlist.PlayedGames + 1;
                    var totalGames = playlist.TotalGames;

                    Console.WriteLine($"\n[{gameNumber}/{totalGames}] Preparing: {Path.GetFileName(gamePath)}");

                    // Show popup briefly
#if CROSS_PLATFORM
                    await TitlePopupConsole.ShowBrieflyAsync(gamePath, gameNumber, totalGames);
#else
                    await TitlePopup.ShowBrieflyAsync(gamePath, gameNumber, totalGames);
#endif

                    // Random play duration between min-max seconds
                    int playSeconds = random.Next(minSeconds, maxSeconds + 1);

                    // Launch and play the game
                    bool success = await launcher.LaunchGameAsync(gamePath, playSeconds, cts.Token);

                    if (success)
                    {
                        playlist.MarkGamePlayed();
                        Console.WriteLine($"Completed! ({playlist.RemainingGames} games remaining)\n");
                    }
                    else
                    {
                        Console.WriteLine("Failed to launch game, skipping...\n");
                        playlist.MarkGamePlayed(); // Skip failed games
                    }

                    // Small delay between games
                    if (playlist.RemainingGames > 0 && !cts.Token.IsCancellationRequested)
                    {
                        await Task.Delay(2000, cts.Token);
                    }
                }

                if (playlist.RemainingGames == 0)
                {
                    Console.WriteLine("\n=== PLAYLIST COMPLETED! ===");
                    Console.WriteLine($"All {playlist.TotalGames} games have been played!");
                    Console.WriteLine("\nRun 'RetroAuto.exe reset' to create a new randomized playlist.");
                }
                else if (cts.Token.IsCancellationRequested)
                {
                    Console.WriteLine("\n=== PLAYBACK STOPPED ===");
                    Console.WriteLine($"Progress saved: {playlist.PlayedGames}/{playlist.TotalGames} games completed");
                    Console.WriteLine("Run 'RetroAuto.exe continue' to resume where you left off.");
                }
            }
            finally
            {
                launcher.Cleanup();
            }
        }

        static void ShowStatus(GamePlaylist playlist)
        {
            Console.WriteLine($"\n=== PLAYLIST STATUS ===");
            Console.WriteLine($"Total games: {playlist.TotalGames}");
            Console.WriteLine($"Played: {playlist.PlayedGames}");
            Console.WriteLine($"Remaining: {playlist.RemainingGames}");

            if (playlist.RemainingGames > 0)
            {
                var nextGame = playlist.GetNextGame();
                if (nextGame != null)
                {
                    Console.WriteLine($"\nNext game: {Path.GetFileName(nextGame)}");
                }
            }
            else
            {
                Console.WriteLine("\nPlaylist completed!");
            }
        }

        static void ShowUsage()
        {
            Console.WriteLine(@"
Usage: RetroAuto.exe [system/command] [options]

=== SYSTEM MODES (Interactive) ===
  all                  Random games from ALL available systems
  gameboy, gb          Game Boy / GBC / GBA (Mesen)
  n64                  Nintendo 64 (Ares)
  snes                 Super Nintendo (BSNES)
  genesis, md          Sega Genesis / Mega Drive (Ares)
  saturn               Sega Saturn (YabaSanshiro)
  ps1, psx             PlayStation 1 (DuckStation)
  ps2                  PlayStation 2 (PCSX2)
  ps3                  PlayStation 3 (RPCS3)
  xbox360, x360        Xbox 360 (Xenia)

=== STREAMING MODES ===
  stream-psx           Stream PSX games from archive.org (DuckStation)
  stream-ps2           Stream PS2 games from archive.org (PCSX2)
  stream-xbox          Stream Xbox games from archive.org (Xemu)
  stream-xbox360       Stream Xbox 360 games from archive.org (Xenia)
  stream-saturn        Stream Sega Saturn games from archive.org (YabaSanshiro)

  Streaming Options:
    -l, --locale XX    Filter by locale: en (English/USA), jp (Japanese), eu (European), * (all)
    --reset            Reset playlist with NEW random order
    --reset-progress   Restart from beginning (keep same order)

=== ATARI 2600 MODE (Auto-play) ===
Commands:
  continue, --continue  Continue playing from where you left off (default)
  restart, --restart    Restart playlist from beginning (same order)
  reset, --reset        Reset playlist with NEW random order
  status, --status      Show playlist status and progress
  help, --help, -h      Show this help message

Options:
  --min-max X,Y        Set random play duration range in seconds (default: 20,60)

=== EXAMPLES ===
  RetroAuto.exe all                  # Random game from any system
  RetroAuto.exe ps1                  # Interactive PS1 player
  RetroAuto.exe snes                 # Interactive SNES player
  RetroAuto.exe n64                  # Interactive N64 player
  RetroAuto.exe                      # Continue Atari 2600 playlist
  RetroAuto.exe --reset              # Reset Atari playlist
  RetroAuto.exe --min-max 10,30      # Atari: 10-30 second play time

=== CONFIGURED SYSTEMS ===
  Atari 2600   C:\Users\rob\Games\ATARI2600   (RetroArch + stella2014)
  Game Boy     C:\Users\rob\Games\GameBoy     (Mesen)
  N64          C:\Users\rob\Games\N64         (Ares)
  SNES         C:\Users\rob\Games\SNES        (BSNES)
  Genesis      C:\Users\rob\Games\Genesis     (Ares)
  Saturn       C:\Users\rob\Games\Saturn      (YabaSanshiro)
  PS1          C:\Users\rob\Games\PS1         (DuckStation)
  PS2          C:\Users\rob\Games\PS2         (PCSX2)
  PS3          C:\Users\rob\Games\PS3         (RPCS3)
  Xbox 360     C:\Users\rob\Games\Xbox360     (Xenia)

Press Ctrl+C during playback to stop. Use arrow keys for menu navigation.
");
        }

        static async Task<int> RunGameBoyMode(string[] args)
        {
            Console.WriteLine("=== RetroAuto - Game Boy Mode (Mesen) ===\n");

            try
            {
                // Parse optional ROM directory argument
                string romDir = DEFAULT_GAMEBOY_DIR;
                string mesenPath = DEFAULT_MESEN;

                for (int i = 0; i < args.Length; i++)
                {
                    var arg = args[i].ToLower();

                    if (arg == "--mesen" && i + 1 < args.Length)
                    {
                        mesenPath = args[++i];
                    }
                    else if (!arg.StartsWith("-") &&
                             !arg.Equals("gameboy", StringComparison.OrdinalIgnoreCase) &&
                             !arg.Equals("gb", StringComparison.OrdinalIgnoreCase))
                    {
                        romDir = args[i];
                    }
                }

                using var player = new GameBoyPlayer(mesenPath, romDir);
                await player.RunInteractive();

                return 0;
            }
            catch (InvalidOperationException ex)
            {
                Console.WriteLine($"\nSetup Error: {ex.Message}");
                Console.WriteLine("\nPlease check your paths and try again.");
                return 1;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"\nError: {ex.Message}");
                Console.WriteLine(ex.StackTrace);
                return 1;
            }
        }

        static async Task<int> RunN64Mode(string[] args)
        {
            Console.WriteLine("=== RetroAuto - N64 Mode (Ares) ===\n");

            try
            {
                // Parse optional ROM directory argument
                string romDir = DEFAULT_N64_DIR;
                string emulatorPath = DEFAULT_N64_EMULATOR;

                for (int i = 0; i < args.Length; i++)
                {
                    var arg = args[i].ToLower();

                    if (arg == "--emu" && i + 1 < args.Length)
                    {
                        emulatorPath = args[++i];
                    }
                    else if (!arg.StartsWith("-") &&
                             !arg.Equals("n64", StringComparison.OrdinalIgnoreCase))
                    {
                        romDir = args[i];
                    }
                }

                using var player = new N64Player(emulatorPath, romDir);
                await player.RunInteractive();

                return 0;
            }
            catch (InvalidOperationException ex)
            {
                Console.WriteLine($"\nSetup Error: {ex.Message}");
                Console.WriteLine("\nPlease check your paths and try again.");
                return 1;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"\nError: {ex.Message}");
                Console.WriteLine(ex.StackTrace);
                return 1;
            }
        }

        static async Task<int> RunNESMode(string[] args)
        {
            Console.WriteLine("=== RetroAuto - NES Mode (Ares) ===\n");

            try
            {
                using var player = new NESPlayer();
                await player.RunInteractive();
                return 0;
            }
            catch (InvalidOperationException ex)
            {
                Console.WriteLine($"\nSetup Error: {ex.Message}");
                Console.WriteLine("\nPlease check your paths and try again.");
                return 1;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"\nError: {ex.Message}");
                Console.WriteLine(ex.StackTrace);
                return 1;
            }
        }

        static async Task<int> RunSNESMode(string[] args)
        {
            Console.WriteLine("=== RetroAuto - SNES Mode (Ares) ===\n");

            try
            {
                using var player = new SNESPlayer();
                await player.RunInteractive();
                return 0;
            }
            catch (InvalidOperationException ex)
            {
                Console.WriteLine($"\nSetup Error: {ex.Message}");
                Console.WriteLine("\nPlease check your paths and try again.");
                return 1;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"\nError: {ex.Message}");
                Console.WriteLine(ex.StackTrace);
                return 1;
            }
        }

        static async Task<int> RunAmigaMode(string[] args)
        {
            Console.WriteLine("=== RetroAuto - Amiga Mode (FS-UAE) ===\n");

            try
            {
                using var player = new AmigaPlayer();
                await player.RunInteractive();
                return 0;
            }
            catch (InvalidOperationException ex)
            {
                Console.WriteLine($"\nSetup Error: {ex.Message}");
                Console.WriteLine("\nPlease check your paths and try again.");
                return 1;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"\nError: {ex.Message}");
                Console.WriteLine(ex.StackTrace);
                return 1;
            }
        }

        static async Task<int> RunGenesisMode(string[] args)
        {
            Console.WriteLine("=== RetroAuto - Sega Genesis Mode (Ares) ===\n");

            try
            {
                using var player = new GenesisPlayer();
                await player.RunInteractive();
                return 0;
            }
            catch (InvalidOperationException ex)
            {
                Console.WriteLine($"\nSetup Error: {ex.Message}");
                Console.WriteLine("\nPlease check your paths and try again.");
                return 1;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"\nError: {ex.Message}");
                Console.WriteLine(ex.StackTrace);
                return 1;
            }
        }

        static async Task<int> RunPS1Mode(string[] args)
        {
            Console.WriteLine("=== RetroAuto - PlayStation 1 Mode (DuckStation) ===\n");

            try
            {
                using var player = new PS1Player();
                await player.RunInteractive();
                return 0;
            }
            catch (InvalidOperationException ex)
            {
                Console.WriteLine($"\nSetup Error: {ex.Message}");
                Console.WriteLine("\nPlease check your paths and try again.");
                return 1;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"\nError: {ex.Message}");
                Console.WriteLine(ex.StackTrace);
                return 1;
            }
        }

        static async Task<int> RunPS2Mode(string[] args)
        {
            Console.WriteLine("=== RetroAuto - PlayStation 2 Mode (PCSX2) ===\n");

            try
            {
                using var player = new PS2Player();
                await player.RunInteractive();
                return 0;
            }
            catch (InvalidOperationException ex)
            {
                Console.WriteLine($"\nSetup Error: {ex.Message}");
                Console.WriteLine("\nPlease check your paths and try again.");
                return 1;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"\nError: {ex.Message}");
                Console.WriteLine(ex.StackTrace);
                return 1;
            }
        }

        static async Task<int> RunPS3Mode(string[] args)
        {
            Console.WriteLine("=== RetroAuto - PlayStation 3 Mode (RPCS3) ===\n");

            try
            {
                using var player = new PS3Player();
                await player.RunInteractive();
                return 0;
            }
            catch (InvalidOperationException ex)
            {
                Console.WriteLine($"\nSetup Error: {ex.Message}");
                Console.WriteLine("\nPlease check your paths and try again.");
                return 1;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"\nError: {ex.Message}");
                Console.WriteLine(ex.StackTrace);
                return 1;
            }
        }

        static async Task<int> RunXbox360Mode(string[] args)
        {
            Console.WriteLine("=== RetroAuto - Xbox 360 Mode (Xenia) ===\n");

            try
            {
                using var player = new Xbox360Player();
                await player.RunInteractive();
                return 0;
            }
            catch (InvalidOperationException ex)
            {
                Console.WriteLine($"\nSetup Error: {ex.Message}");
                Console.WriteLine("\nPlease check your paths and try again.");
                return 1;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"\nError: {ex.Message}");
                Console.WriteLine(ex.StackTrace);
                return 1;
            }
        }

        static async Task<int> RunAllSystemsMode(string[] args)
        {
            Console.WriteLine("=== RetroAuto - All Systems Mode ===\n");

            try
            {
                using var player = new AllSystemsPlayer();
                await player.RunInteractive();
                return 0;
            }
            catch (InvalidOperationException ex)
            {
                Console.WriteLine($"\nSetup Error: {ex.Message}");
                Console.WriteLine("\nPlease check your paths and try again.");
                return 1;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"\nError: {ex.Message}");
                Console.WriteLine(ex.StackTrace);
                return 1;
            }
        }

        static async Task<int> RunSaturnMode(string[] args)
        {
            Console.WriteLine("=== RetroAuto - Sega Saturn Mode (RetroArch + Beetle Saturn) ===\n");

            try
            {
                using var player = new SaturnPlayer();
                await player.RunInteractive();
                return 0;
            }
            catch (InvalidOperationException ex)
            {
                Console.WriteLine($"\nSetup Error: {ex.Message}");
                Console.WriteLine("\nPlease check your paths and try again.");
                return 1;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"\nError: {ex.Message}");
                Console.WriteLine(ex.StackTrace);
                return 1;
            }
        }

        static async Task<int> RunStreamingPSXMode(string[] args)
        {
            try
            {
                string? archiveUrl = GetStreamingArchiveUrl(args);
                string? locale = GameLocale.ParseLocaleFromArgs(args);
                var (forceReset, resetProgressOnly) = ParseResetFlags(args);

                using var player = new StreamingPSXPlayer(archiveUrl, locale: locale, forceReset: forceReset, resetProgressOnly: resetProgressOnly);
                await player.RunAsync();
                return 0;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"\nError: {ex.Message}");
                Console.WriteLine(ex.StackTrace);
                return 1;
            }
        }

        static async Task<int> RunStreamingPS2Mode(string[] args)
        {
            try
            {
                string? archiveUrl = GetStreamingArchiveUrl(args);
                string? locale = GameLocale.ParseLocaleFromArgs(args);
                var (forceReset, resetProgressOnly) = ParseResetFlags(args);

                using var player = new StreamingPS2Player(archiveUrl, locale: locale, forceReset: forceReset, resetProgressOnly: resetProgressOnly);
                await player.RunAsync();
                return 0;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"\nError: {ex.Message}");
                Console.WriteLine(ex.StackTrace);
                return 1;
            }
        }

        static async Task<int> RunStreamingXboxMode(string[] args)
        {
            try
            {
                string? archiveUrl = GetStreamingArchiveUrl(args);
                string? locale = GameLocale.ParseLocaleFromArgs(args);
                var (forceReset, resetProgressOnly) = ParseResetFlags(args);

                using var player = new StreamingXboxPlayer(archiveUrl, locale: locale, forceReset: forceReset, resetProgressOnly: resetProgressOnly);
                await player.RunAsync();
                return 0;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"\nError: {ex.Message}");
                Console.WriteLine(ex.StackTrace);
                return 1;
            }
        }

        static async Task<int> RunStreamingXbox360Mode(string[] args)
        {
            try
            {
                string? archiveUrl = GetStreamingArchiveUrl(args);
                string? locale = GameLocale.ParseLocaleFromArgs(args);
                var (forceReset, resetProgressOnly) = ParseResetFlags(args);

                using var player = new StreamingXbox360Player(archiveUrl, locale: locale, forceReset: forceReset, resetProgressOnly: resetProgressOnly);
                await player.RunAsync();
                return 0;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"\nError: {ex.Message}");
                Console.WriteLine(ex.StackTrace);
                return 1;
            }
        }

        static async Task<int> RunStreamingSaturnMode(string[] args)
        {
            try
            {
                string? archiveUrl = GetStreamingArchiveUrl(args);
                string? locale = GameLocale.ParseLocaleFromArgs(args);
                var (forceReset, resetProgressOnly) = ParseResetFlags(args);

                using var player = new StreamingSaturnPlayer(archiveUrl, locale: locale, forceReset: forceReset, resetProgressOnly: resetProgressOnly);
                await player.RunAsync();
                return 0;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"\nError: {ex.Message}");
                Console.WriteLine(ex.StackTrace);
                return 1;
            }
        }

        /// <summary>
        /// Helper to extract archive URL from streaming command args (skips flags like -l, --locale, --reset)
        /// </summary>
        private static string? GetStreamingArchiveUrl(string[] args)
        {
            for (int i = 1; i < args.Length; i++)
            {
                var arg = args[i];
                // Skip flag arguments
                if (arg.StartsWith("-"))
                {
                    // Skip the next arg if this is -l or --locale (value follows)
                    if (arg == "-l" || arg == "--locale")
                        i++;
                    continue;
                }
                // First non-flag argument is the archive URL
                if (arg.StartsWith("http"))
                    return arg;
            }
            return null;
        }

        /// <summary>
        /// Parse reset flags from command line args
        /// --reset: Full reset with new random order
        /// --reset-progress: Reset progress but keep same order
        /// </summary>
        private static (bool forceReset, bool resetProgressOnly) ParseResetFlags(string[] args)
        {
            bool forceReset = args.Any(a => a.Equals("--reset", StringComparison.OrdinalIgnoreCase));
            bool resetProgressOnly = args.Any(a => a.Equals("--reset-progress", StringComparison.OrdinalIgnoreCase));
            return (forceReset, resetProgressOnly);
        }
    }
}
