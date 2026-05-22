using System.Globalization;

namespace Tonono2.SKKEngine.States;

public abstract class StateBase
{
    protected static (SkkContext context, int vkCode, bool? result) PrepareProcessing(SkkEngine engine, SkkKeyCommand command) => 
        (engine.Context, command.VkCode, IsNavigationKey(command.VkCode) ? false : null);


    protected static bool ResetBuffers(SkkEngine engine)
    {
        engine.ResetBuffers();
        return true;
    }

    protected static bool CommitAll(SkkEngine engine, bool result = true)
    {
        engine.CommitAll();
        return result;
    }

    protected static bool HandleRomaji(SkkEngine engine, SkkContext context, char c)
    {
        context.RomajiBuffer.Append(char.ToLower(c, CultureInfo.CurrentCulture));
        engine.TryConvertRomaji();
        return true;
    }

    protected static bool BackspaceRomaji(SkkContext context)
    {
        if (context.RomajiBuffer.Length > 0)
        {
            context.RomajiBuffer.Remove(context.RomajiBuffer.Length - 1, 1);
            return true;
        }
        return false;
    }

    protected static bool HandleQKey(SkkEngine engine, SkkContext context)
    {
        if (context.CompositionBuffer.Length > 0 || context.RomajiBuffer.Length > 0)
        {
            engine.FlipAndCommit();
        }
        else
        {
            engine.ToggleHiraganaKatakana();
        }
        return true;
    }

    protected static bool EnterAbbreviationMode(SkkEngine engine, SkkContext context)
    {
        context.IsAbbreviationMode = true;
        engine.ChangeState(engine.State);
        return true;
    }

    protected static bool StartConversion(SkkEngine engine)
    {
        engine.StartConversion();
        return true;
    }

    protected static bool AppendToComposition(SkkContext context, char c)
    {
        context.CompositionBuffer.Append(c);
        return true;
    }

    protected static void HandleUpperCase(SkkEngine engine, SkkContext context, char c)
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
            if (context.RomajiBuffer.Length == 1 && context.RomajiBuffer.First == 'n')
            {
                engine.HandleKanaProduced("ん");
                context.RomajiBuffer.Clear();
            }
            context.OkuriPrefix = char.ToLower(c, CultureInfo.CurrentCulture).ToString();
            context.ReadingBeforeOkuri = context.CompositionBuffer.ToString();
            engine.ChangeState(engine.State);
        }
    }

    protected static bool BackspaceComposition(SkkEngine engine, SkkContext context)
    {
        if (context.CompositionBuffer.Length > 0)
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
            return true;
        }
        return false;
    }

    protected static bool HandleCharInput(SkkEngine engine, SkkContext context, char c)
    {
        if (char.IsControl(c))
        {
            return false;
        }

        if (context.IsAbbreviationMode)
        {
            return AppendToComposition(context, c);
        }

        var isSymbol = !char.IsLetter(c) && !char.IsDigit(c);
        var canMatch = engine.kanaConverter.ToKana(c.ToString()) != string.Empty || engine.kanaConverter.IsPotentialPrefix(c.ToString());
        if (isSymbol && !canMatch)
        {
            if (context.CompositionBuffer.Length == 0 && context.RomajiBuffer.Length == 0)
            {
                return false;
            }
            return CommitAll(engine, false);
        }

        if (c != ' ')
        {
            HandleUpperCase(engine, context, c);
            return HandleRomaji(engine, context, c);
        }
        else
        {
            if (context.CompositionBuffer.Length == 0 && context.RomajiBuffer.Length == 0)
            {
                return false;
            }
            return CommitAll(engine, false);
        }
    }

    protected static bool IsNavigationKey(int vkCode)
    {
        return vkCode >= 0x21 && vkCode <= 0x28;
    }

    protected static bool SwitchState(SkkEngine engine, SkkState newState)
    {
        engine.CommitAll();
        engine.ChangeState(newState);
        return true;
    }

    protected static bool TryResetBuffers(SkkEngine engine, SkkContext context)
    {
        if (context.CompositionBuffer.Length > 0 || context.RomajiBuffer.Length > 0 || context.IsConversionMode)
        {
            return ResetBuffers(engine);
        }
        return false;
    }

    protected static bool IsBufferActive(SkkContext context)
    {
        return context.CompositionBuffer.Length > 0 || context.RomajiBuffer.Length > 0;
    }

    protected static bool HandleCommonCtrlKeys(SkkEngine engine, SkkContext context, int vkCode)
    {
        if (vkCode == SkkConstants.VkJ)
        {
            return CommitAll(engine);
        }
        if (vkCode == SkkConstants.VkG)
        {
            return TryResetBuffers(engine, context);
        }
        return false;
    }
}
