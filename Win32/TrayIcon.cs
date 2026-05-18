using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using static Tonono2.Win32.NativeConstants;

namespace Tonono2.Win32;

public record class MenuItem(string? Title, Action Action);

internal sealed class TrayIcon : IDisposable
{
    private readonly IntPtr _hWnd;
    private const int _uID = 1;
    private bool disposed;

    private readonly MenuItem[] menuItems;

    public TrayIcon(Icon icon, string title, Window ui, IEnumerable<MenuItem> items)
    {
        _hWnd = new WindowInteropHelper(ui).EnsureHandle();
        var source = HwndSource.FromHwnd(_hWnd)!;
        source.AddHook(WndProc);
        menuItems = [.. items];
        AddTrayIcon(icon, title);
    }

    private void AddTrayIcon(Icon icon, string title)
    {
        var nid = new NativeMethods.NOTIFYICONDATA();
        nid.cbSize = Marshal.SizeOf(nid);
        nid.hWnd = _hWnd;
        nid.uID = _uID;
        nid.uFlags = NIF_MESSAGE | NIF_ICON | NIF_TIP;
        nid.uCallbackMessage = WM_TRAYICON;
        nid.hIcon = icon.Handle;
        nid.szTip = title;
        NativeMethods.Shell_NotifyIcon(NIM_ADD, ref nid);
    }

    private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (msg == WM_TRAYICON)
        {
            var eventId = (int)lParam.ToInt64() & 0xFFFF;
            if (eventId == WM_RBUTTONUP)
            {
                ShowContextMenu();
            }
        }
        return IntPtr.Zero;
    }

    private void ShowContextMenu()
    {

        const uint idxBaase = 1000;

        var hMenu = NativeMethods.CreatePopupMenu();
        var itemselector = menuItems.Select((item, idx) => ((uint)(idxBaase + idx), item.Title == null ? MF_SEPARATOR : MF_STRING, item.Title ?? ""));
        foreach (var (idx, type, title) in itemselector)
        {
            NativeMethods.AppendMenu(hMenu, type, idx, title);
        }

        NativeMethods.GetCursorPos(out var pt);

        NativeMethods.SetForegroundWindow(_hWnd);

        var selectedId = NativeMethods.TrackPopupMenu(hMenu, (uint)(TPM_LEFTALIGN | TPM_RETURNCMD), pt.X, pt.Y, 0, _hWnd, IntPtr.Zero) - idxBaase;

        NativeMethods.DestroyMenu(hMenu);

        if (selectedId >= 0 && selectedId < menuItems.Length)
        {
            menuItems[selectedId].Action();
        }
    }

    public void Dispose()
    {
        if (!disposed)
        {
            var nid = new NativeMethods.NOTIFYICONDATA { cbSize = Marshal.SizeOf<NativeMethods.NOTIFYICONDATA>(), hWnd = _hWnd, uID = _uID };
            NativeMethods.Shell_NotifyIcon(NIM_DELETE, ref nid);
            disposed = true;
        }
    }
}
