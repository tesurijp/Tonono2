namespace Tonono2.Win32;

internal static class NativeConstants
{
    internal const int WH_KEYBOARD_LL = 13;
    internal const int WM_KEYDOWN = 0x0100;
    internal const int WM_KEYUP = 0x0101;
    internal const int WM_SYSKEYDOWN = 0x0104;
    internal const int WM_SYSKEYUP = 0x0105;

    internal const int VK_CONTROL = 0x11;
    internal const int VK_MENU = 0x12; // Alt
    internal const int VK_SHIFT = 0x10;
    internal const int VK_LWIN = 0x5B;
    internal const int VK_RWIN = 0x5C;

    internal const uint INPUT_KEYBOARD = 1;
    internal const uint KEYEVENTF_EXTENDEDKEY = 0x0001;
    internal const uint KEYEVENTF_KEYUP = 0x0002;
    internal const uint KEYEVENTF_UNICODE = 0x0004;
    internal const uint KEYEVENTF_SCANCODE = 0x0008;

    internal const uint PROCESS_QUERY_LIMITED_INFORMATION = 0x1000;

    internal const uint MONITOR_DEFAULTTONEAREST = 2;
    internal const uint MAPVK_VK_TO_CHAR = 2;

    internal const int NIM_ADD = 0x00000000;
    internal const int NIM_MODIFY = 0x00000001;
    internal const int NIM_DELETE = 0x00000002;
    internal const int NIF_MESSAGE = 0x00000001;
    internal const int NIF_ICON = 0x00000002;
    internal const int NIF_TIP = 0x00000004;
    internal const int WM_USER = 0x0400;
    internal const int WM_TRAYICON = WM_USER + 1;
    internal const int WM_RBUTTONUP = 0x0205;
    internal const int WM_LBUTTONDBLCLK = 0x0203;
    internal const int TPM_LEFTALIGN = 0x0000;
    internal const int TPM_RETURNCMD = 0x0100;
    internal const int MF_STRING = 0x00000000;
    internal const int MF_SEPARATOR = 0x00000800;
}
