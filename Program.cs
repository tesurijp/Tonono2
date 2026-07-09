using System;
using System.Diagnostics;
using System.Threading;
using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;

namespace Tonono2;

internal static class Program
{
    internal const string RestartArgument = "--restart";
    private const string SingleInstanceMutexName = "{E11B24F6-0499-4E83-A781-D847BDCD673B}";

    [STAThread]
    public static void Main(string[] args)
    {
        using var mutex = AcquireSingleInstance(args);
        if (mutex is not null)
        {
            try
            {
                BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);
            }
            finally
            {
                mutex.ReleaseMutex();
            }
        }
    }

    public static AppBuilder BuildAvaloniaApp() =>
        AppBuilder.Configure<App>()
            .UsePlatformDetect()
            .WithInterFont()
            .LogToTrace();

    private static Mutex? AcquireSingleInstance(string[] args)
    {
        var mutex = new Mutex(false, SingleInstanceMutexName, out var createdNew);
        if (createdNew || Array.IndexOf(args, RestartArgument) >= 0)
        {
            if (mutex.WaitOne(5000))
            {
                return mutex;
            }
        }

        mutex.Dispose();
        return null;
    }
    internal static void RestartApplication(IControlledApplicationLifetime? lifetime)
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

        ShutdownApplication(lifetime);
    }

    internal static void ShutdownApplication(IControlledApplicationLifetime? lifetime) => lifetime?.Shutdown();

}
