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
        private static extern bool IsZoomed(IntPtr hWnd); // Returns true if window is maximized

        [DllImport("user32.dll")]
        private static extern bool GetWindowPlacement(IntPtr hWnd, ref WINDOWPLACEMENT lpwndpl);

        [DllImport("user32.dll")]
        private static extern IntPtr GetForegroundWindow();

        [DllImport("user32.dll")]
        private static extern bool SetForegroundWindow(IntPtr hWnd);

        [DllImport("user32.dll")]
        private static extern bool PostMessage(IntPtr hWnd, uint Msg, IntPtr wParam, IntPtr lParam);

        [DllImport("user32.dll")]
        private static extern IntPtr FindWindow(string? lpClassName, string? lpWindowName);

        [DllImport("user32.dll")]
        private static extern bool EnumWindows(EnumWindowsProc lpEnumFunc, IntPtr lParam);

        [DllImport("user32.dll")]
        private static extern int GetWindowText(IntPtr hWnd, System.Text.StringBuilder lpString, int nMaxCount);

        [DllImport("user32.dll")]
        private static extern int GetWindowTextLength(IntPtr hWnd);

        [DllImport("user32.dll")]
        private static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint lpdwProcessId);

        private delegate bool EnumWindowsProc(IntPtr hWnd, IntPtr lParam);

        private const uint WM_KEYDOWN = 0x0100;
        private const uint WM_KEYUP = 0x0101;
        private const int VK_RETURN = 0x0D;

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

        [StructLayout(LayoutKind.Sequential)]
        public struct WINDOWPLACEMENT
        {
            public int length;
            public int flags;
            public int showCmd;
            public System.Drawing.Point ptMinPosition;
            public System.Drawing.Point ptMaxPosition;
            public RECT rcNormalPosition;
        }

        // Constants
        private const int SW_SHOWMAXIMIZED = 3;
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

            try
            {
                // Check if window is maximized
                bool isMaximized = IsZoomed(hWnd);

                // Get window placement to get the normal (restored) position
                // This is important because GetWindowRect returns the maximized size when maximized
                var placement = new WINDOWPLACEMENT();
                placement.length = Marshal.SizeOf(placement);

                if (GetWindowPlacement(hWnd, ref placement))
                {
                    // Use the normal position (rcNormalPosition) which is the restored size
                    var normalRect = placement.rcNormalPosition;

                    return new WindowPosition
                    {
                        X = normalRect.Left,
                        Y = normalRect.Top,
                        Width = normalRect.Width,
                        Height = normalRect.Height,
                        IsMaximized = isMaximized || placement.showCmd == SW_SHOWMAXIMIZED
                    };
                }

                // Fallback to GetWindowRect if placement fails
                if (GetWindowRect(hWnd, out RECT rect))
                {
                    return new WindowPosition
                    {
                        X = rect.Left,
                        Y = rect.Top,
                        Width = rect.Width,
                        Height = rect.Height,
                        IsMaximized = isMaximized
                    };
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Warning: Error getting window position: {ex.Message}");
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

        /// <summary>
        /// Sends Enter key to confirm any dialog that appears for a process.
        /// Useful for auto-confirming "Close game?" dialogs in emulators.
        /// </summary>
        public static async Task AutoConfirmDialogsAsync(Process process, int maxWaitMs = 2000, int checkIntervalMs = 100)
        {
            var startTime = DateTime.Now;
            IntPtr mainWindowHandle = IntPtr.Zero;

            try
            {
                process.Refresh();
                mainWindowHandle = process.MainWindowHandle;
            }
            catch { }

            while ((DateTime.Now - startTime).TotalMilliseconds < maxWaitMs)
            {
                try
                {
                    if (process.HasExited)
                        return;

                    // Get the foreground window - if a dialog appeared, it should be in front
                    var foregroundWnd = GetForegroundWindow();
                    if (foregroundWnd == IntPtr.Zero)
                    {
                        await Task.Delay(checkIntervalMs);
                        continue;
                    }

                    // Skip if this is the main window (not a dialog)
                    if (foregroundWnd == mainWindowHandle)
                    {
                        await Task.Delay(checkIntervalMs);
                        continue;
                    }

                    // Check if the foreground window belongs to our process
                    GetWindowThreadProcessId(foregroundWnd, out uint windowPid);

                    if (windowPid == process.Id)
                    {
                        // Get window title
                        int length = GetWindowTextLength(foregroundWnd);
                        if (length > 0)
                        {
                            var sb = new System.Text.StringBuilder(length + 1);
                            GetWindowText(foregroundWnd, sb, sb.Capacity);
                            string title = sb.ToString();

                            // Only confirm if it looks like a confirmation dialog
                            // Must contain specific dialog keywords AND not be a game title window
                            bool isConfirmDialog =
                                title.Contains("Confirm", StringComparison.OrdinalIgnoreCase) ||
                                title.Contains("Close", StringComparison.OrdinalIgnoreCase) ||
                                title.Contains("Exit", StringComparison.OrdinalIgnoreCase) ||
                                title.Contains("Quit", StringComparison.OrdinalIgnoreCase) ||
                                title.Contains("Save State", StringComparison.OrdinalIgnoreCase) ||
                                title.Contains("Warning", StringComparison.OrdinalIgnoreCase) ||
                                title.Equals("DuckStation", StringComparison.OrdinalIgnoreCase); // Plain "DuckStation" is usually a dialog

                            // If we found a confirmation dialog, send Enter
                            if (isConfirmDialog)
                            {
                                Console.WriteLine($"[AUTO] Confirming dialog: {title}");

                                // Make sure it's in foreground
                                SetForegroundWindow(foregroundWnd);
                                await Task.Delay(50);

                                // Send Enter key
                                PostMessage(foregroundWnd, WM_KEYDOWN, (IntPtr)VK_RETURN, IntPtr.Zero);
                                await Task.Delay(50);
                                PostMessage(foregroundWnd, WM_KEYUP, (IntPtr)VK_RETURN, IntPtr.Zero);

                                // Wait a bit and check again (there might be multiple dialogs)
                                await Task.Delay(300);
                                continue;
                            }
                        }
                    }
                }
                catch
                {
                    // Ignore errors
                }

                await Task.Delay(checkIntervalMs);
            }
        }

        /// <summary>
        /// Sends Enter key to the specified window
        /// </summary>
        public static void SendEnterKey(IntPtr hWnd)
        {
            if (hWnd == IntPtr.Zero) return;

            SetForegroundWindow(hWnd);
            Thread.Sleep(50);
            PostMessage(hWnd, WM_KEYDOWN, (IntPtr)VK_RETURN, IntPtr.Zero);
            Thread.Sleep(50);
            PostMessage(hWnd, WM_KEYUP, (IntPtr)VK_RETURN, IntPtr.Zero);
        }
    }
}
