using System;

namespace RetroAuto
{
    /// <summary>
    /// Sega Genesis / Mega Drive player using Ares emulator
    /// </summary>
    public class GenesisPlayer : BaseInteractivePlayer
    {
        private const string DEFAULT_EMULATOR_PATH = @"C:\Users\rob\Games\ares\ares-v146\ares.exe";
        private const string DEFAULT_ROM_DIR = @"C:\Users\rob\Games\Genesis";
        private static readonly string[] ROM_EXTENSIONS = { "*.zip", "*.bin", "*.md", "*.gen", "*.smd", "*.32x" };

        public GenesisPlayer(string? emulatorPath = null, string? romDirectory = null)
            : base(
                emulatorPath ?? DEFAULT_EMULATOR_PATH,
                romDirectory ?? DEFAULT_ROM_DIR,
                "genesis_games.txt",
                "Sega Genesis",
                ROM_EXTENSIONS,
                ConsoleColor.DarkRed)
        { }

        protected override string GetLaunchArguments(string romPath)
        {
            // Ares accepts ROM path directly
            return $"\"{romPath}\"";
        }
    }
}
