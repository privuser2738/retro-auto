using System;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;

namespace RetroAuto
{
    /// <summary>
    /// Sega Saturn player using YabaSanshiro emulator
    /// With window memory support (position, size, maximized state)
    /// </summary>
    public class SaturnPlayer : BaseInteractivePlayer
    {
        private const string DEFAULT_EMULATOR_PATH = @"C:\Users\rob\Games\Apps\YabaSanshiro\yabasanshiro.exe";
        private const string DEFAULT_ROM_DIR = @"C:\Users\rob\Games\Saturn";
        private static readonly string[] ROM_EXTENSIONS = { "*.chd", "*.cue", "*.iso", "*.bin" };

        private readonly string windowConfigPath;

        public SaturnPlayer(string? emulatorPath = null, string? romDirectory = null)
            : base(
                emulatorPath ?? DEFAULT_EMULATOR_PATH,
                romDirectory ?? DEFAULT_ROM_DIR,
                "saturn_games.txt",
                "Sega Saturn",
                ROM_EXTENSIONS,
                ConsoleColor.DarkYellow)
        {
            // Store window config in ROM directory
            windowConfigPath = Path.Combine(romDirectory ?? DEFAULT_ROM_DIR, "yabasanshiro_window.json");
        }

        protected override string GetLaunchArguments(string romPath)
        {
            // YabaSanshiro: -a for auto-start, --iso for ROM file
            return $"-a --iso=\"{romPath}\"";
        }

#if !CROSS_PLATFORM
        /// <summary>
        /// Override to add window memory support for YabaSanshiro
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

            await Task.Yield();

            try
            {
                // Load saved window position
                WindowManager.WindowPosition? savedPosition = WindowManager.LoadWindowPosition(windowConfigPath);
                if (savedPosition != null)
                {
                    Console.WriteLine($"Window preferences: {savedPosition.Width}x{savedPosition.Height}" +
                                    (savedPosition.IsMaximized ? " [Maximized]" : ""));
                }

                var startInfo = new ProcessStartInfo
                {
                    FileName = emulatorPath,
                    Arguments = GetLaunchArguments(romPath),
                    UseShellExecute = false,
                    CreateNoWindow = false
                };

                Console.WriteLine($"Command: \"{emulatorPath}\" {startInfo.Arguments}");

                lock (lockObject)
                {
                    currentProcess = Process.Start(startInfo);
                }

                if (currentProcess == null)
                {
                    Console.WriteLine($"ERROR: Failed to start YabaSanshiro");
                    Console.WriteLine("Press any key to continue...");
                    Console.ReadKey(true);
                    return false;
                }

                Console.WriteLine($"YabaSanshiro launched (PID: {currentProcess.Id})");

                // Wait for window to appear
                var hWnd = await WindowManager.WaitForProcessWindowAsync(currentProcess, 5000);

                if (hWnd != IntPtr.Zero)
                {
                    // Check if display options override saved position
                    var displayOpts = DisplayOptions.Current;
                    if (displayOpts.Monitor.HasValue || displayOpts.Maximized || displayOpts.Fullscreen)
                    {
                        // Apply display options (monitor, maximized, fullscreen)
                        displayOpts.ApplyToWindow(hWnd, WindowManager.FullscreenMethod.AltEnter);
                        Console.WriteLine($"Applied display options: {displayOpts}");
                    }
                    else if (savedPosition != null)
                    {
                        // Use saved window position
                        if (WindowManager.SetWindowPosition(hWnd, savedPosition))
                        {
                            Console.WriteLine("Window position restored");
                        }
                    }
                }

                Console.WriteLine("\nPress any key to close the game and return to menu...");

                // Wait for user input or process exit
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
                    catch (Exception ex)
                    {
                        Console.WriteLine($"Warning: Could not save window position: {ex.Message}");
                    }
                }

                SafeCleanupProcess();
                return true;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error launching game: {ex.Message}");
                Console.WriteLine("Press any key to continue...");
                Console.ReadKey(true);
                SafeCleanupProcess();
                return false;
            }
        }
#endif
    }
}
