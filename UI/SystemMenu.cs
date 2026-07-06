using System;
using System.Diagnostics;
using System.Drawing;
using System.Reflection;
using System.Windows;
using Tonono2.Win32;

namespace Tonono2.UI;

public sealed class SystemMenu : IDisposable
{
    private readonly TrayIcon trayicon;
    private readonly Icon icon = Icon.ExtractAssociatedIcon(Assembly.GetExecutingAssembly().Location)!;
    private InfoWindow? infoWindow;

    public SystemMenu(Window ui, Action restartAction)
    {
        trayicon = new(icon, "Tonono", ui, [
            new("情報", ShowInfoWindow ),
            new("設定", OpenConfig  ),
            new("再起動", restartAction ),
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
        if (infoWindow is { IsLoaded: true })
        {
            infoWindow.Activate();
        }
        else
        {
            infoWindow = new() { DataContext = new InfoViewModel(ConfigLoader.CurrentConfig) };
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
