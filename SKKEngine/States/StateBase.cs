using System;
using System.Windows;

namespace Tonono2.SKKEngine.States;

public record SkkActionResult(bool IsHandled, Action Action)
{
    public static SkkActionResult operator +(SkkActionResult current, Action postAction) =>
        new (current.IsHandled, () => { current.Action(); postAction(); });
    public static SkkActionResult operator +(Action preAction , SkkActionResult current) =>
        new (current.IsHandled, () => { preAction(); current.Action(); });

    public void Invoke(SkkEngine engine) =>
        Application.Current.Dispatcher.Invoke(() =>
        {
            Action();
            engine.Context.NotifyBufferChanged();
        });
}

public abstract class StateBase
{
    protected static readonly SkkActionResult Passthrough = new(false, () => { });
    protected static readonly SkkActionResult HandledOnly = new(true, () => { });
    protected static SkkActionResult Handled(Action action) => new(true, action);
    protected static SkkActionResult Pass(Action action) => new(false, action);

    protected static SkkActionResult? CommonPreCheck(SkkEngine engine, int vkCode )
    {
        if (vkCode == SkkConstants.VkEscape && engine.State != SkkState.Disabled && engine.IsViCompatibleAppActive())
        {
            return Pass(engine.CancelAndDisable);
        }
        if (IsNavigationKey(vkCode))
        {
            return Passthrough;
        }
        return null;
    }

    protected static SkkActionResult HandleQKey(SkkEngine engine, SkkContext context) =>
        context.IsBufferActive ? Handled(engine.FlipAndCommit) : Handled(engine.ToggleHiraganaKatakana);

    protected static SkkActionResult HandleCommitAll(SkkEngine engine) => Handled(engine.CommitAll);

    protected static SkkActionResult HandleEnterAbbreviationMode(SkkEngine engine, SkkContext context) => Handled(() =>
    {
        context.IsAbbreviationMode = true;
        engine.ChangeState(engine.State);
    });

    protected static SkkActionResult HandleRomajiCandidate(SkkEngine engine, SkkContext context, char c)
    {
        var actionresult = Handled(() =>
        {
            context.RomajiBuffer.Append(char.ToLower(c));
            engine.TryConvertRomaji();
        });

        if (char.IsUpper(c) && char.IsLetter(c))
        {
            return (() =>
            {
                if (!context.IsConversionMode)
                {
                    context.IsConversionMode = true;
                    context.OkuriPrefix = null;
                    context.ReadingBeforeOkuri = "";
                    engine.ChangeState(engine.State);
                }
                else if (context.OkuriPrefix == null && context.CompositionBuffer.Length > 0)
                {
                    if( engine.kanaConverter.ToFinish( context.RomajiBuffer, out var mora))
                    {
                        context.CompositionBuffer.Append(mora!);
                        context.RomajiBuffer.Remove(0, 1);
                    }
                    context.OkuriPrefix = char.ToLower(context.RomajiBuffer.First ?? c).ToString();
                    context.ReadingBeforeOkuri = context.CompositionBuffer;
                    engine.ChangeState(engine.State);
                }
            }) + actionresult;
        }
        else
        {
            return actionresult;
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
        if (c == '\0' || char.IsControl(c))
        {
            return Passthrough;
        }

        if (context.IsAbbreviationMode)
        {
            return Handled(() => context.CompositionBuffer.Append(c));
        }

        SkkActionResult Unhandled() => context.IsBufferActive ? Pass(engine.CommitAll) : Passthrough;

        var isSymbol = !char.IsLetter(c) && !char.IsDigit(c);
        var canMatch = engine.kanaConverter.CanMatch(c.ToString());
        if (isSymbol && !canMatch)
        {
            return Unhandled();
        }

        if (c != ' ')
        {
            return HandleRomajiCandidate(engine, context, c);
        }
        else
        {
            return Unhandled();
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
