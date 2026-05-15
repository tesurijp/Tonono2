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

        Engine = new SkkEngine(Config.RomajiTable, Config.ZenkakuTable, dic, RequestUiUpdate);

        _hook = new KeyboardHook();
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
        if (Engine.ProcessKey(e.VirtualKeyCode, e.IsKeyDown))
        {
            e.Handled = true;
            RequestUiUpdate();
        }
    }
    public void Dispose()
    {
        _configWatcher.Dispose();
        _hook.Dispose();
    }
}
