using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Threading;

namespace WindowEvader
{
    internal static class Program
    {
        private const int ProximityThreshold = 70;
        private const int BaseMoveDistance = 60;
        private const int UpdateIntervalMilliseconds = 15;

        private static Rectangle _desktopBounds;
        private static readonly uint CurrentProcessId = (uint)Process.GetCurrentProcess().Id;

        private static void Main()
        {
            IntPtr consoleWindowHandle = NativeMethods.GetConsoleWindow();
            NativeMethods.ShowWindow(consoleWindowHandle, 0); // Hide console window

            _desktopBounds = GetAllMonitorBounds();
            Point lastCursorPosition = GetCursorPosition();

            while (true)
            {
                Point currentCursorPosition = GetCursorPosition();
                double cursorSpeed = CalculateDistance(currentCursorPosition, lastCursorPosition) / (UpdateIntervalMilliseconds / 1000.0);

                foreach (IntPtr windowHandle in GetAllWindows())
                {
                    ProcessWindow(windowHandle, currentCursorPosition, cursorSpeed);
                }

                lastCursorPosition = currentCursorPosition;
                Thread.Sleep(UpdateIntervalMilliseconds);
            }
        }

        private static void ProcessWindow(IntPtr windowHandle, Point cursorPosition, double cursorSpeed)
        {
            if (!IsEligibleWindow(windowHandle))
            {
                return;
            }

            if (!NativeMethods.GetWindowRect(windowHandle, out NativeMethods.RECT r))
            {
                return;
            }

            var windowRect = new Rectangle(r.Left, r.Top, r.Right - r.Left, r.Bottom - r.Top);
            var proximityZone = Rectangle.Inflate(windowRect, ProximityThreshold, ProximityThreshold);

            if (!proximityZone.Contains(cursorPosition))
            {
                return;
            }

            MoveWindowAwayFromCursor(windowHandle, windowRect, cursorPosition, cursorSpeed);
        }

        private static bool IsEligibleWindow(IntPtr windowHandle)
        {
            if (!NativeMethods.IsWindowVisible(windowHandle) || NativeMethods.GetWindowTextLength(windowHandle) == 0)
            {
                return false;
            }

            NativeMethods.GetWindowThreadProcessId(windowHandle, out uint processId);
            if (processId == CurrentProcessId)
            {
                return false;
            }

            return !IsMaximized(windowHandle);
        }

        private static void MoveWindowAwayFromCursor(IntPtr windowHandle, Rectangle windowRect, Point cursorPosition, double cursorSpeed)
        {
            var windowCenter = new Point(windowRect.Left + windowRect.Width / 2, windowRect.Top + windowRect.Height / 2);
            double distanceToCursor = CalculateDistance(cursorPosition, windowCenter);

            int moveDistance = Math.Max(BaseMoveDistance, (int)(cursorSpeed * 0.05 + (ProximityThreshold - distanceToCursor)));

            int dx = windowCenter.X - cursorPosition.X;
            int dy = windowCenter.Y - cursorPosition.Y;
            double vectorLength = Math.Sqrt(dx * dx + dy * dy);
            if (vectorLength == 0) vectorLength = 1;

            int newX = windowRect.Left + (int)(moveDistance * dx / vectorLength);
            int newY = windowRect.Top + (int)(moveDistance * dy / vectorLength);

            // Teleport window to the opposite side if it hits a screen boundary
            if (newX < _desktopBounds.Left)
                newX = _desktopBounds.Right - windowRect.Width;
            else if (newX + windowRect.Width > _desktopBounds.Right)
                newX = _desktopBounds.Left;

            if (newY < _desktopBounds.Top)
                newY = _desktopBounds.Bottom - windowRect.Height;
            else if (newY + windowRect.Height > _desktopBounds.Bottom)
                newY = _desktopBounds.Top;

            NativeMethods.MoveWindow(windowHandle, newX, newY, windowRect.Width, windowRect.Height, true);
        }

        private static IEnumerable<IntPtr> GetAllWindows()
        {
            var windows = new List<IntPtr>();
            NativeMethods.EnumWindows((hWnd, _) => { windows.Add(hWnd); return true; }, IntPtr.Zero);
            return windows;
        }

