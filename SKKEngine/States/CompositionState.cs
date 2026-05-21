using System.Globalization;

namespace Tonono2.SKKEngine.States;
public class CompositionState : ISkkEditorState
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
            engine.ResetBuffers();
            return true;
        }

        // Ctrl keys
        if (ctrlPressed)
        {
            if (vkCode == SkkKeyConstants.VkJ) // Ctrl+J
            {
                engine.CommitAll();
                return true;
            }
            if (vkCode == 0x47) // Ctrl+G: Cancel
            {
                engine.ResetBuffers();
                return true;
            }
            return false;
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

        // TAB Completion
        if (vkCode == SkkKeyConstants.VkTab && !shiftPressed)
        {
            if (context.CandidateIndex == -1) // ▽または / モードで読み入力中
            {
                if (context.CompletionIndex == -1)
                {
                    context.OriginalReadingBeforeCompletion = engine.CompositionBuffer.ToString();
                    context.Completions = [ .. engine.Dictionary.GetCompletions(context.OriginalReadingBeforeCompletion) ];
                    if (context.Completions.Count > 0)
                    {
                        context.CompletionIndex = 0;
                    }
                }
                else
                {
                    context.CompletionIndex = (context.CompletionIndex + 1) % context.Completions.Count;
                }

                context.NotifyBufferChanged();
                return true;
            }
            return false;
        }

        // Break completion if other keys pressed
        if (context.CompletionIndex >= 0 && vkCode != SkkKeyConstants.VkTab && vkCode != SkkKeyConstants.VkSpace)
        {
            context.CompletionIndex = -1;
            context.Completions.Clear();
        }

        // Space -> Conversion
        if (vkCode == SkkKeyConstants.VkSpace && !shiftPressed)
        {
            if (context.CompletionIndex >= 0)
            {
                engine.CompositionBuffer.Clear();
                engine.CompositionBuffer.Append(context.Completions[context.CompletionIndex]);
                context.CompletionIndex = -1;
                context.Completions.Clear();
                engine.StartConversion();
                context.NotifyBufferChanged();
                return true;
            }

            if (engine.CompositionBuffer.Length > 0 || engine.RomajiBuffer.Length > 0)
            {
                engine.StartConversion();
                context.NotifyBufferChanged();
                return true;
            }
        }

        // Backspace
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
                    engine.ChangeState(engine.State); // Transitions to IdleState
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
                engine.CommitAll();
                return false; // Let the original Space pass through after committing the buffer
            }
        }

        return false;
    }
}
