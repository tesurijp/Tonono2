using System;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Runtime.InteropServices;
using Tonono2.SKKEngine;
using static Tonono2.Win32.NativeConstants;

namespace Tonono2.Win32;

public record  class KeyInfo(int VirtualKeyCode, bool IsKeyDown) 
{
    public bool Handled { get; set; }
}

public sealed class KeyboardHook : IDisposable
{
    private IntPtr hookId = IntPtr.Zero;
    private NativeMethods.LowLevelKeyboardProc? hookProc;
    public Action<KeyInfo>? KeyIntercepted { get; set; }

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
                        var args = new KeyInfo((int)hook.vkCode, isKeyDown);
                        KeyIntercepted?.Invoke(args);

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
            DebugLogger.Log($"{Environment.NewLine}{ex.GetType()}{Environment.NewLine}{ex.Message}");
            DebugLogger.Log($"{Environment.NewLine}{ex.StackTrace}");
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
        if (vkCode == SkkConstants.VkG && (NativeMethods.GetKeyState(VK_CONTROL) & 0x8000) != 0)
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
            return str[0];
        }

        return '\0';
    }

}
