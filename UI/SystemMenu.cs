using System;
using System.Diagnostics;
using Avalonia.Controls;
using Avalonia.Platform;

namespace Tonono2.UI;

public sealed class SystemMenu : IDisposable
{
    private readonly TrayIcon trayicon;
    private InfoWindow? infoWindow;

    public SystemMenu(Action restartAction, Action shutdownAction)
    {
        trayicon = new TrayIcon
        {
            Icon = LoadWindowIcon(),
            ToolTipText = "Tonono",
            Menu = CreateMenu(restartAction, shutdownAction),
            IsVisible = true
        };
    }

    public static WindowIcon LoadWindowIcon()
    {
        using var stream = AssetLoader.Open(new Uri("avares://Tonono2/TONONO.ICO"));
        return new WindowIcon(stream);
    }

    private NativeMenu CreateMenu(Action restartAction, Action shutdownAction)
    {
        var menu = new NativeMenu
        {
            makeMenu("情報", ShowInfoWindow),
            makeMenu("設定", OpenConfig),
            makeMenu("再起動", restartAction),
            new NativeMenuItemSeparator(),
            makeMenu("終了", shutdownAction)
        };
        return menu;
        static NativeMenuItem makeMenu(string header, Action act)
        {
            var menu = new NativeMenuItem { Header = header };
            menu.Click += (_, _) => act();
            return menu;
        }
    }

    private static void OpenConfig()
    {
        try
        {
            Process.Start(new ProcessStartInfo(AppConfig.ConfigPath) { UseShellExecute = true });
        }
        catch (Exception ex)
        {
            DebugLogger.Log($"Failed to open config.yaml: {ex.Message}");
        }
    }

    private void ShowInfoWindow()
    {
        if (infoWindow is { IsVisible: true })
        {
            infoWindow.Activate();
        }
        else
        {
            infoWindow = new() { DataContext = new InfoViewModel(ConfigLoader.CurrentConfig) };
            infoWindow.Closed += (_, _) => infoWindow = null;
            infoWindow.Show();
        }
    }

    public void Dispose()
    {
        infoWindow?.Close();
        trayicon.IsVisible = false;
        trayicon.Dispose();
    }
}
