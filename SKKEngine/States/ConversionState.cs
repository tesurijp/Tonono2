namespace Tonono2.SKKEngine.States;

public class ConversionState : StateBase
{
    public static bool ProcessKey(SkkEngine engine, SkkKeyCommand command)
    {
        var context = engine.Context;
        var vkCode = command.VkCode;

        if (IsNavigationKey(vkCode))
        {
            return false;
        }

        switch (vkCode, command.Control, command.Shift)
        {
            case (SkkKeyConstants.VkEscape, _, _):
            {
                return ResetBuffers(engine);
            }
            case (_, true, _):
            {
                return vkCode switch
                {
                    SkkKeyConstants.VkJ => CommitAll(engine),
                    0x47 => ResetBuffers(engine),
                    SkkKeyConstants.VkN or SkkKeyConstants.VkP => SelectCandidate(engine, context, vkCode == SkkKeyConstants.VkN),
                    SkkKeyConstants.VkX when context.CandidateIndex >= 0 && context.CandidateIndex < context.Candidates.Count => RemoveWord(engine, context),
                    _ => false
                };
            }
        }

        if (context.CandidateIndex >= 4 && vkCode >= 0x31 && vkCode <= 0x37)
        {
            int targetIdx = GetNumericSelectionIndex(context, vkCode);
            if (targetIdx < context.Candidates.Count)
            {
                return SelectCandidateDirectly(engine, context, targetIdx);
            }
        }

        return (vkCode, command.Shift) switch
        {
            (SkkKeyConstants.VkSpace, false) => NextCandidate(engine, context),
            (SkkKeyConstants.VkBack, _) => BackToComposition(engine, context),
            (SkkKeyConstants.VkReturn, _) or (SkkKeyConstants.VkQ, _) => CommitAll(engine),
            _ when command.Ch is { } c => char.IsControl(c) ? CommitAll(engine, false) : CommitAndProcessKeyInNextState(engine, vkCode),
            _ => false
        };
    }

    private static bool CommitAndProcessKeyInNextState(SkkEngine engine, int vkCode)
    {
        CommitAll(engine);
        // Process the key in the new state (usually IdleState)
        return engine.ProcessKey(vkCode, true);
    }

    private static bool BackToComposition(SkkEngine engine, SkkContext context)
    {
        context.CandidateIndex = -1;
        engine.ChangeState(engine.State); // Transitions back to CompositionState
        context.NotifyBufferChanged();
        return true;
    }

    private static bool NextCandidate(SkkEngine engine, SkkContext context)
    {
        context.CandidateIndex++;
        if (context.CandidateIndex >= context.Candidates.Count)
        {
            engine.StartRegistration(engine.GetDictionaryKey());
        }
        context.NotifyBufferChanged();
        return true;
    }

    private static int GetNumericSelectionIndex(SkkContext context, int vkCode)
    {
        var selection = vkCode - 0x31;
        var pageStart = (context.CandidateIndex / 7) * 7;
        var targetIdx = pageStart + selection;
        return targetIdx;
    }

    private static bool SelectCandidateDirectly(SkkEngine engine, SkkContext context, int targetIdx)
    {
        context.CandidateIndex = targetIdx;
        return CommitAll(engine);
    }

    private static bool RemoveWord(SkkEngine engine, SkkContext context)
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

    private static bool SelectCandidate(SkkEngine engine, SkkContext context, bool forward)
    {
        if (forward)
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
}
