using System;
using System.Security.Principal;
using System.Windows;
using static Tonono2.Win32.NativeConstants;

namespace Tonono2.Win32;

public static class WindowPositioner
{
    public static (double x, double y) GetTargetPosition(double dpiScaleX, double dpiScaleY, double actualWidth, double actualHeight)
    {
        var gui = new NativeMethods.GUITHREADINFO { cbSize = System.Runtime.InteropServices.Marshal.SizeOf<NativeMethods.GUITHREADINFO>() };
        var pt = new NativeMethods.POINT();
        var hasCaret = false;

        if (NativeMethods.GetGUIThreadInfo(0, ref gui) && gui.hwndCaret != IntPtr.Zero)
        {
            pt = new NativeMethods.POINT { X = gui.rcCaret.Left, Y = gui.rcCaret.Bottom };
            NativeMethods.ClientToScreen(gui.hwndCaret, ref pt);
            hasCaret = true;
        }
        else
        {
            NativeMethods.GetCursorPos(out pt);
        }

        var hMonitor = NativeMethods.MonitorFromPoint(pt, MONITOR_DEFAULTTONEAREST);
        var mi = new NativeMethods.MONITORINFO { cbSize = System.Runtime.InteropServices.Marshal.SizeOf<NativeMethods.MONITORINFO>() };

        if (NativeMethods.GetMonitorInfo(hMonitor, ref mi))
        {
            var x = pt.X / dpiScaleX;
            var y = (pt.Y + (hasCaret ? 5 : 0)) / dpiScaleY;

            var workLeft = mi.rcWork.Left / dpiScaleX;
            var workTop = mi.rcWork.Top / dpiScaleY;
            var workRight = mi.rcWork.Right / dpiScaleX;
            var workBottom = mi.rcWork.Bottom / dpiScaleY;

            if (x + actualWidth > workRight)
            {
                x = workRight - actualWidth;
            }
            if (x < workLeft)
            {
                x = workLeft;
            }

            if (y + actualHeight > workBottom)
            {
                y = (pt.Y / dpiScaleY) - actualHeight - (hasCaret ? 5 : 0);
            }
            if (y < workTop)
            {
                y = workTop;
            }

            return (x, y);
        }

        return (double.NaN, double.NaN);
    }
    public static void SetNonActiveWindow(Window window)
    {
        var helper = new System.Windows.Interop.WindowInteropHelper(window);
        var hwnd = helper.Handle;
        var currentStyle = NativeMethods.GetWindowLongPtr(hwnd, GWL_EXSTYLE);
        var newStyle = new IntPtr(currentStyle | WS_EX_NOACTIVATE | WS_EX_TOOLWINDOW);
        NativeMethods.SetWindowLongPtr(hwnd, GWL_EXSTYLE, newStyle);
    }
}