        private static double CalculateDistance(Point a, Point b)
        {
            int dx = a.X - b.X;
            int dy = a.Y - b.Y;
            return Math.Sqrt(dx * dx + dy * dy);
        }

        private static bool IsMaximized(IntPtr windowHandle)
        {
            var placement = new NativeMethods.WINDOWPLACEMENT();
            placement.length = Marshal.SizeOf(placement);
            return NativeMethods.GetWindowPlacement(windowHandle, ref placement) && placement.showCmd == 3; // SW_MAXIMIZE
        }

        private static Point GetCursorPosition()
        {
            NativeMethods.GetCursorPos(out NativeMethods.POINT p);
            return new Point(p.X, p.Y);
        }

        private static Rectangle GetAllMonitorBounds()
        {
            var bounds = Rectangle.Empty;
            NativeMethods.MonitorEnumProc callback = (IntPtr hMon, IntPtr hdc, ref NativeMethods.RECT rect, IntPtr d) =>
            {
                var mi = new NativeMethods.MONITORINFOEX();
                mi.cbSize = Marshal.SizeOf(mi);
                if (NativeMethods.GetMonitorInfo(hMon, ref mi))
                {
                    var monitorRect = new Rectangle(mi.rcWork.Left, mi.rcWork.Top,
                        mi.rcWork.Right - mi.rcWork.Left, mi.rcWork.Bottom - mi.rcWork.Top);
                    bounds = Rectangle.Union(bounds, monitorRect);
                }
                return true;
            };
            NativeMethods.EnumDisplayMonitors(IntPtr.Zero, IntPtr.Zero, callback, IntPtr.Zero);
            return bounds;
        }
    }

    internal static partial class NativeMethods
    {
        internal delegate bool EnumWindowsProc(IntPtr hWnd, IntPtr lParam);
        internal delegate bool MonitorEnumProc(IntPtr hMonitor, IntPtr hdcMonitor, ref RECT lprcMonitor, IntPtr dwData);

        [DllImport("kernel32.dll")]
        internal static extern IntPtr GetConsoleWindow();

        [DllImport("user32.dll")]
        internal static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

        [DllImport("user32.dll")]
        internal static extern bool EnumWindows(EnumWindowsProc lpEnumFunc, IntPtr lParam);

        [DllImport("user32.dll")]
        internal static extern bool GetWindowRect(IntPtr hWnd, out RECT lpRect);

        [DllImport("user32.dll")]
        internal static extern bool MoveWindow(IntPtr hWnd, int X, int Y, int nWidth, int nHeight, bool bRepaint);

        [DllImport("user32.dll")]
        internal static extern bool IsWindowVisible(IntPtr hWnd);

        [DllImport("user32.dll")]
        internal static extern int GetWindowTextLength(IntPtr hWnd);

        [DllImport("user32.dll")]
        internal static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint lpdwProcessId);

        [DllImport("user32.dll")]
        internal static extern bool GetCursorPos(out POINT lpPoint);

        [DllImport("user32.dll")]
        internal static extern bool EnumDisplayMonitors(IntPtr hdc, IntPtr lprcClip, MonitorEnumProc lpfnEnum, IntPtr dwData);

        [DllImport("user32.dll", CharSet = CharSet.Auto)]
        internal static extern bool GetMonitorInfo(IntPtr hMonitor, ref MONITORINFOEX lpmi);

        [DllImport("user32.dll")]
        internal static extern bool GetWindowPlacement(IntPtr hWnd, ref WINDOWPLACEMENT lpwndpl);

        [StructLayout(LayoutKind.Sequential)]
        internal struct RECT { public int Left, Top, Right, Bottom; }

        [StructLayout(LayoutKind.Sequential)]
        internal struct POINT { public int X, Y; }

        [StructLayout(LayoutKind.Sequential)]
        internal struct WINDOWPLACEMENT
        {
            public int length;
            public int flags;
            public int showCmd;
            public POINT ptMinPosition;
            public POINT ptMaxPosition;
            public RECT rcNormalPosition;
        }

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
        internal struct MONITORINFOEX
        {
            public int cbSize;
            public RECT rcMonitor;
            public RECT rcWork;
            public uint dwFlags;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)]
            public string szDevice;
        }
    }
}