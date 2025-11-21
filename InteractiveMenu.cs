using System;
using System.Collections.Generic;
using System.Linq;

namespace RetroAuto
{
    public class InteractiveMenu
    {
        public static int ShowMenu(string title, string[] options, int defaultIndex = 0)
        {
            int selectedIndex = defaultIndex;
            ConsoleKey key;

            Console.CursorVisible = false;

            do
            {
                Console.Clear();
                Console.WriteLine($"=== {title} ===\n");

                for (int i = 0; i < options.Length; i++)
                {
                    if (i == selectedIndex)
                    {
                        Console.BackgroundColor = ConsoleColor.White;
                        Console.ForegroundColor = ConsoleColor.Black;
                        Console.WriteLine($" > {options[i]} ");
                        Console.ResetColor();
                    }
                    else
                    {
                        Console.WriteLine($"   {options[i]}");
                    }
                }

                Console.WriteLine("\n[Arrow Keys to navigate, Enter to select]");

                key = Console.ReadKey(true).Key;

                if (key == ConsoleKey.UpArrow)
                {
                    selectedIndex = (selectedIndex - 1 + options.Length) % options.Length;
                }
                else if (key == ConsoleKey.DownArrow)
                {
                    selectedIndex = (selectedIndex + 1) % options.Length;
                }

            } while (key != ConsoleKey.Enter);

            Console.CursorVisible = true;
            return selectedIndex;
        }

        public static string? SearchAndSelect(string prompt, List<string> allItems, Func<string, string> displayFunc)
        {
            while (true)
            {
                Console.Clear();
                Console.WriteLine($"=== {prompt} ===\n");
                Console.Write("Search: ");
                string? searchTerm = Console.ReadLine()?.Trim();

                if (string.IsNullOrEmpty(searchTerm))
                {
                    return null; // Back
                }

                // Search for matching items (case-insensitive)
                var matches = allItems
                    .Where(item => displayFunc(item).Contains(searchTerm, StringComparison.OrdinalIgnoreCase))
                    .Take(20) // Limit to 20 results
                    .ToList();

                if (matches.Count == 0)
                {
                    Console.WriteLine("\nNo matches found. Press any key to try again...");
                    Console.ReadKey(true);
                    continue;
                }

                Console.WriteLine($"\nFound {matches.Count} matches:\n");

                // Add "Back" option at the end
                var options = matches.Select(m => displayFunc(m)).ToList();
                options.Add("<< Back to search >>");

                int selected = SelectFromList(options.ToArray());

                if (selected == options.Count - 1)
                {
                    continue; // Back to search
                }

                return matches[selected];
            }
        }

        public static int SelectFromList(string[] options, int defaultIndex = 0)
        {
            int selectedIndex = defaultIndex;
            int startLine = Console.CursorTop;
            ConsoleKey key;

            Console.CursorVisible = false;

            // Initial display
            DrawList(options, selectedIndex);

            do
            {
                key = Console.ReadKey(true).Key;

                if (key == ConsoleKey.UpArrow)
                {
                    selectedIndex = (selectedIndex - 1 + options.Length) % options.Length;
                    DrawList(options, selectedIndex);
                }
                else if (key == ConsoleKey.DownArrow)
                {
                    selectedIndex = (selectedIndex + 1) % options.Length;
                    DrawList(options, selectedIndex);
                }

            } while (key != ConsoleKey.Enter);

            Console.CursorVisible = true;
            Console.WriteLine();
            return selectedIndex;
        }

        private static void DrawList(string[] options, int selectedIndex)
        {
            int startTop = Console.CursorTop - options.Length;
            if (startTop < 0) startTop = 0;

            for (int i = 0; i < options.Length; i++)
            {
                Console.SetCursorPosition(0, startTop + i);
                Console.Write(new string(' ', Console.WindowWidth - 1));
                Console.SetCursorPosition(0, startTop + i);

                if (i == selectedIndex)
                {
                    Console.BackgroundColor = ConsoleColor.White;
                    Console.ForegroundColor = ConsoleColor.Black;
                    Console.Write($" > {options[i]}");
                    Console.ResetColor();
                }
                else
                {
                    Console.Write($"   {options[i]}");
                }
            }

            Console.SetCursorPosition(0, startTop + options.Length);
        }

        public static void ShowGameTitle(string title, string filename, int? gameNumber = null, int? totalGames = null)
        {
            Console.Clear();
            Console.ForegroundColor = ConsoleColor.Cyan;
            Console.WriteLine("╔════════════════════════════════════════════════════════════════╗");
            Console.WriteLine("║                        NOW PLAYING                             ║");
            Console.WriteLine("╚════════════════════════════════════════════════════════════════╝");
            Console.ResetColor();
            Console.WriteLine();

            if (gameNumber.HasValue && totalGames.HasValue)
            {
                Console.ForegroundColor = ConsoleColor.Yellow;
                Console.WriteLine($"  Game {gameNumber} of {totalGames}");
                Console.ResetColor();
            }

            Console.WriteLine();
            Console.ForegroundColor = ConsoleColor.White;
            Console.WriteLine($"  {title}");
            Console.ResetColor();
            Console.ForegroundColor = ConsoleColor.DarkGray;
            Console.WriteLine($"  {filename}");
            Console.ResetColor();
            Console.WriteLine();
        }
    }
}
