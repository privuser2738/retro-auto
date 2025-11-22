using System;

namespace RetroAuto
{
    /// <summary>
    /// Xbox 360 player using Xenia emulator
    /// </summary>
    public class Xbox360Player : BaseInteractivePlayer
    {
        private const string DEFAULT_EMULATOR_PATH = @"C:\Users\rob\Games\Xenia\xenia.exe";
        private const string DEFAULT_ROM_DIR = @"C:\Users\rob\Games\Xbox360";
        private static readonly string[] ROM_EXTENSIONS = { "*.iso", "*.xex", "*.xcp", "*.zar" };

        public Xbox360Player(string? emulatorPath = null, string? romDirectory = null)
            : base(
                emulatorPath ?? DEFAULT_EMULATOR_PATH,
                romDirectory ?? DEFAULT_ROM_DIR,
                "xbox360_games.txt",
                "Xbox 360",
                ROM_EXTENSIONS,
                ConsoleColor.Green)
        { }

        protected override string GetLaunchArguments(string romPath)
        {
            // Xenia accepts ISO or XEX path directly
            return $"\"{romPath}\"";
        }
    }
}
