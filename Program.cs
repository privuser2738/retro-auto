using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace RetroAuto
{
    class Program
    {
        private const string DEFAULT_ROM_DIR = @"C:\Users\rob\Games\ATARI2600";
        private const string DEFAULT_RETROARCH = @"C:\RetroArch-Win64\retroarch.exe";
        private const string CORE_NAME = "stella2014";

        // Game Boy / Mesen settings
        private const string DEFAULT_GAMEBOY_DIR = @"C:\Users\rob\Games\Gameboy";
        private const string DEFAULT_MESEN = @"C:\Users\rob\Games\Mesen\Mesen.exe";

        // N64 / Project64 settings
        private const string DEFAULT_N64_DIR = @"C:\Users\rob\Games\N64";
        private const string DEFAULT_PROJECT64 = @"C:\Users\rob\Games\N64\Project64\Project64.exe";

        [STAThread]
        static async Task<int> Main(string[] args)
        {
            // Check for Game Boy mode
            if (args.Any(a => a.Equals("gameboy", StringComparison.OrdinalIgnoreCase) ||
                             a.Equals("--gameboy", StringComparison.OrdinalIgnoreCase) ||
                             a.Equals("gb", StringComparison.OrdinalIgnoreCase)))
            {
                return await RunGameBoyMode(args);
            }

            // Check for N64 mode
            if (args.Any(a => a.Equals("n64", StringComparison.OrdinalIgnoreCase) ||
                             a.Equals("--n64", StringComparison.OrdinalIgnoreCase)))
            {
                return await RunN64Mode(args);
            }

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
Usage: RetroAuto.exe [command] [rom_directory]

Commands:
  continue, --continue  Continue playing from where you left off (default)
  restart, --restart    Restart playlist from beginning (same order, no re-randomize)
  reset, --reset        Reset playlist with NEW random order and start playing
  status, --status      Show playlist status and progress
  help, --help, -h      Show this help message

Options:
  --min-max X,Y        Set random play duration range in seconds (default: 20,60)

Arguments:
  rom_directory        Path to ROM directory (default: C:\Users\rob\Games\ATARI2600)

Examples:
  RetroAuto.exe                              # Continue from last position
  RetroAuto.exe --continue                   # Same as above
  RetroAuto.exe --restart                    # Start over from beginning
  RetroAuto.exe --reset                      # Re-randomize and play
  RetroAuto.exe --status                     # Check progress
  RetroAuto.exe --min-max 10,30              # Play each game 10-30 seconds
  RetroAuto.exe --min-max 5,15 --restart     # Quick preview mode
  RetroAuto.exe play ""C:\Games\NES""          # Use custom ROM directory
  RetroAuto.exe --restart ""C:\Games\NES""     # Restart custom directory

Files created in ROM directory:
  games.txt          - Full randomized playlist
  games_progress.txt - Remaining games to play

Press Ctrl+C during playback to stop and save progress.

Settings:
  RetroArch: C:\RetroArch-Win64\retroarch.exe
  Core: stella2014
  Play Time: 20-60 seconds (random per game)

=== GAME BOY MODE ===
  RetroAuto.exe gameboy              # Interactive Game Boy player with Mesen
  RetroAuto.exe gb                   # Same as above (short form)

=== N64 MODE ===
  RetroAuto.exe n64                  # Interactive N64 player with Project64
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
            Console.WriteLine("=== RetroAuto - N64 Mode (Project64) ===\n");

            try
            {
                // Parse optional ROM directory argument
                string romDir = DEFAULT_N64_DIR;
                string emulatorPath = DEFAULT_PROJECT64;

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
    }
}
