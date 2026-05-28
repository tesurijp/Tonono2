using System;
using System.IO;
using System.Linq;
using System.Threading;
using Tonono2.Win32;

namespace Tonono2.SKKEngine;

public sealed class SkkController : IDisposable
{
    private readonly KeyboardHook _hook;
    private readonly FileSystemWatcher _configWatcher;

    public SkkEngine Engine { get; private set; } = null!;
    public AppConfig Config { get; private set; } = new();

    public SkkController()
    {
        LoadConfig();

        var dic = new SkkDicManager(Config.DictionaryPaths, Config.UserDictionaryPath);

        Engine = new (Config, dic);

        _hook = new ();
        _hook.KeyIntercepted += OnKeyIntercepted;
        _hook.Install();

        _configWatcher = new FileSystemWatcher(Path.GetDirectoryName(AppConfig.ConfigPath)!, Path.GetFileName(AppConfig.ConfigPath))
        {
            NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.FileName,
            EnableRaisingEvents = true
        };
        _configWatcher.Changed += (_, _) => OnConfigChanged();
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

    private void OnConfigChanged()
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

    private void OnKeyIntercepted(KeyInfo e)
    {
        if (e.VirtualKeyCode == SkkConstants.VkEscape && e.IsKeyDown && Engine.State != SkkState.Disabled && IsViCompatibleAppActive())
        {
            Engine.CancelAndDisable();
            return;
        }

        if (Engine.ProcessKey(e.VirtualKeyCode, e.IsKeyDown))
        {
            e.Handled = true;
        }
        Engine.Context.NotifyBufferChanged();
    }

    private bool IsViCompatibleAppActive()
    {
        var activePath = ActiveProcess.GetActiveProcessPath()?.Replace('/', '\\');
        if (string.IsNullOrEmpty(activePath)) return false;
        return Config.ViCompatibleApps.Any(i => activePath.EndsWith(i, StringComparison.OrdinalIgnoreCase));
    }
    public void Dispose()
    {
        _configWatcher.Dispose();
        _hook.Dispose();
    }
}
