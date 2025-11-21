using System;

namespace RetroAuto
{
    /// <summary>
    /// Interactive N64 player using Project64 emulator
    /// </summary>
    public class N64Player : BaseInteractivePlayer
    {
        private const string DEFAULT_PROJECT64_PATH = @"C:\Users\rob\Games\N64\Project64\Project64.exe";
        private const string DEFAULT_ROM_DIR = @"C:\Users\rob\Games\N64";
        private const string GAMES_LIST_FILE = "n64_games.txt";

        private static readonly string[] ROM_EXTENSIONS = { "*.n64", "*.z64", "*.v64", "*.zip" };

        public N64Player(string? emulatorPath = null, string? romDirectory = null)
            : base(
                emulatorPath ?? DEFAULT_PROJECT64_PATH,
                romDirectory ?? DEFAULT_ROM_DIR,
                GAMES_LIST_FILE,
                "N64",
                ROM_EXTENSIONS,
                ConsoleColor.Green)
        {
        }
    }
}
