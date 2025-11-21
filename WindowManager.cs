using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;

namespace RetroAuto
{
    public class WindowManager
    {
        // Windows API imports
        [DllImport("user32.dll")]
        private static extern bool SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter, int X, int Y, int cx, int cy, uint uFlags);

        [DllImport("user32.dll")]
        private static extern bool GetWindowRect(IntPtr hWnd, out RECT lpRect);

        [DllImport("user32.dll")]
        private static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

        [DllImport("user32.dll")]
        private static extern bool IsWindowVisible(IntPtr hWnd);

        [DllImport("user32.dll")]
        private static extern IntPtr GetForegroundWindow();

        [DllImport("user32.dll")]
        private static extern bool SetForegroundWindow(IntPtr hWnd);

        [StructLayout(LayoutKind.Sequential)]
        public struct RECT
        {
            public int Left;
            public int Top;
            public int Right;
            public int Bottom;

            public int Width => Right - Left;
            public int Height => Bottom - Top;
        }

        // Constants
        private const uint SWP_NOSIZE = 0x0001;
        private const uint SWP_NOZORDER = 0x0004;
        private const uint SWP_SHOWWINDOW = 0x0040;
        private const int SW_RESTORE = 9;
        private const int SW_MAXIMIZE = 3;
        private const int SW_SHOW = 5;

        public class WindowPosition
        {
            public int X { get; set; }
            public int Y { get; set; }
            public int Width { get; set; }
            public int Height { get; set; }
            public bool IsMaximized { get; set; }
        }

        public static WindowPosition? GetWindowPosition(IntPtr hWnd)
        {
            if (hWnd == IntPtr.Zero || !IsWindowVisible(hWnd))
                return null;

            if (GetWindowRect(hWnd, out RECT rect))
            {
                return new WindowPosition
                {
                    X = rect.Left,
                    Y = rect.Top,
                    Width = rect.Width,
                    Height = rect.Height,
                    IsMaximized = false // We'll detect this separately if needed
                };
            }

            return null;
        }

        public static bool SetWindowPosition(IntPtr hWnd, WindowPosition position)
        {
            if (hWnd == IntPtr.Zero || position == null)
                return false;

            try
            {
                // First restore the window if it's maximized/minimized
                ShowWindow(hWnd, SW_RESTORE);
                Thread.Sleep(100);

                // Set position and size
                bool result = SetWindowPos(
                    hWnd,
                    IntPtr.Zero,
                    position.X,
                    position.Y,
                    position.Width,
                    position.Height,
                    SWP_NOZORDER | SWP_SHOWWINDOW
                );

                Thread.Sleep(100);

                // Maximize if needed
                if (position.IsMaximized)
                {
                    ShowWindow(hWnd, SW_MAXIMIZE);
                    Thread.Sleep(100);
                }

                // Bring to foreground
                SetForegroundWindow(hWnd);

                return result;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error setting window position: {ex.Message}");
                return false;
            }
        }

        public static async Task<IntPtr> WaitForProcessWindowAsync(Process process, int timeoutMs = 5000)
        {
            var startTime = DateTime.Now;

            while ((DateTime.Now - startTime).TotalMilliseconds < timeoutMs)
            {
                try
                {
                    // Refresh process info
                    process.Refresh();

                    // Check if main window handle is available
                    if (process.MainWindowHandle != IntPtr.Zero)
                    {
                        // Give it a moment to fully initialize
                        await Task.Delay(500);
                        return process.MainWindowHandle;
                    }
                }
                catch
                {
                    // Process might have exited
                    return IntPtr.Zero;
                }

                await Task.Delay(100);
            }

            return IntPtr.Zero;
        }

        public static void SaveWindowPosition(WindowPosition position, string configFile)
        {
            try
            {
                var json = System.Text.Json.JsonSerializer.Serialize(position, new System.Text.Json.JsonSerializerOptions
                {
                    WriteIndented = true
                });
                System.IO.File.WriteAllText(configFile, json);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Warning: Could not save window position: {ex.Message}");
            }
        }

        public static WindowPosition? LoadWindowPosition(string configFile)
        {
            try
            {
                if (System.IO.File.Exists(configFile))
                {
                    var json = System.IO.File.ReadAllText(configFile);
                    return System.Text.Json.JsonSerializer.Deserialize<WindowPosition>(json);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Warning: Could not load window position: {ex.Message}");
            }

            return null;
        }
    }
}
