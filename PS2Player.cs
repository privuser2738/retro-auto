using System;

namespace RetroAuto
{
    /// <summary>
    /// PlayStation 2 player using PCSX2 emulator
    /// </summary>
    public class PS2Player : BaseInteractivePlayer
    {
        private const string DEFAULT_EMULATOR_PATH = @"C:\Program Files\PCSX2\pcsx2-qt.exe";
        private const string DEFAULT_ROM_DIR = @"C:\Users\rob\Games\PS2";
        private static readonly string[] ROM_EXTENSIONS = { "*.iso", "*.bin", "*.chd", "*.cso", "*.gz" };

        public PS2Player(string? emulatorPath = null, string? romDirectory = null)
            : base(
                emulatorPath ?? DEFAULT_EMULATOR_PATH,
                romDirectory ?? DEFAULT_ROM_DIR,
                "ps2_games.txt",
                "PlayStation 2",
                ROM_EXTENSIONS,
                ConsoleColor.DarkBlue)
        { }

        protected override string GetLaunchArguments(string romPath)
        {
            // PCSX2-Qt accepts ROM path directly with optional flags
            return $"\"{romPath}\"";
        }
    }
}
