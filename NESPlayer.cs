using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace RetroAuto
{
    /// <summary>
    /// Nintendo Entertainment System player using Ares emulator
    /// With window memory support (position, size, maximized state)
    /// </summary>
    public class NESPlayer : BaseInteractivePlayer
    {
        private const string DEFAULT_EMULATOR_PATH = @"C:\Users\rob\Games\ares\ares-v146\ares.exe";
        private const string DEFAULT_ROM_DIR = @"C:\Users\rob\Games\NES";
        private static readonly string[] ROM_EXTENSIONS = { "*.nes", "*.zip" };

        private readonly string windowConfigPath;

        public NESPlayer(string? emulatorPath = null, string? romDirectory = null)
            : base(
                emulatorPath ?? DEFAULT_EMULATOR_PATH,
                romDirectory ?? DEFAULT_ROM_DIR,
                "nes_games.txt",
                "Nintendo Entertainment System",
                ROM_EXTENSIONS,
                ConsoleColor.Red)
        {
            // Store window config in ROM directory
            windowConfigPath = Path.Combine(romDirectory ?? DEFAULT_ROM_DIR, "ares_window.json");
        }

        /// <summary>
        /// Override to recursively scan subdirectories for ROMs
        /// </summary>
        public override void Initialize()
        {
            Console.WriteLine($"Scanning for {systemName} ROMs (including subdirectories)...");

            try
            {
                var games = new List<string>();

                foreach (var ext in romExtensions)
                {
                    // Recursively search all subdirectories using base class method
                    var files = SafeGetFilesRecursive(romDirectory, ext)
                        .Where(f => !f.Contains("ares", StringComparison.OrdinalIgnoreCase));
                    games.AddRange(files);
                }

                allGames = games.Distinct().OrderBy(f => Path.GetFileNameWithoutExtension(f)).ToList();
                Console.WriteLine($"Found {allGames.Count} games");

                if (allGames.Count > 0)
                {
                    SafeWriteGamesList();
                    remainingGames = allGames.OrderBy(x => random.Next()).ToList();
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Warning: Error during initialization: {ex.Message}");
                allGames = new List<string>();
                remainingGames = new List<string>();
            }
        }

        protected override string GetLaunchArguments(string romPath)
        {
            // Ares accepts ROM path directly
            return $"\"{romPath}\"";
        }

#if !CROSS_PLATFORM
        /// <summary>
        /// Override to add window memory support for Ares
        /// </summary>
        protected override async Task<bool> SafeLaunchGame(string romPath)
        {
            if (!File.Exists(romPath))
            {
                Console.WriteLine($"ERROR: ROM file not found: {romPath}");
                Console.WriteLine("Press any key to continue...");
                Console.ReadKey(true);
                return false;
            }

            try
            {
                // Load saved window position
                WindowManager.WindowPosition? savedPosition = WindowManager.LoadWindowPosition(windowConfigPath);
                if (savedPosition != null)
                {
                    Console.WriteLine($"Window preferences: {savedPosition.Width}x{savedPosition.Height}" +
                        (savedPosition.IsMaximized ? " (maximized)" : ""));
                }

                var psi = new ProcessStartInfo
                {
                    FileName = emulatorPath,
                    Arguments = GetLaunchArguments(romPath),
                    UseShellExecute = false
                };

                currentProcess = Process.Start(psi);
                if (currentProcess == null)
                {
                    Console.WriteLine("Failed to start emulator");
                    Console.WriteLine("Press any key to continue...");
                    Console.ReadKey(true);
                    return false;
                }

                // Wait for window to appear
                var hWnd = await WindowManager.WaitForProcessWindowAsync(currentProcess, 5000);

                if (hWnd != IntPtr.Zero)
                {
                    // Check if display options override saved position
                    var displayOpts = DisplayOptions.Current;
                    if (displayOpts.Monitor.HasValue || displayOpts.Maximized || displayOpts.Fullscreen)
                    {
                        displayOpts.ApplyToWindow(hWnd, WindowManager.FullscreenMethod.F11);
                        Console.WriteLine($"Applied display options: {displayOpts}");
                    }
                    else if (savedPosition != null)
                    {
                        if (WindowManager.SetWindowPosition(hWnd, savedPosition))
                        {
                            Console.WriteLine("Window position restored");
                        }
                    }
                }

                Console.WriteLine("Game is running. Press any key to stop and return to selection...\n");

                // Wait for key press or process exit
                while (!Console.KeyAvailable && !currentProcess.HasExited)
                {
                    await Task.Delay(100);
                }

                if (Console.KeyAvailable)
                {
                    Console.ReadKey(true);
                }

                // Save window position before closing
                if (!currentProcess.HasExited)
                {
                    try
                    {
                        currentProcess.Refresh();
                        if (currentProcess.MainWindowHandle != IntPtr.Zero)
                        {
                            var currentPosition = WindowManager.GetWindowPosition(currentProcess.MainWindowHandle);
                            if (currentPosition != null)
                            {
                                WindowManager.SaveWindowPosition(currentPosition, windowConfigPath);
                                Console.WriteLine("Window position saved");
                            }
                        }
                    }
                    catch { }

                    // Close the emulator
                    try
                    {
                        currentProcess.CloseMainWindow();
                        if (!currentProcess.WaitForExit(3000))
                        {
                            currentProcess.Kill();
                        }
                    }
                    catch { }
                }

                return true;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error launching game: {ex.Message}");
                Console.WriteLine("Press any key to continue...");
                Console.ReadKey(true);
                return false;
            }
            finally
            {
                currentProcess?.Dispose();
                currentProcess = null;
            }
        }
#endif
    }
}
