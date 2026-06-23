using System;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace VoiceInput
{
    public static class DpiHelper
    {
        [DllImport("shcore.dll")]
        private static extern int GetDpiForMonitor(IntPtr hmonitor, MonitorDpiType dpiType, out uint dpiX, out uint dpiY);

        [DllImport("user32.dll")]
        private static extern IntPtr MonitorFromPoint(POINT pt, uint dwFlags);

        [DllImport("user32.dll")]
        private static extern IntPtr MonitorFromWindow(IntPtr hwnd, uint dwFlags);

        [StructLayout(LayoutKind.Sequential)]
        private struct POINT
        {
            public int X;
            public int Y;
        }

        private enum MonitorDpiType
        {
            MDT_EFFECTIVE_DPI = 0,
            MDT_ANGULAR_DPI = 1,
            MDT_RAW_DPI = 2
        }

        private const uint MONITOR_DEFAULTTONEAREST = 2;

        public static double GetDpiScaleForPoint(int x, int y)
        {
            POINT pt = new POINT { X = x, Y = y };
            IntPtr monitor = MonitorFromPoint(pt, MONITOR_DEFAULTTONEAREST);
            return GetDpiScaleForMonitor(monitor);
        }

        public static double GetDpiScaleForWindow(IntPtr hwnd)
        {
            IntPtr monitor = MonitorFromWindow(hwnd, MONITOR_DEFAULTTONEAREST);
            return GetDpiScaleForMonitor(monitor);
        }

        private static double GetDpiScaleForMonitor(IntPtr monitor)
        {
            try
            {
                int hr = GetDpiForMonitor(monitor, MonitorDpiType.MDT_EFFECTIVE_DPI, out uint dpiX, out _);
                if (hr == 0 && dpiX > 0)
                {
                    return dpiX / 96.0;
                }
            }
            catch { }
            return 1.0;
        }

        public static Screen GetScreenForPoint(int x, int y)
        {
            foreach (var screen in Screen.AllScreens)
            {
                if (screen.WorkingArea.Contains(x, y))
                    return screen;
            }
            return Screen.PrimaryScreen ?? Screen.AllScreens[0];
        }
    }
}
