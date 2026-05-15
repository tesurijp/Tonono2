using System;
using System.Windows;
using Tonono2.SKKEngine;
using Tonono2.UI;

namespace Tonono2;

[System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1001:破棄可能なフィールドを所有する型は、破棄可能でなければなりません", Justification = "<保留中>")]
public partial class App : Application
{
    private SkkController? controller;
    private TrayIconManager? trayIcon;
    private TononoUI? ui;

    public App()
    {
        System.Text.Encoding.RegisterProvider(System.Text.CodePagesEncodingProvider.Instance);
    }

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        controller = new SkkController();
        ui = new TononoUI(controller);
        trayIcon = new TrayIconManager(ui , controller);
    }

    protected override void OnExit(ExitEventArgs e)
    {
        trayIcon?.Dispose();
        controller?.Dispose();
        base.OnExit(e);
    }
}
