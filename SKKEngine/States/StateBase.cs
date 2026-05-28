using System;
using System.Globalization;
using System.Windows;

namespace Tonono2.SKKEngine.States;

public record SkkActionResult(bool IsHandled, Action? Action = null)
{
    public SkkActionResult AppendPreAction(Action action) =>
        Action is not null ?
         new(IsHandled, () => { 
             action(); 
             Action(); 
         }) :
         new(IsHandled, action);

    public void Invoke(SkkEngine engine)
    {
        if (Action is not null)
        {
            Application.Current.Dispatcher.Invoke(() =>
            {
                Action();
                engine.Context.NotifyBufferChanged();
            });
        }
    }
}

public abstract class StateBase
{
    protected static readonly SkkActionResult Passthrough = new(false, null);
    protected static readonly SkkActionResult HandledOnly = new(true, null);
    protected static SkkActionResult Handled(Action? action) => new(true, action);
    protected static SkkActionResult Pass(Action? action) => new(false, action);

    protected static SkkActionResult HandleQKey(SkkEngine engine, SkkContext context) =>
        context.IsBufferActive ? Handled(engine.FlipAndCommit) : Handled(engine.ToggleHiraganaKatakana);

    protected static SkkActionResult HandleCommitAll(SkkEngine engine) => Handled(engine.CommitAll);

    protected static SkkActionResult HandleEnterAbbreviationMode(SkkEngine engine, SkkContext context) => Handled(() =>
    {
        context.IsAbbreviationMode = true;
        engine.ChangeState(engine.State);
    });

    protected static void ActionUpperCase(SkkEngine engine, SkkContext context, char c)
    {
        if (!char.IsUpper(c) || !char.IsLetter(c))
        {
            return;
        }

        if (!context.IsConversionMode)
        {
            context.IsConversionMode = true;
            context.OkuriPrefix = null;
            context.ReadingBeforeOkuri = "";
            engine.ChangeState(engine.State);
        }
        else if (context.OkuriPrefix == null && context.CompositionBuffer.Length > 0)
        {
            if (context.RomajiBuffer.Length == 1 && context.RomajiBuffer[0] == 'n')
            {
                engine.HandleKanaProduced("ん");
                context.RomajiBuffer.Clear();
            }
            context.OkuriPrefix = char.ToLower(c, CultureInfo.CurrentCulture).ToString();
            context.ReadingBeforeOkuri = context.CompositionBuffer.ToString();
            engine.ChangeState(engine.State);
        }
    }

    protected static SkkActionResult HandleBackspace(SkkEngine engine, SkkContext context)
    {
        if (context.RomajiBuffer.Length > 0)
        {
            return Handled(() => context.RomajiBuffer.Remove(context.RomajiBuffer.Length - 1, 1));
        }
        if (context.CompositionBuffer.Length > 0)
        {
            return Handled(() =>
            {
                context.CompositionBuffer.Remove(context.CompositionBuffer.Length - 1, 1);
                if (context.CompositionBuffer.Length == 0)
                {
                    context.IsConversionMode = false;
                    context.IsAbbreviationMode = false;
                    context.OkuriPrefix = null;
                    engine.ChangeState(engine.State);
                }
                else if (context.OkuriPrefix != null && context.CompositionBuffer.Length < context.ReadingBeforeOkuri.Length)
                {
                    context.OkuriPrefix = null;
                }
            });
        }
        return Passthrough;
    }

    protected static SkkActionResult HandleCharInput(SkkEngine engine, SkkContext context, char c)
    {
        if (char.IsControl(c))
        {
            return Passthrough;
        }

        if (context.IsAbbreviationMode)
        {
            return Handled(() => context.CompositionBuffer.Append(c));
        }

        var isSymbol = !char.IsLetter(c) && !char.IsDigit(c);
        var canMatch = engine.kanaConverter.CanMatch(c.ToString());
        if (isSymbol && !canMatch)
        {
            if (context.CompositionBuffer.Length == 0 && context.RomajiBuffer.Length == 0)
            {
                return Passthrough;
            }
            return Pass(engine.CommitAll);
        }

        if (c != ' ')
        {
            return Handled(() =>
            {
                ActionUpperCase(engine, context, c);
                context.RomajiBuffer.Append(char.ToLower(c, CultureInfo.CurrentCulture));
                engine.TryConvertRomaji();
            });
        }
        else
        {
            if (context.CompositionBuffer.Length == 0 && context.RomajiBuffer.Length == 0)
            {
                return Passthrough;
            }
            return Pass(engine.CommitAll);
        }
    }

    protected static bool IsNavigationKey(int vkCode) => vkCode >= 0x21 && vkCode <= 0x28;

    protected static SkkActionResult HandleSwitchState(SkkEngine engine, SkkState newState) => Handled(() =>
    {
        engine.CommitAll();
        engine.ChangeState(newState);
    });

    protected static SkkActionResult HandleTryResetBuffers(SkkEngine engine, SkkContext context) =>
        context.IsBufferActive || context.IsConversionMode ? Handled(engine.ResetBuffers) : Passthrough;

    protected static SkkActionResult HandleCommonCtrlKeys(SkkEngine engine, SkkContext context, int vkCode) =>
        vkCode == SkkConstants.VkJ ? HandleCommitAll(engine) :
        vkCode == SkkConstants.VkG ? HandleTryResetBuffers(engine, context) :
        Passthrough;
}
