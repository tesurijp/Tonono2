using System;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using Tonono2.SKKEngine;
using Tonono2.Win32;
using System.Drawing;
using System.Reflection;

namespace Tonono2.UI;

public sealed class TrayIconManager : IDisposable
{
    private readonly IntPtr _hWnd;
    private readonly SkkController _controller;
    private readonly int _uID = 1;
    private bool _disposed;
    private InfoWindow? _infoWindow;
    private readonly Icon icon = Icon.ExtractAssociatedIcon(Assembly.GetExecutingAssembly().Location)!;

    public TrayIconManager(Window ui , SkkController controller)
    {
        _hWnd = new WindowInteropHelper(ui).EnsureHandle();
        HwndSource source = HwndSource.FromHwnd(_hWnd)!;
        source.AddHook(WndProc);

        _controller = controller;
        AddTrayIcon();
    }
    private void AddTrayIcon()
    {
        var nid = new NativeMethods.NOTIFYICONDATA();
        nid.cbSize = Marshal.SizeOf(nid);
        nid.hWnd = _hWnd;
        nid.uID = _uID;
        nid.uFlags = NativeMethods.NIF_MESSAGE | NativeMethods.NIF_ICON | NativeMethods.NIF_TIP;
        nid.uCallbackMessage = NativeMethods.WM_TRAYICON;
        nid.hIcon = icon.Handle;
        nid.szTip = "Tonono SKK";
        NativeMethods.Shell_NotifyIcon(NativeMethods.NIM_ADD, ref nid);
    }

    private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (msg == NativeMethods.WM_TRAYICON)
        {
            var eventId = (int)lParam.ToInt64() & 0xFFFF;
            if (eventId == NativeMethods.WM_RBUTTONUP)
            {
                ShowContextMenu();
            }
        }
        return IntPtr.Zero;
    }

    private void ShowContextMenu()
    {
        var hMenu = NativeMethods.CreatePopupMenu();
        NativeMethods.AppendMenu(hMenu, (uint)NativeMethods.MF_STRING, 1001, "Information");
        NativeMethods.AppendMenu(hMenu, (uint)NativeMethods.MF_SEPARATOR, 0, "");
        NativeMethods.AppendMenu(hMenu, (uint)NativeMethods.MF_STRING, 1002, "Exit");

        NativeMethods.GetCursorPos(out var pt);

        NativeMethods.SetForegroundWindow(_hWnd);

        var selectedId = NativeMethods.TrackPopupMenu(
            hMenu,
            (uint)(NativeMethods.TPM_LEFTALIGN | NativeMethods.TPM_RETURNCMD),
            pt.X, pt.Y, 0, _hWnd, IntPtr.Zero);

        NativeMethods.DestroyMenu(hMenu);

        switch (selectedId)
        {
            case 1001:
                ShowInfoWindow();
                break;
            case 1002:
                Application.Current.Shutdown();
                break;
        }
    }

    private void ShowInfoWindow()
    {
        if (_infoWindow != null && _infoWindow.IsLoaded)
        {
            _infoWindow.Activate();
            return;
        }

        _infoWindow = new InfoWindow(_controller);
        _infoWindow.Show();
    }

    public void Dispose()
    {
        if (!_disposed)
        {
            _infoWindow?.Close();
            var nid = new NativeMethods.NOTIFYICONDATA { cbSize = Marshal.SizeOf<NativeMethods.NOTIFYICONDATA>(), hWnd = _hWnd, uID = _uID };
            NativeMethods.Shell_NotifyIcon(NativeMethods.NIM_DELETE, ref nid);
            icon.Dispose();
            _disposed = true;
        }
    }

}
