using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace RetroAuto
{
    /// <summary>
    /// Super Nintendo player using BSNES emulator
    /// </summary>
    public class SNESPlayer : BaseInteractivePlayer
    {
        private const string DEFAULT_EMULATOR_PATH = @"C:\Users\rob\Games\SNES\BSNES\bsnes.exe";
        private const string DEFAULT_ROM_DIR = @"C:\Users\rob\Games\SNES";
        private static readonly string[] ROM_EXTENSIONS = { "*.sfc", "*.smc", "*.fig", "*.swc", "*.bs", "*.st" };

        public SNESPlayer(string? emulatorPath = null, string? romDirectory = null)
            : base(
                emulatorPath ?? DEFAULT_EMULATOR_PATH,
                romDirectory ?? DEFAULT_ROM_DIR,
                "snes_games.txt",
                "Super Nintendo",
                ROM_EXTENSIONS,
                ConsoleColor.Magenta)
        { }

        /// <summary>
        /// Override to exclude BSNES folder from ROM scanning
        /// </summary>
        public override void Initialize()
        {
            Console.WriteLine($"Scanning for {systemName} ROMs...");

            try
            {
                var games = new List<string>();

                foreach (var ext in romExtensions)
                {
                    var files = SafeGetFiles(romDirectory, ext)
                        .Where(f => !f.Contains("BSNES", StringComparison.OrdinalIgnoreCase));
                    games.AddRange(files);
                }

                allGames = games.Distinct().OrderBy(f => Path.GetFileNameWithoutExtension(f)).ToList();
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

        protected override string GetLaunchArguments(string romPath)
        {
            // BSNES accepts ROM path directly
            return $"\"{romPath}\"";
        }
    }
}
