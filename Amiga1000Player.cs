using System;

namespace RetroAuto
{
    /// <summary>
    /// Interactive Amiga 1000 player using Ares emulator
    /// </summary>
    public class Amiga1000Player : BaseInteractivePlayer
    {
        private const string DEFAULT_EMULATOR_PATH = @"C:\Users\rob\Games\Apps\Ares\ares.exe";
        private const string DEFAULT_ROM_DIR = @"C:\Users\rob\Games\Amiga1000";
        private const string GAMES_LIST_FILE = "amiga1000_games.txt";

        private static readonly string[] ROM_EXTENSIONS = { "*.adf", "*.adz", "*.dms", "*.ipf", "*.zip" };

        public Amiga1000Player(string? emulatorPath = null, string? romDirectory = null)
            : base(
                emulatorPath ?? DEFAULT_EMULATOR_PATH,
                romDirectory ?? DEFAULT_ROM_DIR,
                GAMES_LIST_FILE,
                "Amiga 1000",
                ROM_EXTENSIONS,
                ConsoleColor.DarkCyan)
        {
        }
    }
}
