using System;

namespace RetroAuto
{
    /// <summary>
    /// Interactive Dreamcast player using Flycast emulator
    /// </summary>
    public class DreamcastPlayer : BaseInteractivePlayer
    {
        private const string DEFAULT_EMULATOR_PATH = @"C:\Users\rob\Games\Apps\Flycast\flycast.exe";
        private const string DEFAULT_ROM_DIR = @"C:\Users\rob\Games\Dreamcast";
        private const string GAMES_LIST_FILE = "dreamcast_games.txt";

        private static readonly string[] ROM_EXTENSIONS = { "*.chd", "*.cdi", "*.gdi", "*.cue", "*.iso" };

        public DreamcastPlayer(string? emulatorPath = null, string? romDirectory = null)
            : base(
                emulatorPath ?? DEFAULT_EMULATOR_PATH,
                romDirectory ?? DEFAULT_ROM_DIR,
                GAMES_LIST_FILE,
                "Dreamcast",
                ROM_EXTENSIONS,
                ConsoleColor.Blue)
        {
        }
    }
}
