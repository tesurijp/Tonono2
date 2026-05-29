using System;
using Tonono2.Win32;

namespace Tonono2.SKKEngine;

public sealed class SkkController : IDisposable
{
    private readonly KeyboardHook hook;

    public SkkEngine Engine { get; private set; } = null!;

    public SkkController()
    {
        var config = ConfigLoader.Load();
        var dic = new SkkDicManager(config.DictionaryPaths, config.UserDictionaryPath);
        Engine = new(config, dic);
        ConfigLoader.StartWatch(Engine.UpdateConfig);

        hook = new();
        hook.KeyIntercepted += OnKeyIntercepted;
        hook.Install();
    }
    private void OnKeyIntercepted(KeyInfo e)
    {
        if (e.IsKeyDown)
        {
            e.Handled = Engine.ProcessKey(e.VirtualKeyCode);
            Engine.Context.NotifyBufferChanged();
        }
    }
    public void Dispose()
    {
        ConfigLoader.Tidy();
        hook.Dispose();
    }
}
