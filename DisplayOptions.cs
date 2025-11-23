using System;

namespace RetroAuto
{
    /// <summary>
    /// Display options for emulator window management
    /// </summary>
    public class DisplayOptions
    {
        /// <summary>
        /// Target monitor index (0-based). Null means use saved position or default.
        /// </summary>
        public int? Monitor { get; set; }

        /// <summary>
        /// Force maximized state on launch
        /// </summary>
        public bool Maximized { get; set; }

        /// <summary>
        /// Launch in fullscreen mode
        /// </summary>
        public bool Fullscreen { get; set; }

#if !CROSS_PLATFORM
        /// <summary>
        /// Fullscreen method to use (F11, F12, AltEnter)
        /// </summary>
        public WindowManager.FullscreenMethod FullscreenMethod { get; set; } = WindowManager.FullscreenMethod.F11;
#endif

        /// <summary>
        /// Parse display options from command line arguments
        /// </summary>
        public static DisplayOptions Parse(string[] args)
        {
            var options = new DisplayOptions();

            foreach (var arg in args)
            {
                var lower = arg.ToLower();

                // --monitor=X or --display=X
                if (lower.StartsWith("--monitor=") || lower.StartsWith("--display="))
                {
                    var value = arg.Substring(arg.IndexOf('=') + 1);
                    if (int.TryParse(value, out int monitorIndex))
                    {
                        options.Monitor = monitorIndex;
                    }
                }
                // --maximized
                else if (lower == "--maximized" || lower == "--max")
                {
                    options.Maximized = true;
                }
                // --fullscreen
                else if (lower == "--fullscreen" || lower == "--fs")
                {
                    options.Fullscreen = true;
                }
#if !CROSS_PLATFORM
                // --fullscreen-method=X
                else if (lower.StartsWith("--fullscreen-method=") || lower.StartsWith("--fs-method="))
                {
                    var value = arg.Substring(arg.IndexOf('=') + 1).ToLower();
                    options.FullscreenMethod = value switch
                    {
                        "f11" => WindowManager.FullscreenMethod.F11,
                        "f12" => WindowManager.FullscreenMethod.F12,
                        "altenter" or "alt-enter" or "alt+enter" => WindowManager.FullscreenMethod.AltEnter,
                        _ => WindowManager.FullscreenMethod.F11
                    };
                }
                // --monitors (list available monitors)
                else if (lower == "--monitors" || lower == "--list-monitors")
                {
                    WindowManager.PrintMonitors();
                }
#endif
            }

            return options;
        }

        /// <summary>
        /// Global display options instance (set from command line)
        /// </summary>
        public static DisplayOptions Current { get; set; } = new DisplayOptions();

#if !CROSS_PLATFORM
        /// <summary>
        /// Apply display options to a window after it's created
        /// </summary>
        public void ApplyToWindow(IntPtr hWnd, WindowManager.FullscreenMethod? emulatorFullscreenMethod = null)
        {
            if (hWnd == IntPtr.Zero) return;

            // Move to specific monitor if specified
            if (Monitor.HasValue)
            {
                if (Maximized)
                {
                    WindowManager.MoveToMonitorAndMaximize(hWnd, Monitor.Value);
                }
                else
                {
                    WindowManager.MoveToMonitor(hWnd, Monitor.Value);
                }
            }
            else if (Maximized)
            {
                // Just maximize on current monitor
                WindowManager.SetWindowPosition(hWnd, new WindowManager.WindowPosition
                {
                    IsMaximized = true,
                    X = 0, Y = 0, Width = 800, Height = 600  // These get ignored when maximizing
                });
            }

            // Apply fullscreen
            if (Fullscreen)
            {
                System.Threading.Thread.Sleep(500);  // Give window time to settle
                var method = emulatorFullscreenMethod ?? FullscreenMethod;
                WindowManager.SendFullscreenKey(hWnd, method);
            }
        }
#endif

        public override string ToString()
        {
            var parts = new System.Collections.Generic.List<string>();
            if (Monitor.HasValue) parts.Add($"Monitor={Monitor.Value}");
            if (Maximized) parts.Add("Maximized");
#if !CROSS_PLATFORM
            if (Fullscreen) parts.Add($"Fullscreen({FullscreenMethod})");
#else
            if (Fullscreen) parts.Add("Fullscreen");
#endif
            return parts.Count > 0 ? string.Join(", ", parts) : "Default";
        }
    }
}
