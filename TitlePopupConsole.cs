using System;
using System.IO;
using System.Threading.Tasks;

namespace RetroAuto
{
    /// <summary>
    /// Cross-platform console-based title display (replacement for Windows Forms popup)
    /// </summary>
    public static class TitlePopupConsole
    {
        public static async Task ShowBrieflyAsync(string romPath, int gameNumber, int totalGames)
        {
            Console.Clear();

            string title = Path.GetFileNameWithoutExtension(romPath);
            string filename = Path.GetFileName(romPath);

            Console.ForegroundColor = ConsoleColor.Cyan;
            Console.WriteLine("╔════════════════════════════════════════════════════════════════╗");
            Console.WriteLine("║                        NOW PLAYING                             ║");
            Console.WriteLine("╚════════════════════════════════════════════════════════════════╝");
            Console.ResetColor();
            Console.WriteLine();

            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.WriteLine($"  Game {gameNumber} of {totalGames}");
            Console.ResetColor();

            Console.WriteLine();
            Console.ForegroundColor = ConsoleColor.White;
            Console.WriteLine($"  {title}");
            Console.ResetColor();

            Console.ForegroundColor = ConsoleColor.DarkGray;
            Console.WriteLine($"  {filename}");
            Console.ResetColor();
            Console.WriteLine();

            // Brief delay to show the title
            await Task.Delay(2000);
        }
    }
}
