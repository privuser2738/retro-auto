using System;
using System.Collections.Generic;
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
        private delegate bool MonitorEnumProc(IntPtr hMonitor, IntPtr hdcMonitor, ref RECT lprcMonitor, IntPtr dwData);

        [DllImport("user32.dll")]
        private static extern bool EnumDisplayMonitors(IntPtr hdc, IntPtr lprcClip, MonitorEnumProc lpfnEnum, IntPtr dwData);

        [DllImport("user32.dll")]
        private static extern bool GetMonitorInfo(IntPtr hMonitor, ref MONITORINFO lpmi);

        [DllImport("user32.dll")]
        private static extern IntPtr MonitorFromWindow(IntPtr hwnd, uint dwFlags);

        [DllImport("user32.dll")]
        private static extern void keybd_event(byte bVk, byte bScan, uint dwFlags, UIntPtr dwExtraInfo);

        [StructLayout(LayoutKind.Sequential)]
        private struct MONITORINFO
        {
            public int cbSize;
            public RECT rcMonitor;
            public RECT rcWork;
            public uint dwFlags;
        }

        private const uint MONITOR_DEFAULTTONEAREST = 2;
        private const uint KEYEVENTF_KEYUP = 0x0002;

        private const uint WM_KEYDOWN = 0x0100;
        private const uint WM_KEYUP = 0x0101;
        private const uint WM_SYSKEYDOWN = 0x0104;
        private const uint WM_SYSKEYUP = 0x0105;
        private const int VK_RETURN = 0x0D;
        private const int VK_F11 = 0x7A;
        private const int VK_F12 = 0x7B;
        private const int VK_MENU = 0x12;  // Alt key

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

        /// <summary>
        /// Monitor info for display selection
        /// </summary>
        public class MonitorInfo
        {
            public int Index { get; set; }
            public IntPtr Handle { get; set; }
            public int X { get; set; }
            public int Y { get; set; }
            public int Width { get; set; }
            public int Height { get; set; }
            public bool IsPrimary { get; set; }
        }

        /// <summary>
        /// Gets all available monitors
        /// </summary>
        public static List<MonitorInfo> GetMonitors()
        {
            var monitors = new List<MonitorInfo>();
            int index = 0;

            EnumDisplayMonitors(IntPtr.Zero, IntPtr.Zero,
                (IntPtr hMonitor, IntPtr hdcMonitor, ref RECT lprcMonitor, IntPtr dwData) =>
                {
                    var mi = new MONITORINFO();
                    mi.cbSize = Marshal.SizeOf(mi);

                    if (GetMonitorInfo(hMonitor, ref mi))
                    {
                        monitors.Add(new MonitorInfo
                        {
                            Index = index++,
                            Handle = hMonitor,
                            X = mi.rcMonitor.Left,
                            Y = mi.rcMonitor.Top,
                            Width = mi.rcMonitor.Width,
                            Height = mi.rcMonitor.Height,
                            IsPrimary = (mi.dwFlags & 1) != 0  // MONITORINFOF_PRIMARY
                        });
                    }
                    return true;
                }, IntPtr.Zero);

            return monitors;
        }

        /// <summary>
        /// Moves a window to the specified monitor
        /// </summary>
        public static bool MoveToMonitor(IntPtr hWnd, int monitorIndex)
        {
            if (hWnd == IntPtr.Zero) return false;

            var monitors = GetMonitors();
            if (monitorIndex < 0 || monitorIndex >= monitors.Count)
            {
                Console.WriteLine($"Monitor {monitorIndex} not found. Available: 0-{monitors.Count - 1}");
                return false;
            }

            var monitor = monitors[monitorIndex];

            // Restore window first if maximized
            ShowWindow(hWnd, SW_RESTORE);
            Thread.Sleep(100);

            // Get current window size
            if (!GetWindowRect(hWnd, out RECT currentRect))
                return false;

            int windowWidth = currentRect.Width;
            int windowHeight = currentRect.Height;

            // Center window on the target monitor
            int newX = monitor.X + (monitor.Width - windowWidth) / 2;
            int newY = monitor.Y + (monitor.Height - windowHeight) / 2;

            return SetWindowPos(hWnd, IntPtr.Zero, newX, newY, windowWidth, windowHeight,
                SWP_NOZORDER | SWP_SHOWWINDOW);
        }

        /// <summary>
        /// Moves window to monitor and optionally maximizes
        /// </summary>
        public static bool MoveToMonitorAndMaximize(IntPtr hWnd, int monitorIndex)
        {
            if (!MoveToMonitor(hWnd, monitorIndex))
                return false;

            Thread.Sleep(100);
            ShowWindow(hWnd, SW_MAXIMIZE);
            return true;
        }

        /// <summary>
        /// Sends fullscreen toggle key (F11) to the window
        /// </summary>
        public static void SendFullscreenKey(IntPtr hWnd, FullscreenMethod method = FullscreenMethod.F11)
        {
            if (hWnd == IntPtr.Zero) return;

            SetForegroundWindow(hWnd);
            Thread.Sleep(100);

            switch (method)
            {
                case FullscreenMethod.F11:
                    keybd_event(VK_F11, 0, 0, UIntPtr.Zero);
                    Thread.Sleep(50);
                    keybd_event(VK_F11, 0, KEYEVENTF_KEYUP, UIntPtr.Zero);
                    break;

                case FullscreenMethod.F12:
                    keybd_event(VK_F12, 0, 0, UIntPtr.Zero);
                    Thread.Sleep(50);
                    keybd_event(VK_F12, 0, KEYEVENTF_KEYUP, UIntPtr.Zero);
                    break;

                case FullscreenMethod.AltEnter:
                    keybd_event(VK_MENU, 0, 0, UIntPtr.Zero);  // Alt down
                    Thread.Sleep(20);
                    keybd_event(VK_RETURN, 0, 0, UIntPtr.Zero);  // Enter down
                    Thread.Sleep(50);
                    keybd_event(VK_RETURN, 0, KEYEVENTF_KEYUP, UIntPtr.Zero);  // Enter up
                    Thread.Sleep(20);
                    keybd_event(VK_MENU, 0, KEYEVENTF_KEYUP, UIntPtr.Zero);  // Alt up
                    break;
            }
        }

        /// <summary>
        /// Fullscreen toggle methods for different emulators
        /// </summary>
        public enum FullscreenMethod
        {
            F11,        // Ares, DuckStation, most emulators
            F12,        // Some emulators
            AltEnter    // Project64, PCSX2, many Windows apps
        }

        /// <summary>
        /// Print available monitors
        /// </summary>
        public static void PrintMonitors()
        {
            var monitors = GetMonitors();
            Console.WriteLine($"Available monitors ({monitors.Count}):");
            foreach (var m in monitors)
            {
                Console.WriteLine($"  [{m.Index}] {m.Width}x{m.Height} at ({m.X},{m.Y}){(m.IsPrimary ? " (Primary)" : "")}");
            }
        }
    }
}
