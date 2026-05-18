using System;
using System.Diagnostics;
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
    private IntPtr _hookId = IntPtr.Zero;
    private NativeMethods.LowLevelKeyboardProc? _proc;

    public event EventHandler<KeyboardKeyEventArgs>? KeyIntercepted;

    public void Install()
    {
        _proc = HookCallback;
        using var curProcess = Process.GetCurrentProcess();
        using var curModule = curProcess.MainModule;
        _hookId = NativeMethods.SetWindowsHookEx(WH_KEYBOARD_LL, _proc, NativeMethods.GetModuleHandle(curModule?.ModuleName), 0);
    }

    private IntPtr HookCallback(int nCode, IntPtr wParam, IntPtr lParam)
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

        return NativeMethods.CallNextHookEx(_hookId, nCode, wParam, lParam);
    }

    public void Dispose()
    {
        if (_hookId != IntPtr.Zero)
        {
            NativeMethods.UnhookWindowsHookEx(_hookId);
            _hookId = IntPtr.Zero;
        }
        GC.SuppressFinalize(this);
    }
}
