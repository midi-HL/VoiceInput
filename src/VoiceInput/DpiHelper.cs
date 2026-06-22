using System;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;

namespace VoiceInput
{
    public static class DpiHelper
    {
        #region Win32 API

        [DllImport("user32.dll")]
        private static extern IntPtr MonitorFromPoint(POINT pt, uint dwFlags);

        [DllImport("user32.dll")]
        private static extern IntPtr MonitorFromWindow(IntPtr hwnd, uint dwFlags);

        [DllImport("Shcore.dll")]
        private static extern int GetDpiForMonitor(IntPtr hmonitor, int dpiType, out uint dpiX, out uint dpiY);

        [DllImport("user32.dll")]
        private static extern bool GetMonitorInfo(IntPtr hmonitor, ref MONITORINFO lpmi);

        [DllImport("user32.dll")]
        private static extern bool GetCursorPos(out POINT lpPoint);

        [StructLayout(LayoutKind.Sequential)]
        private struct POINT
        {
            public int X;
            public int Y;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct MONITORINFO
        {
            public int cbSize;
            public RECT rcMonitor;
            public RECT rcWork;
            public uint dwFlags;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct RECT
        {
            public int Left;
            public int Top;
            public int Right;
            public int Bottom;
        }

        private const int MDT_EFFECTIVE_DPI = 0;
        private const uint MONITOR_DEFAULTTONEAREST = 2;
        private const uint MONITOR_DEFAULTTOPRIMARY = 1;

        #endregion

        /// <summary>
        /// 获取鼠标当前所在显示器的 DPI
        /// </summary>
        public static double GetCurrentMonitorDpi()
        {
            try
            {
                GetCursorPos(out POINT cursorPos);
                IntPtr monitor = MonitorFromPoint(cursorPos, MONITOR_DEFAULTTONEAREST);
                return GetMonitorDpi(monitor);
            }
            catch
            {
                return 96.0; // 默认 100% 缩放
            }
        }

        /// <summary>
        /// 获取指定窗口所在显示器的 DPI
        /// </summary>
        public static double GetWindowMonitorDpi(Window window)
        {
            try
            {
                var hwnd = new WindowInteropHelper(window).Handle;
                IntPtr monitor = MonitorFromWindow(hwnd, MONITOR_DEFAULTTONEAREST);
                return GetMonitorDpi(monitor);
            }
            catch
            {
                return 96.0;
            }
        }

        /// <summary>
        /// 获取指定显示器的 DPI
        /// </summary>
        public static double GetMonitorDpi(IntPtr monitor)
        {
            try
            {
                int result = GetDpiForMonitor(monitor, MDT_EFFECTIVE_DPI, out uint dpiX, out uint dpiY);
                if (result == 0) // S_OK
                {
                    return dpiX;
                }
                return 96.0;
            }
            catch
            {
                return 96.0;
            }
        }

        /// <summary>
        /// 获取鼠标当前所在显示器的工作区（逻辑坐标）
        /// </summary>
        public static Rect GetCurrentMonitorWorkArea()
        {
            try
            {
                GetCursorPos(out POINT cursorPos);
                IntPtr monitor = MonitorFromPoint(cursorPos, MONITOR_DEFAULTTONEAREST);
                return GetMonitorWorkArea(monitor);
            }
            catch
            {
                // 回退到主显示器
                return new Rect(
                    SystemParameters.WorkArea.Left,
                    SystemParameters.WorkArea.Top,
                    SystemParameters.WorkArea.Width,
                    SystemParameters.WorkArea.Height);
            }
        }

        /// <summary>
        /// 获取指定显示器的工作区（逻辑坐标）
        /// </summary>
        public static Rect GetMonitorWorkArea(IntPtr monitor)
        {
            try
            {
                var monitorInfo = new MONITORINFO { cbSize = Marshal.SizeOf<MONITORINFO>() };
                if (GetMonitorInfo(monitor, ref monitorInfo))
                {
                    double dpi = GetMonitorDpi(monitor);
                    double scale = dpi / 96.0;

                    return new Rect(
                        monitorInfo.rcWork.Left / scale,
                        monitorInfo.rcWork.Top / scale,
                        (monitorInfo.rcWork.Right - monitorInfo.rcWork.Left) / scale,
                        (monitorInfo.rcWork.Bottom - monitorInfo.rcWork.Top) / scale);
                }

                return new Rect(
                    SystemParameters.WorkArea.Left,
                    SystemParameters.WorkArea.Top,
                    SystemParameters.WorkArea.Width,
                    SystemParameters.WorkArea.Height);
            }
            catch
            {
                return new Rect(
                    SystemParameters.WorkArea.Left,
                    SystemParameters.WorkArea.Top,
                    SystemParameters.WorkArea.Width,
                    SystemParameters.WorkArea.Height);
            }
        }

        /// <summary>
        /// 计算 HUD 窗口在当前显示器底部居中的位置
        /// </summary>
        public static Point CalculateHudPosition(double hudWidth, double hudHeight, double bottomOffset)
        {
            Rect workArea = GetCurrentMonitorWorkArea();
            double dpi = GetCurrentMonitorDpi();

            // 将底部偏移从 DIU 转换为逻辑像素
            double logicalOffset = bottomOffset;

            double x = workArea.Left + (workArea.Width - hudWidth) / 2;
            double y = workArea.Top + workArea.Height - hudHeight - logicalOffset;

            return new Point(x, y);
        }

        /// <summary>
        /// 获取当前 DPI 缩放比例（1.0 = 100%）
        /// </summary>
        public static double GetCurrentDpiScale()
        {
            return GetCurrentMonitorDpi() / 96.0;
        }

        /// <summary>
        /// 将设备无关单位（DIU）转换为物理像素
        /// </summary>
        public static double DiuToPixels(double diu)
        {
            return diu * GetCurrentDpiScale();
        }

        /// <summary>
        /// 将物理像素转换为设备无关单位（DIU）
        /// </summary>
        public static double PixelsToDiu(double pixels)
        {
            return pixels / GetCurrentDpiScale();
        }
    }
}
