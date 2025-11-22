using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace RetroAuto
{
    /// <summary>
    /// Commodore Amiga player using FS-UAE emulator
    /// With window memory support (position, size, maximized state)
    /// </summary>
    public class AmigaPlayer : BaseInteractivePlayer
    {
        private const string DEFAULT_EMULATOR_PATH = @"C:\Users\rob\AppData\Local\Programs\FS-UAE\Launcher\Windows\x86-64\fs-uae-launcher.exe";
        private const string DEFAULT_ROM_DIR = @"C:\Users\rob\Games\Amiga";
        private static readonly string[] ROM_EXTENSIONS = { "*.adf", "*.ipf", "*.dms", "*.adz", "*.zip", "*.lha", "*.hdf", "*.iso" };

        private readonly string windowConfigPath;

        public AmigaPlayer(string? emulatorPath = null, string? romDirectory = null)
            : base(
                emulatorPath ?? DEFAULT_EMULATOR_PATH,
                romDirectory ?? DEFAULT_ROM_DIR,
                "amiga_games.txt",
                "Commodore Amiga",
                ROM_EXTENSIONS,
                ConsoleColor.DarkCyan)
        {
            // Store window config in ROM directory
            windowConfigPath = Path.Combine(romDirectory ?? DEFAULT_ROM_DIR, "fsuae_window.json");
        }

        /// <summary>
        /// Override to recursively scan subdirectories for ROMs
        /// </summary>
        public override void Initialize()
        {
            Console.WriteLine($"Scanning for {systemName} games (including subdirectories)...");

            try
            {
                var games = new List<string>();

                foreach (var ext in romExtensions)
                {
                    // Recursively search all subdirectories
                    var files = SafeGetFilesRecursive(romDirectory, ext)
                        .Where(f => !f.Contains("FS-UAE", StringComparison.OrdinalIgnoreCase))
                        .Where(f => !f.Contains("Kickstart", StringComparison.OrdinalIgnoreCase)); // Exclude BIOS files
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
            // FS-UAE launcher accepts the ROM path directly
            return $"\"{romPath}\"";
        }

#if !CROSS_PLATFORM
        /// <summary>
        /// Override to add window memory support for FS-UAE
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
                var hWnd = await WindowManager.WaitForProcessWindowAsync(currentProcess, 8000); // FS-UAE may take longer

                if (hWnd != IntPtr.Zero)
                {
                    // Check if display options override saved position
                    var displayOpts = DisplayOptions.Current;
                    if (displayOpts.Monitor.HasValue || displayOpts.Maximized || displayOpts.Fullscreen)
                    {
                        // FS-UAE uses F12 for fullscreen by default
                        displayOpts.ApplyToWindow(hWnd, WindowManager.FullscreenMethod.F12);
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
