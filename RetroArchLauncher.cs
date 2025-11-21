using System;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace RetroAuto
{
    public class RetroArchLauncher
    {
        private readonly string retroArchPath;
        private readonly string coreName;
        private readonly string windowConfigPath;
        private Process? currentProcess;
        private bool enableWindowMemory;

        public RetroArchLauncher(
            string retroArchPath = @"C:\Program Files\RetroArch\retroarch.exe",
            string coreName = "stella",
            string? configDirectory = null,
            bool enableWindowMemory = true)
        {
            this.retroArchPath = retroArchPath;
            this.coreName = coreName;
            this.enableWindowMemory = enableWindowMemory;

            // Default config path to same directory as ROM folder or exe directory
            if (configDirectory == null)
            {
                configDirectory = Path.GetDirectoryName(retroArchPath) ?? Environment.CurrentDirectory;
            }

            this.windowConfigPath = Path.Combine(configDirectory, "retroauto_window.json");

            if (!File.Exists(retroArchPath))
            {
                throw new Exception($"RetroArch not found at: {retroArchPath}");
            }
        }

        public async Task<bool> LaunchGameAsync(string romPath, int playSeconds, CancellationToken cancellationToken = default)
        {
            try
            {
                Console.WriteLine($"Launching: {Path.GetFileName(romPath)}");
                Console.WriteLine($"Play duration: {playSeconds} seconds");

                // Load saved window position
                WindowManager.WindowPosition? savedPosition = null;
                if (enableWindowMemory)
                {
                    savedPosition = WindowManager.LoadWindowPosition(windowConfigPath);
                    if (savedPosition != null)
                    {
                        Console.WriteLine($"Loaded window preferences: {savedPosition.Width}x{savedPosition.Height} at ({savedPosition.X}, {savedPosition.Y})" +
                                        (savedPosition.IsMaximized ? " [Maximized]" : ""));
                    }
                }

                var startInfo = new ProcessStartInfo
                {
                    FileName = retroArchPath,
                    Arguments = $"-L {coreName}_libretro.dll \"{romPath}\"",
                    UseShellExecute = false,
                    CreateNoWindow = false,
                    RedirectStandardOutput = false,
                    RedirectStandardError = false
                };

                Console.WriteLine($"Command: \"{retroArchPath}\" {startInfo.Arguments}");

                currentProcess = Process.Start(startInfo);

                if (currentProcess == null)
                {
                    Console.WriteLine("ERROR: Failed to start RetroArch process");
                    return false;
                }

                Console.WriteLine($"RetroArch launched (PID: {currentProcess.Id})");

                // Wait for window to appear and apply saved position
                if (enableWindowMemory && savedPosition != null)
                {
                    Console.WriteLine("Waiting for RetroArch window...");
                    var hWnd = await WindowManager.WaitForProcessWindowAsync(currentProcess, 5000);

                    if (hWnd != IntPtr.Zero)
                    {
                        Console.WriteLine("Applying saved window position...");
                        if (WindowManager.SetWindowPosition(hWnd, savedPosition))
                        {
                            Console.WriteLine("Window position restored successfully");
                        }
                        else
                        {
                            Console.WriteLine("Warning: Could not restore window position");
                        }
                    }
                    else
                    {
                        Console.WriteLine("Warning: Could not find RetroArch window");
                    }
                }

                // Wait for the specified duration or cancellation
                var delayTask = Task.Delay(playSeconds * 1000, cancellationToken);
                var exitTask = currentProcess.WaitForExitAsync(cancellationToken);

                await Task.WhenAny(delayTask, exitTask);

                // Save window position before closing
                if (enableWindowMemory && !currentProcess.HasExited)
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

                // Close RetroArch if still running
                if (!currentProcess.HasExited)
                {
                    Console.WriteLine("Closing game...");
                    currentProcess.CloseMainWindow();

                    // Give it 2 seconds to close gracefully
                    if (!currentProcess.WaitForExit(2000))
                    {
                        currentProcess.Kill();
                    }
                }

                currentProcess.Dispose();
                currentProcess = null;

                return true;
            }
            catch (OperationCanceledException)
            {
                Console.WriteLine("Launch cancelled");
                if (currentProcess != null && !currentProcess.HasExited)
                {
                    currentProcess.Kill();
                    currentProcess.Dispose();
                }
                return false;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error launching game: {ex.Message}");
                return false;
            }
        }

        public void Cleanup()
        {
            if (currentProcess != null && !currentProcess.HasExited)
            {
                try
                {
                    currentProcess.Kill();
                    currentProcess.Dispose();
                }
                catch { }
            }
        }
    }
}
