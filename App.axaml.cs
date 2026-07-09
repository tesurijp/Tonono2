using System;
using System.Diagnostics;
using System.Text;
using System.Threading;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Tonono2.SKKEngine;
using Tonono2.UI;

namespace Tonono2;

public partial class App : Application, IDisposable
{
    private const string SingleInstanceMutexName = "{E11B24F6-0499-4E83-A781-D847BDCD673B}";
    private const string RestartArgument = "--restart";

    private Mutex? mutex;
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

            if (AcquireSingleInstance(desktop.Args ?? []))
            {
                controller = new();
                ui = new TononoUI
                {
                    DataContext = controller.Engine.Context,
                    Icon = SystemMenu.LoadWindowIcon()
                };
                trayIcon = new(RestartApplication, ShutdownApplication);
                controller.Engine.Context.NotifyBufferChanged();
            }
            else
            {
                desktop.Shutdown();
            }
        }

        base.OnFrameworkInitializationCompleted();
    }

    public void Dispose()
    {
        trayIcon?.Dispose();
        ui?.Close();
        controller?.Dispose();
        mutex?.ReleaseMutex();
        mutex?.Dispose();
        GC.SuppressFinalize(this);
    }

    private bool AcquireSingleInstance(string[] args)
    {
        mutex = new Mutex(false, SingleInstanceMutexName, out var createdNew);
        if (createdNew || Array.IndexOf(args, RestartArgument) >= 0)
        {
            if (mutex.WaitOne(5000))
            {
                return true;
            }
        }

        mutex.Dispose();
        mutex = null;
        return false;
    }

    private void RestartApplication()
    {
        try
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = Environment.ProcessPath!,
                UseShellExecute = false,
                WorkingDirectory = AppContext.BaseDirectory,
                CreateNoWindow = true,
                Arguments = RestartArgument
            });
        }
        catch (Exception ex)
        {
            DebugLogger.Log($"Failed to restart application: {ex.Message}");
            return;
        }

        ShutdownApplication();
    }

    private void ShutdownApplication()
    {
        if (ApplicationLifetime is IControlledApplicationLifetime lifetime)
        {
            lifetime.Shutdown();
        }
    }
}
