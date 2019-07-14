using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using System.Security;
using System.Text;
using System.Threading.Tasks;

namespace Ricimon.WindowKeeper.Common.WinApiBridge
{
    // adapted from https://archive.codeplex.com/?p=persistentwindows
    // and https://referencesource.microsoft.com/#System.Windows.Forms/winforms/Managed/System/WinForms/SafeNativeMethods.cs
    // and https://referencesource.microsoft.com/#System.Windows.Forms/winforms/Managed/System/WinForms/UnsafeNativeMethods.cs
    // and https://stackoverflow.com/questions/5020559/screen-allscreen-is-not-giving-the-correct-monitor-count

    [StructLayout(LayoutKind.Sequential)]
    public struct RECT
    {
        public int left;
        public int top;
        public int right;
        public int bottom;

        public static bool operator ==(RECT r1, RECT r2)
        {
            return r1.left   == r2.left  &&
                   r1.top    == r2.top   &&
                   r1.right  == r2.right &&
                   r1.bottom == r2.bottom;
        }

        public static bool operator !=(RECT r1, RECT r2)
        {
            return !(r1 == r2);
        }

        public override bool Equals(object obj)
        {
            return obj is RECT rect &&
                   left == rect.left &&
                   top == rect.top &&
                   right == rect.right &&
                   bottom == rect.bottom;
        }

        public override int GetHashCode()
        {
            var hashCode = -971476797;
            hashCode = hashCode * -1521134295 + left.GetHashCode();
            hashCode = hashCode * -1521134295 + top.GetHashCode();
            hashCode = hashCode * -1521134295 + right.GetHashCode();
            hashCode = hashCode * -1521134295 + bottom.GetHashCode();
            return hashCode;
        }
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct POINT
    {
        public int x;
        public int y;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct WINDOWPLACEMENT
    {
        public int length;
        public int flags;
        public int showCmd;
        public POINT ptMinPosition;
        public POINT ptMaxPosition;
        public RECT rcNormalPosition;

        public enum WindowStatus
        {
            Unknown,
            Normal,
            Minimized,
            Maximized,
        }
        public WindowStatus windowStatus
        {
            get
            {
                switch(showCmd)
                {
                    case 1:
                        return WindowStatus.Normal;
                    case 2:
                        return WindowStatus.Minimized;
                    case 3:
                        return WindowStatus.Maximized;
                    default:
                        return WindowStatus.Unknown;
                }
            }
        }
    }

    [SuppressUnmanagedCodeSecurity]
    internal static class NativeMethods
    {
        // Intentionally CharSet.Ansi
        [DllImport("user32.dll", EntryPoint = "RegisterWindowMessageA", CharSet = CharSet.Ansi, SetLastError = true, ExactSpelling = true)]
        public static extern int RegisterWindowMessage(string lpString);

        // Intentionally CharSet.Ansi
        [DllImport("user32.dll", CharSet = CharSet.Ansi, SetLastError = true, ExactSpelling = true)]
        public static extern int DeregisterShellHookWindow(IntPtr hWnd);

        // Intentionally CharSet.Ansi
        [DllImport("user32.dll", CharSet = CharSet.Ansi, SetLastError = true, ExactSpelling = true)]
        public static extern int RegisterShellHookWindow(IntPtr hWnd);

        [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        [ResourceExposure(ResourceScope.None)]
        public static extern bool EnumWindows(EnumThreadWindowsCallback callback, IntPtr extraData);
        internal delegate bool EnumThreadWindowsCallback(IntPtr hWnd, IntPtr lParam);

        [DllImport("user32.dll", ExactSpelling = true, CharSet = CharSet.Auto, SetLastError = true)]
        [ResourceExposure(ResourceScope.None)]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool GetWindowRect(IntPtr hWnd, [In, Out] ref RECT lpRect);

        [DllImport("user32.dll", ExactSpelling = true, CharSet = CharSet.Auto)]
        [ResourceExposure(ResourceScope.None)]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool GetWindowPlacement(IntPtr hWnd, ref WINDOWPLACEMENT lpwndpl);

        [DllImport("user32.dll", CharSet = CharSet.Auto)]
        [ResourceExposure(ResourceScope.None)]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool IsIconic(IntPtr hWnd);

        [DllImport("user32.dll", CharSet = CharSet.Auto)]
        [ResourceExposure(ResourceScope.None)]
        public static extern int GetWindowText(IntPtr hWnd, StringBuilder lpString, int nMaxCount);

        [DllImport("user32.dll", SetLastError = true, CharSet = CharSet.Auto)]
        [ResourceExposure(ResourceScope.None)]
        public static extern int GetWindowTextLength(IntPtr hWnd);

        [DllImport("user32.dll", ExactSpelling = true, CharSet = CharSet.Auto, SetLastError = true)]
        [ResourceExposure(ResourceScope.Process)]
        public static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint lpdwProcessId);

        [DllImport("user32.dll")]
        public static extern uint GetWindowThreadProcessId(IntPtr hWnd, IntPtr voidProcessId);

        [DllImport("user32.dll", CharSet = CharSet.Auto, CallingConvention = CallingConvention.StdCall, ExactSpelling = true, SetLastError = true)]
        public static extern void MoveWindow(IntPtr hWnd, int X, int Y, int nWidth, int nHeight, bool bRepaint);

        [DllImport("user32.dll", SetLastError = false)]
        public static extern IntPtr SetWinEventHook(WinEventHook.SWEH_Events eventMin, WinEventHook.SWEH_Events eventMax,
                                                    IntPtr hmodWinEventProc, WinEventHook.WinEventDelegate lpfnWinEventProc,
                                                    uint idProcess, uint idThread, WinEventHook.SWEH_dwFlags dwFlags);

        [DllImport("user32.dll", SetLastError = false)]
        public static extern bool UnhookWinEvent(IntPtr hWinEventHook);

        [DllImport("user32.dll")]
        public static extern bool EnumDisplayMonitors(IntPtr hdc, IntPtr lpRect, MonitorEnumProc callback, int dwData);

        public delegate bool MonitorEnumProc(IntPtr hDesktop, IntPtr hdc, ref RECT pRect, int dwData);
    }
}
