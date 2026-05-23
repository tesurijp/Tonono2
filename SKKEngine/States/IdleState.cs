using System.Globalization;

namespace Tonono2.SKKEngine.States;

public static class IdleState
{
    public static bool ProcessKey(SkkEngine engine, SkkKeyCommand command)
    {
        var context = engine.Context;
        var vkCode = command.VkCode;
        var shiftPressed = command.Shift;
        var ctrlPressed = command.Control;

        // Navigation keys: Pass through
        if (vkCode >= 0x21 && vkCode <= 0x28)
        {
            return false;
        }

        // ESC: Cancellation
        if (vkCode == SkkKeyConstants.VkEscape)
        {
            if (context.CompositionBuffer.Length > 0 || context.RomajiBuffer.Length > 0 || context.IsConversionMode)
            {
                engine.ResetBuffers();
                return true;
            }
            return false;
        }

        // Ctrl keys
        if (ctrlPressed)
        {
            if (vkCode == SkkKeyConstants.VkJ)
            {
                engine.CommitAll();
                return true;
            }
            if (vkCode == 0x47) // Ctrl+G
            {
                if (context.CompositionBuffer.Length > 0 || context.RomajiBuffer.Length > 0)
                {
                    engine.ResetBuffers();
                    return true;
                }
            }
            return false;
        }

        // Mode transitions
        if (vkCode == SkkKeyConstants.VkL && !shiftPressed && !context.IsAbbreviationMode) // l -> ASCII
        {
            engine.CommitAll();
            engine.ChangeState(SkkState.Disabled);
            return true;
        }

        if (vkCode == SkkKeyConstants.VkL && shiftPressed && !context.IsAbbreviationMode) // L -> Zenkaku
        {
            engine.CommitAll();
            engine.ChangeState(SkkState.Zenkaku);
            return true;
        }

        // Q -> toggle or flip
        if (vkCode == SkkKeyConstants.VkQ)
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

        // / -> Abbreviation mode
        if (vkCode == SkkKeyConstants.VkSlash && !shiftPressed)
        {
            if (!context.IsConversionMode && !context.IsAbbreviationMode && context.CompositionBuffer.Length == 0)
            {
                context.IsAbbreviationMode = true;
                engine.ChangeState(engine.State);
                context.NotifyBufferChanged();
                return true;
            }
        }

        // BS
        if (vkCode == SkkKeyConstants.VkBack)
        {
            if (context.RomajiBuffer.Length > 0)
            {
                context.RomajiBuffer.Remove(context.RomajiBuffer.Length - 1, 1);
                context.NotifyBufferChanged();
                return true;
            }
            if (context.CompositionBuffer.Length > 0)
            {
                context.CompositionBuffer.Remove(context.CompositionBuffer.Length - 1, 1);
                if (context.CompositionBuffer.Length == 0)
                {
                    context.IsConversionMode = false;
                    context.IsAbbreviationMode = false;
                    context.OkuriPrefix = null;
                }
                else if (context.OkuriPrefix != null && context.CompositionBuffer.Length < context.ReadingBeforeOkuri.Length)
                {
                    context.OkuriPrefix = null;
                }
                context.NotifyBufferChanged();
                return true;
            }
            return false;
        }

        // Return
        if (vkCode == SkkKeyConstants.VkReturn && !shiftPressed)
        {
            if (context.CompositionBuffer.Length > 0 || context.RomajiBuffer.Length > 0)
            {
                engine.CommitAll();
                return true;
            }
            return false;
        }

        // Space
        if (vkCode == SkkKeyConstants.VkSpace && !shiftPressed)
        {
            if ((context.IsConversionMode || context.IsAbbreviationMode) && (context.CompositionBuffer.Length > 0 || context.RomajiBuffer.Length > 0))
            {
                engine.StartConversion();
                context.NotifyBufferChanged();
                return true;
            }
        }

        // Char input
        if (command.Ch.HasValue)
        {
            char c = command.Ch.Value;
            
            // Pass through control characters (TAB, CR, LF, etc.) if not explicitly handled
            if (char.IsControl(c))
            {
                return false;
            }

            if (context.IsAbbreviationMode)
            {
                context.CompositionBuffer.Append(c);
                context.NotifyBufferChanged();
                return true;
            }

            var isSymbol = !char.IsLetter(c) && !char.IsDigit(c);
            var canMatch = engine.kanaConverter.ToKana(c.ToString()) != string.Empty || engine.kanaConverter.IsPotentialPrefix(c.ToString());
            if (isSymbol && !canMatch)
            {
                if (context.CompositionBuffer.Length == 0 && context.RomajiBuffer.Length == 0)
                {
                    return false; // Let it pass through if no active composition
                }
                engine.CommitAll();
                return false; // Let the original symbol pass through after committing the buffer
            }

            if (c != ' ')
            {
                if (char.IsUpper(c) && char.IsLetter(c))
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

                context.RomajiBuffer.Append(char.ToLower(c, CultureInfo.CurrentCulture));
                engine.TryConvertRomaji();
                context.NotifyBufferChanged();
                return true;
            }
            else
            {
                if (context.CompositionBuffer.Length == 0 && context.RomajiBuffer.Length == 0)
                {
                    return false; // Let Space pass through if nothing to commit
                }
                engine.CommitAll();
                return false; // Let the original Space pass through after committing the buffer
            }
        }

        return false;
    }
}
