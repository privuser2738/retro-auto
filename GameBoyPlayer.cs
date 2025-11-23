using System;

namespace RetroAuto
{
    /// <summary>
    /// Interactive Game Boy player using Mesen emulator
    /// </summary>
    public class GameBoyPlayer : BaseInteractivePlayer
    {
        private const string DEFAULT_MESEN_PATH = @"C:\Users\rob\Games\Apps\Mesen\Mesen.exe";
        private const string DEFAULT_ROM_DIR = @"C:\Users\rob\Games\Gameboy";
        private const string GAMES_LIST_FILE = "gameboy_games.txt";

        private static readonly string[] ROM_EXTENSIONS = { "*.gb", "*.gbc", "*.gba" };

        public GameBoyPlayer(string? mesenPath = null, string? romDirectory = null)
            : base(
                mesenPath ?? DEFAULT_MESEN_PATH,
                romDirectory ?? DEFAULT_ROM_DIR,
                GAMES_LIST_FILE,
                "Game Boy",
                ROM_EXTENSIONS,
                ConsoleColor.Cyan)
        {
        }
    }
}
