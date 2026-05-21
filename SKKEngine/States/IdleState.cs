using System;
using System.Globalization;

namespace Tonono2.SKKEngine.States;

public class IdleState : ISkkEditorState
{
    public bool ProcessKey(SkkEngine engine, SkkKeyCommand command)
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
            if (engine.CompositionBuffer.Length > 0 || engine.RomajiBuffer.Length > 0 || context.IsConversionMode)
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
                if (engine.CompositionBuffer.Length > 0 || engine.RomajiBuffer.Length > 0)
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
            if (engine.CompositionBuffer.Length > 0 || engine.RomajiBuffer.Length > 0)
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
            if (!context.IsConversionMode && !context.IsAbbreviationMode && engine.CompositionBuffer.Length == 0)
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
            if (engine.RomajiBuffer.Length > 0)
            {
                engine.RomajiBuffer.Remove(engine.RomajiBuffer.Length - 1, 1);
                context.NotifyBufferChanged();
                return true;
            }
            if (engine.CompositionBuffer.Length > 0)
            {
                engine.CompositionBuffer.Remove(engine.CompositionBuffer.Length - 1, 1);
                if (engine.CompositionBuffer.Length == 0)
                {
                    context.IsConversionMode = false;
                    context.IsAbbreviationMode = false;
                    context.OkuriPrefix = null;
                }
                else if (context.OkuriPrefix != null && engine.CompositionBuffer.Length < context.ReadingBeforeOkuri.Length)
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
            if (engine.CompositionBuffer.Length > 0 || engine.RomajiBuffer.Length > 0)
            {
                engine.CommitAll();
                return true;
            }
            return false;
        }

        // Space
        if (vkCode == SkkKeyConstants.VkSpace && !shiftPressed)
        {
            if ((context.IsConversionMode || context.IsAbbreviationMode) && (engine.CompositionBuffer.Length > 0 || engine.RomajiBuffer.Length > 0))
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
                engine.CompositionBuffer.Append(c);
                context.NotifyBufferChanged();
                return true;
            }

            var isSymbol = !char.IsLetter(c) && !char.IsDigit(c);
            var canMatch = engine.kanaConverter.ToKana(c.ToString()) != string.Empty || engine.kanaConverter.IsPotentialPrefix(c.ToString());
            if (isSymbol && !canMatch)
            {
                if (engine.CompositionBuffer.Length == 0 && engine.RomajiBuffer.Length == 0)
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
                    else if (context.OkuriPrefix == null && engine.CompositionBuffer.Length > 0)
                    {
                        if (engine.RomajiBuffer.Length == 1 && engine.RomajiBuffer[0] == 'n')
                        {
                            engine.HandleKanaProduced("ん");
                            engine.RomajiBuffer.Clear();
                        }
                        context.OkuriPrefix = char.ToLower(c, CultureInfo.CurrentCulture).ToString();
                        context.ReadingBeforeOkuri = engine.CompositionBuffer.ToString();
                        engine.ChangeState(engine.State);
                    }
                }

                engine.RomajiBuffer.Append(char.ToLower(c, CultureInfo.CurrentCulture));
                engine.TryConvertRomaji();
                context.NotifyBufferChanged();
                return true;
            }
            else
            {
                if (engine.CompositionBuffer.Length == 0 && engine.RomajiBuffer.Length == 0)
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
