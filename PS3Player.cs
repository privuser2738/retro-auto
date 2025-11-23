using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace RetroAuto
{
    /// <summary>
    /// PlayStation 3 player using RPCS3 emulator
    /// </summary>
    public class PS3Player : BaseInteractivePlayer
    {
        private const string DEFAULT_EMULATOR_PATH = @"C:\Users\rob\Games\Apps\RPCS3\rpcs3.exe";
        private const string DEFAULT_ROM_DIR = @"C:\Users\rob\Games\PS3";
        private static readonly string[] ROM_EXTENSIONS = { "*.iso", "*.bin" };

        public PS3Player(string? emulatorPath = null, string? romDirectory = null)
            : base(
                emulatorPath ?? DEFAULT_EMULATOR_PATH,
                romDirectory ?? DEFAULT_ROM_DIR,
                "ps3_games.txt",
                "PlayStation 3",
                ROM_EXTENSIONS,
                ConsoleColor.DarkMagenta)
        { }

        /// <summary>
        /// Override to also scan for folder-based games (EBOOT.BIN in PS3_GAME/USRDIR)
        /// </summary>
        public override void Initialize()
        {
            Console.WriteLine($"Scanning for {systemName} ROMs and game folders...");

            try
            {
                var games = new List<string>();

                // Scan for ISO/BIN files
                foreach (var ext in romExtensions)
                {
                    games.AddRange(SafeGetFiles(romDirectory, ext));
                }

                // Scan for folder-based games (look for EBOOT.BIN)
                foreach (var dir in Directory.GetDirectories(romDirectory))
                {
                    // Check for PS3_GAME/USRDIR/EBOOT.BIN structure
                    var ebootPath = Path.Combine(dir, "PS3_GAME", "USRDIR", "EBOOT.BIN");
                    if (File.Exists(ebootPath))
                    {
                        games.Add(ebootPath);
                        continue;
                    }

                    // Check for direct EBOOT.BIN (extracted game)
                    ebootPath = Path.Combine(dir, "USRDIR", "EBOOT.BIN");
                    if (File.Exists(ebootPath))
                    {
                        games.Add(ebootPath);
                        continue;
                    }

                    // Check if directory itself contains EBOOT.BIN
                    ebootPath = Path.Combine(dir, "EBOOT.BIN");
                    if (File.Exists(ebootPath))
                    {
                        games.Add(ebootPath);
                    }
                }

                allGames = games.Distinct().OrderBy(f => GetDisplayName(f)).ToList();
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

        private string GetDisplayName(string path)
        {
            if (path.EndsWith("EBOOT.BIN", StringComparison.OrdinalIgnoreCase))
            {
                // Get the game folder name
                var dir = Path.GetDirectoryName(path);
                while (dir != null && !dir.Equals(romDirectory, StringComparison.OrdinalIgnoreCase))
                {
                    var parent = Path.GetDirectoryName(dir);
                    if (parent != null && parent.Equals(romDirectory, StringComparison.OrdinalIgnoreCase))
                    {
                        return Path.GetFileName(dir);
                    }
                    dir = parent;
                }
                return Path.GetFileName(Path.GetDirectoryName(path) ?? path);
            }
            return Path.GetFileNameWithoutExtension(path);
        }

        protected override void ShowTitle(string title, string filename, int gameNumber, int totalGames)
        {
            // Override to show better names for folder-based games
            if (filename == "EBOOT.BIN")
            {
                filename = title;
            }
            base.ShowTitle(title, filename, gameNumber, totalGames);
        }

        protected override string GetLaunchArguments(string romPath)
        {
            // RPCS3 can launch ISO directly or EBOOT.BIN
            return $"\"{romPath}\"";
        }
    }
}
