using System;
using System.Windows;
using Tonono2.SKKEngine;
using Tonono2.UI;

namespace Tonono2;

public partial class App : Application, IDisposable
{
    private SkkController? controller;
    private SystemMenu? trayIcon;
    private TononoUI? ui;

    public App()
    {
        System.Text.Encoding.RegisterProvider(System.Text.CodePagesEncodingProvider.Instance);
    }

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        controller = new();
        ui = new() { DataContext = controller.Engine.Context };
        trayIcon = new(ui);
    }

    protected override void OnExit(ExitEventArgs e)
    {
        Dispose();
        base.OnExit(e);
    }

    public void Dispose()
    {
        trayIcon?.Dispose();
        ui?.Close();
        controller?.Dispose();
        GC.SuppressFinalize(this);
    }
}
