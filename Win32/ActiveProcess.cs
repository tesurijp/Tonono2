using System;
using static Tonono2.Win32.NativeConstants;

namespace Tonono2.Win32;

public static class ActiveProcess
{

    public  static string GetActiveProcessPath()
    {
        var hwnd = NativeMethods.GetForegroundWindow();
        if (hwnd != IntPtr.Zero)
        {
            NativeMethods.GetWindowThreadProcessId(hwnd, out var pid);
            var hProcess = NativeMethods.OpenProcess(PROCESS_QUERY_LIMITED_INFORMATION, false, pid);
            if (hProcess != IntPtr.Zero)
            {
                try
                {
                    var buffer = new char[1024];
                    var size = (uint)buffer.Length;
                    if (NativeMethods.QueryFullProcessImageName(hProcess, 0, buffer, ref size))
                    {
                        return new string(buffer, 0, (int)size);
                    }
                }
                finally
                {
                    NativeMethods.CloseHandle(hProcess);
                }
            }
        }
        return "";
    }
}