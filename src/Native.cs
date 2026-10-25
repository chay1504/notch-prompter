using System;
using System.Runtime.InteropServices;

namespace NotchPrompter
{
    /// <summary>Win32 calls the app needs that WPF does not expose.</summary>
    internal static class Native
    {
        // SetWindowDisplayAffinity values. EXCLUDEFROMCAPTURE needs Windows 10 2004 or newer;
        // MONITOR (shows as a black box in captures) is the fallback for older builds.
        public const uint WDA_NONE = 0x00, WDA_MONITOR = 0x01, WDA_EXCLUDEFROMCAPTURE = 0x11;

        public const int GWL_EXSTYLE = -20;
        public const int WS_EX_TOOLWINDOW = 0x00000080, WS_EX_NOACTIVATE = 0x08000000;
        public const int WM_HOTKEY = 0x0312;
        public const uint MOD_ALT = 0x1, MOD_CONTROL = 0x2, MOD_SHIFT = 0x4, MOD_NOREPEAT = 0x4000;
        public const uint SWP_NOSIZE = 0x1, SWP_NOMOVE = 0x2, SWP_NOACTIVATE = 0x10, SWP_NOOWNERZORDER = 0x200;
        public const int VK_SHIFT = 0x10, VK_CONTROL = 0x11;
        public static readonly IntPtr HWND_TOPMOST = new IntPtr(-1);
        public static readonly IntPtr HWND_MESSAGE = new IntPtr(-3);

        [DllImport("user32.dll", SetLastError = true)]
        public static extern bool SetWindowDisplayAffinity(IntPtr hWnd, uint dwAffinity);

        [DllImport("user32.dll")]
        public static extern bool GetWindowDisplayAffinity(IntPtr hWnd, out uint pdwAffinity);

        [DllImport("user32.dll", EntryPoint = "GetWindowLongW")]
        public static extern int GetWindowLong(IntPtr hWnd, int nIndex);

        [DllImport("user32.dll", EntryPoint = "SetWindowLongW")]
        public static extern int SetWindowLong(IntPtr hWnd, int nIndex, int dwNewLong);

        [DllImport("user32.dll")]
        public static extern bool SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter, int x, int y, int cx, int cy, uint flags);

        [DllImport("user32.dll", SetLastError = true)]
        public static extern bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, uint vk);

        [DllImport("user32.dll")]
        public static extern bool UnregisterHotKey(IntPtr hWnd, int id);

        [DllImport("user32.dll")]
        private static extern short GetAsyncKeyState(int vKey);

        [DllImport("dwmapi.dll")]
        private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int value, int size);

        /// <summary>Physical key state. The island never takes focus, so the thread's own key state is stale.</summary>
        public static bool IsKeyDown(int vk) => (GetAsyncKeyState(vk) & 0x8000) != 0;

        public static void UseDarkTitleBar(IntPtr hwnd, int captionColorRef)
        {
            int on = 1;
            DwmSetWindowAttribute(hwnd, 20, ref on, sizeof(int));              // DWMWA_USE_IMMERSIVE_DARK_MODE
            DwmSetWindowAttribute(hwnd, 35, ref captionColorRef, sizeof(int)); // DWMWA_CAPTION_COLOR (Windows 11)
        }
    }

    /// <summary>Keeps windows out of screen recordings, screenshots and screen sharing.</summary>
    internal static class CaptureGuard
    {
        /// <summary>Applies the affinity and returns the value Windows actually accepted.</summary>
        public static uint Apply(IntPtr hwnd, bool hide)
        {
            if (hwnd == IntPtr.Zero) return Native.WDA_NONE;
            if (!hide)
            {
                Native.SetWindowDisplayAffinity(hwnd, Native.WDA_NONE);
                return Native.WDA_NONE;
            }
            if (!Native.SetWindowDisplayAffinity(hwnd, Native.WDA_EXCLUDEFROMCAPTURE))
            {
                Log.Write("WDA_EXCLUDEFROMCAPTURE refused (error " + Marshal.GetLastWin32Error() + "), falling back to WDA_MONITOR");
                Native.SetWindowDisplayAffinity(hwnd, Native.WDA_MONITOR);
            }
            Native.GetWindowDisplayAffinity(hwnd, out uint actual);
            return actual;
        }
    }
}
