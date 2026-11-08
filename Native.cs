using System;
using System.Runtime.InteropServices;

namespace LightReader
{
    internal static class Native
    {
        [DllImport("dwmapi.dll", PreserveSig = true)]
        private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attribute,
            ref int value, int size);

        public static void SetDarkTitleBar(IntPtr hwnd, bool dark)
        {
            if (hwnd == IntPtr.Zero)
                return;
            try
            {
                int value = dark ? 1 : 0;
                // 20 = DWMWA_USE_IMMERSIVE_DARK_MODE (Windows 10 1903+)
                // 19 = the same attribute on Windows 10 1809.
                int hr = DwmSetWindowAttribute(hwnd, 20, ref value, sizeof(int));
                if (hr != 0)
                    DwmSetWindowAttribute(hwnd, 19, ref value, sizeof(int));
            }
            catch
            {
            }
        }

        [DllImport("user32.dll")]
        public static extern IntPtr SendMessage(IntPtr hWnd, int msg, IntPtr wParam, IntPtr lParam);

        [DllImport("user32.dll")]
        public static extern bool ReleaseCapture();

        public const int WM_NCLBUTTONDOWN = 0x00A1;
        public const int HTCAPTION = 2;
    }
}
