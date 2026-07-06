using System;
using System.Diagnostics;
using System.Threading;
using System.Windows;
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
    public App()
    {
        System.Text.Encoding.RegisterProvider(System.Text.CodePagesEncodingProvider.Instance);
        ShutdownMode = ShutdownMode.OnExplicitShutdown;
    }

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        if (AcquireSingleInstance(e.Args))
        {
            controller = new();
            ui = new() { DataContext = controller.Engine.Context };
            trayIcon = new(ui, RestartApplication);
        }
        else
        {
            Shutdown();
        }
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
        mutex?.ReleaseMutex();
        mutex?.Dispose();
        GC.SuppressFinalize(this);
    }

    private bool AcquireSingleInstance(string[] args)
    {
        mutex = new (false, SingleInstanceMutexName, out var createdNew);
        if (createdNew || args.Contains(RestartArgument))
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
            Process.Start(new ProcessStartInfo()
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
        Shutdown();
    }
}
