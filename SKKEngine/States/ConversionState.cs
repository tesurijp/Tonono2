namespace Tonono2.SKKEngine.States;

public class ConversionState : ISkkEditorState
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

            // Ctrl+N / Ctrl+P: Select candidate
            if (vkCode == SkkKeyConstants.VkN || vkCode == SkkKeyConstants.VkP)
            {
                if (vkCode == SkkKeyConstants.VkN)
                {
                    context.CandidateIndex++;
                }
                else
                {
                    context.CandidateIndex = (context.CandidateIndex - 1 + context.Candidates.Count) % context.Candidates.Count;
                }

                if (context.CandidateIndex >= context.Candidates.Count)
                {
                    engine.StartRegistration(engine.GetDictionaryKey());
                }
                context.NotifyBufferChanged();
                return true;
            }

            // Ctrl+X: Remove word
            if (vkCode == SkkKeyConstants.VkX && context.CandidateIndex >= 0 && context.CandidateIndex < context.Candidates.Count)
            {
                var word = context.Candidates[context.CandidateIndex];
                engine.Dictionary.RemoveWord(engine.GetDictionaryKey(), word);
                context.Candidates.RemoveAt(context.CandidateIndex);
                if (context.Candidates.Count == 0)
                {
                    context.CandidateIndex = -1;
                    engine.ChangeState(engine.State); // Back to CompositionState or IdleState
                }
                else
                {
                    context.CandidateIndex %= context.Candidates.Count;
                }
                context.NotifyBufferChanged();
                return true;
            }

            return false;
        }

        // Numeric selection (1-7) when page is displayed
        if (context.CandidateIndex >= 4 && vkCode >= 0x31 && vkCode <= 0x37)
        {
            var selection = vkCode - 0x31;
            var pageStart = (context.CandidateIndex / 7) * 7;
            var targetIdx = pageStart + selection;
            if (targetIdx < context.Candidates.Count)
            {
                context.CandidateIndex = targetIdx;
                engine.CommitAll();
                return true;
            }
        }

        // Space -> Next candidate
        if (vkCode == SkkKeyConstants.VkSpace && !shiftPressed)
        {
            context.CandidateIndex++;
            if (context.CandidateIndex >= context.Candidates.Count)
            {
                engine.StartRegistration(engine.GetDictionaryKey());
            }
            context.NotifyBufferChanged();
            return true;
        }

        // Backspace -> Back to CompositionState
        if (vkCode == SkkKeyConstants.VkBack)
        {
            context.CandidateIndex = -1;
            engine.ChangeState(engine.State); // Transitions back to CompositionState
            context.NotifyBufferChanged();
            return true;
        }

        // Return or Q -> Commit selection
        if (vkCode == SkkKeyConstants.VkReturn || vkCode == SkkKeyConstants.VkQ)
        {
            engine.CommitAll();
            return true;
        }

        // Char input -> Commit selection and process char in next state
        if (command.Ch.HasValue)
        {
            char c = command.Ch.Value;

            // If it's a control character (like TAB), commit and let it pass through
            if (char.IsControl(c))
            {
                engine.CommitAll();
                return false;
            }

            engine.CommitAll();
            // Process the key in the new state (usually IdleState)
            return engine.ProcessKey(vkCode, true);
        }

        return false;
    }
}
