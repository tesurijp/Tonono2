using System;
using System.IO;
using System.Threading;
using System.Linq;
using Tonono2.Win32;
using Tonono2;

namespace Tonono2.SKKEngine;

public sealed class SkkController : IDisposable
{
    private readonly KeyboardHook _hook;
    private readonly FileSystemWatcher _configWatcher;

    public SkkEngine Engine { get; private set; } = null!;
    public AppConfig Config { get; private set; } = new();

    public Action RequestUiUpdate { get; set; } = () => { };

    public SkkController()
    {
        LoadConfig();

        var dic = new SkkDicManager(Config.DictionaryPaths, Config.UserDictionaryPath);

        Engine = new (Config.RomajiTable, Config.ZenkakuTable, dic, RequestUiUpdate);

        _hook = new ();
        _hook.KeyIntercepted += OnKeyIntercepted;
        _hook.Install();

        _configWatcher = new FileSystemWatcher(Path.GetDirectoryName(AppConfig.ConfigPath)!, Path.GetFileName(AppConfig.ConfigPath))
        {
            NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.FileName,
            EnableRaisingEvents = true
        };
        _configWatcher.Changed += OnConfigChanged;
    }

    private void LoadConfig()
    {
        try
        {
            DebugLogger.Log($"Loading config from: {AppConfig.ConfigPath}");
            var yaml = File.ReadAllText(AppConfig.ConfigPath);
            var newConfig = ConfigLoader.Load(yaml);
            if (newConfig.RomajiTable != null && newConfig.ZenkakuTable != null)
            {
                Config = newConfig;
                DebugLogger.Log("config.yaml loaded successfully.");
            }
        }
        catch (Exception ex)
        {
            DebugLogger.Log($"Error loading config.yaml: {ex.Message}");
        }
    }

    private void OnConfigChanged(object sender, FileSystemEventArgs e)
    {
        DebugLogger.Log("config.yaml change detected.");
        var oldConfig = Config;
        Thread.Sleep(500);
        LoadConfig();
        if (Config != oldConfig)
        {
            Engine.UpdateConfig(Config);
        }
    }

    private void OnKeyIntercepted(object? sender, KeyboardKeyEventArgs e)
    {
        if (e.VirtualKeyCode == 0x1B && e.IsKeyDown && Engine.State != SkkState.Disabled && IsViCompatibleAppActive())
        {
            Engine.CancelAndDisable();
            // e.Handled is false by default, so ESC is passed through
            RequestUiUpdate();
            return;
        }

        if (Engine.ProcessKey(e.VirtualKeyCode, e.IsKeyDown))
        {
            e.Handled = true;
            RequestUiUpdate();
        }
    }

    private bool IsViCompatibleAppActive()
    {
        var activePath = ActiveProcess.GetActiveProcessPath();
        if (string.IsNullOrEmpty(activePath)) return false;

        // Normalize path separators to backslashes for consistency
        var normalizedActivePath = activePath.Replace('/', '\\');

        foreach (var app in Config.ViCompatibleApps)
        {
            var normalizedApp = app.Replace('/', '\\');
            if (normalizedActivePath.EndsWith(normalizedApp, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }
        return false;
    }
    public void Dispose()
    {
        _configWatcher.Dispose();
        _hook.Dispose();
    }
}
