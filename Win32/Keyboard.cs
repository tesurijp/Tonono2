using System;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Runtime.InteropServices;
using static Tonono2.Win32.NativeConstants;

namespace Tonono2.Win32;

public class KeyboardKeyEventArgs(int vkCode, bool isKeyDown) : EventArgs
{
    public int VirtualKeyCode { get; } = vkCode;
    public bool IsKeyDown { get; } = isKeyDown;
    public bool Handled { get; set; }
}

public sealed class KeyboardHook : IDisposable
{
    private IntPtr hookId = IntPtr.Zero;
    private NativeMethods.LowLevelKeyboardProc? hookProc;

    public event EventHandler<KeyboardKeyEventArgs>? KeyIntercepted;

    public void Install()
    {
        using var curProcess = Process.GetCurrentProcess();
        using var curModule = curProcess.MainModule;
        hookProc = HookCallback;
        hookId = NativeMethods.SetWindowsHookEx(WH_KEYBOARD_LL, hookProc, NativeMethods.GetModuleHandle(curModule?.ModuleName), 0);
    }

    private IntPtr HookCallback(int nCode, IntPtr wParam, IntPtr lParam)
    {
        try
        {
            if (nCode >= 0)
            {
                var msg = wParam.ToInt32();

                var isKeyDown = msg == WM_KEYDOWN || msg == WM_SYSKEYDOWN;
                var isKeyUp = msg == WM_KEYUP || msg == WM_SYSKEYUP;

                if (isKeyDown || isKeyUp)
                {
                    var hook = Marshal.PtrToStructure<NativeMethods.KBDLLHOOKSTRUCT>(lParam);

                    if ((hook.flags & NativeMethods.KbdLlFlags.LLKHF_INJECTED) == 0)
                    {
                        var args = new KeyboardKeyEventArgs((int)hook.vkCode, isKeyDown);
                    KeyIntercepted?.Invoke(this, args);

                        if (args.Handled)
                        {
                            return 1;
                        } 
                    }
                }
            }
        }
        catch (Exception ex)
        {
            var path = Path.Combine(AppContext.BaseDirectory, "exception.txt");
            File.AppendAllText(path, $"{Environment.NewLine}{ex.GetType().ToString()}{Environment.NewLine}{ex.Message}");
            File.AppendAllText(path, $"{Environment.NewLine}{ex.StackTrace}");
        }
        return NativeMethods.CallNextHookEx(hookId, nCode, wParam, lParam);
    }

    public void Dispose()
    {
        if (hookId != IntPtr.Zero)
        {
            NativeMethods.UnhookWindowsHookEx(hookId);
            hookId = IntPtr.Zero;
        }
        GC.SuppressFinalize(this);
    }
}
public static class Keyboard
{
    public static (bool ctrl, bool shift) GetMetaKeyState()
    {
        var ctrlPressed = (NativeMethods.GetKeyState(VK_CONTROL) & 0x8000) != 0;
        var shiftPressed = (NativeMethods.GetKeyState(VK_SHIFT) & 0x8000) != 0;
        return (ctrlPressed, shiftPressed);
    }

    public static char VkToChar(int vkCode, bool shift)
    {
        if (vkCode == 0x47 && (NativeMethods.GetKeyState(VK_CONTROL) & 0x8000) != 0)
        {
            return '\0';
        }

        var keyState = new byte[256];
        NativeMethods.GetKeyboardState(keyState);

        keyState[VK_SHIFT] = (byte)(shift ? 0x80 : 0);
        keyState[VK_CONTROL] = 0;
        keyState[VK_MENU] = 0;

        var sbbuf = new char[10];
        var scanCode = NativeMethods.MapVirtualKey((uint)vkCode, 0);
        var result = NativeMethods.ToUnicode((uint)vkCode, scanCode, keyState, sbbuf, 5, 0);

        if (result > 0)
        {
            var str = new string(sbbuf);
            if (string.IsNullOrEmpty(str))
            {
                return '\0';
            }
            var c = str[0];
            if (char.IsLetter(c))
            {
                return shift ? char.ToUpper(c, CultureInfo.CurrentCulture) : char.ToLower(c, CultureInfo.CurrentCulture);
            }
            return c;
        }

        return '\0';
    }

}
