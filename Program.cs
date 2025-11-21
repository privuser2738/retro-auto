using System;
using System.IO;
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

        [STAThread]
        static async Task<int> Main(string[] args)
        {
            Console.WriteLine("=== RetroAuto - RetroArch Playlist Automation ===\n");

            try
            {
                // Parse command line arguments
                string command = "continue";  // Default behavior
                string romDir = DEFAULT_ROM_DIR;

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
                        await PlayGames(playlist, false);
                        break;

                    case "reset":
                        playlist.ResetPlaylist();
                        Console.WriteLine("Playlist has been reset and randomized!");
                        Console.WriteLine("Starting playback with new random order...\n");
                        await PlayGames(playlist, false);
                        break;

                    case "restart":
                        playlist.RestartPlaylist();
                        Console.WriteLine("Restarting playlist from beginning (same order)...\n");
                        await PlayGames(playlist, false);
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

        static async Task PlayGames(GamePlaylist playlist, bool unused = false)
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
                    await TitlePopup.ShowBrieflyAsync(gamePath, gameNumber, totalGames);

                    // Random play duration between 20-60 seconds
                    int playSeconds = random.Next(20, 61);

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

Arguments:
  rom_directory        Path to ROM directory (default: C:\Users\rob\Games\ATARI2600)

Examples:
  RetroAuto.exe                              # Continue from last position
  RetroAuto.exe --continue                   # Same as above
  RetroAuto.exe --restart                    # Start over from beginning
  RetroAuto.exe --reset                      # Re-randomize and play
  RetroAuto.exe --status                     # Check progress
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
");
        }
    }
}
