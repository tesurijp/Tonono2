using System;
using System.Text;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Tonono2.SKKEngine;
using Tonono2.UI;

namespace Tonono2;

public partial class App : Application, IDisposable
{
    private SkkController? controller;
    private SystemMenu? trayIcon;
    private TononoUI? ui;

    public override void Initialize() => AvaloniaXamlLoader.Load(this);

    public override void OnFrameworkInitializationCompleted()
    {
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);

        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            desktop.ShutdownMode = ShutdownMode.OnExplicitShutdown;
            desktop.Exit += (_, _) => Dispose();
            controller = new();
            ui = new TononoUI
            {
                DataContext = controller.Engine.Context,
                Icon = SystemMenu.LoadWindowIcon()
            };
            trayIcon = new(
                () => Program.RestartApplication(ApplicationLifetime as IControlledApplicationLifetime),
                () => Program.ShutdownApplication(ApplicationLifetime as IControlledApplicationLifetime));
            controller.Engine.Context.NotifyBufferChanged();
        }

        base.OnFrameworkInitializationCompleted();
    }

    public void Dispose()
    {
        trayIcon?.Dispose();
        ui?.Close();
        controller?.Dispose();
        GC.SuppressFinalize(this);
    }
}
