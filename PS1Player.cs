using System;

namespace RetroAuto
{
    /// <summary>
    /// PlayStation 1 player using DuckStation emulator
    /// </summary>
    public class PS1Player : BaseInteractivePlayer
    {
        private const string DEFAULT_EMULATOR_PATH = @"C:\Users\rob\Games\Duckstation\duckstation-qt-x64-ReleaseLTCG.exe";
        private const string DEFAULT_ROM_DIR = @"C:\Users\rob\Games\PS1";
        private static readonly string[] ROM_EXTENSIONS = { "*.bin", "*.cue", "*.iso", "*.chd", "*.img", "*.pbp" };

        public PS1Player(string? emulatorPath = null, string? romDirectory = null)
            : base(
                emulatorPath ?? DEFAULT_EMULATOR_PATH,
                romDirectory ?? DEFAULT_ROM_DIR,
                "ps1_games.txt",
                "PlayStation 1",
                ROM_EXTENSIONS,
                ConsoleColor.Blue)
        { }

        protected override string GetLaunchArguments(string romPath)
        {
            // DuckStation accepts ROM path directly, prefer .cue files for multi-track games
            return $"\"{romPath}\"";
        }
    }
}
