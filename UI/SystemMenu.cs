using System;
using System.Diagnostics;
using System.Drawing;
using System.Reflection;
using System.Windows;
using Tonono2.SKKEngine;
using Tonono2.Win32;

namespace Tonono2.UI;


public sealed class SystemMenu : IDisposable
{
    private readonly SkkController controller;
    private readonly TrayIcon trayicon;
    private readonly Icon icon = Icon.ExtractAssociatedIcon(Assembly.GetExecutingAssembly().Location)!;
    private InfoWindow? infoWindow;

    public SystemMenu(Window ui, SkkController controller)
    {
        this.controller = controller;
        trayicon = new(icon, "Tonono", ui, [
            new("情報", ShowInfoWindow ),
            new("設定", OpenConfig  ),
            new(null, () => { } ),
            new("終了", Application.Current.Shutdown )
            ]);
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
        if (infoWindow != null && infoWindow.IsLoaded)
        {
            infoWindow.Activate();
        }
        else
        {
            infoWindow = new(controller);
            infoWindow.Show();
        }
    }

    public void Dispose()
    {
        infoWindow?.Close();
        trayicon?.Dispose();
        icon.Dispose();
    }
}
