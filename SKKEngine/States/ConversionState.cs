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
            case (SkkConstants.VkEscape, _, _):
                {
                    return CancelComposition(engine,context);
                }
            case (_, true, _):
                {
                    return vkCode switch
                    {
                        SkkConstants.VkJ => CommitAll(engine),
                        SkkConstants.VkG => CancelComposition(engine,context),
                        SkkConstants.VkN => NextCandidate(engine, context),
                        SkkConstants.VkP => BackCandidate(engine, context),
                        SkkConstants.VkX when context.CandidateIndex >= 0 && context.CandidateIndex < context.Candidates.Count => RemoveWord(engine, context),
                        _ => false
                    };
                }
        }

        if (context.ListConversion && vkCode >= 0x31 && vkCode <= 0x37)
        {
            int targetIdx = GetNumericSelectionIndex(context, vkCode);
            if (targetIdx < context.Candidates.Count)
            {
                return SelectCandidateDirectly(engine, context, targetIdx);
            }
        }

        return (vkCode, command.Shift) switch
        {
            (SkkConstants.VkSpace, false) => NextCandidate(engine, context),
            (SkkConstants.VkBack, _) => CancelComposition(engine, context),
            (SkkConstants.VkReturn, _) or (SkkConstants.VkQ, _) => CommitAll(engine),
            _ when command.Ch is { } c => char.IsControl(c) ? CommitAll(engine, false) : CommitAndProcessKeyInNextState(engine, vkCode),
            _ => false
        };
    }

    private static bool CommitAndProcessKeyInNextState(SkkEngine engine, int vkCode)
    {
        CommitAll(engine);
        return engine.ProcessKey(vkCode, true);
    }

    private static bool CancelComposition(SkkEngine engine, SkkContext context)
    {
        context.CandidateIndex = -1;
        engine.ChangeState(engine.State);
        return true;
    }

    private static int GetNumericSelectionIndex(SkkContext context, int vkCode)
    {
        const int startNumberCode = 0x31;
        var selection = vkCode - startNumberCode;
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
            engine.ChangeState(engine.State);
        }
        else
        {
            context.CandidateIndex %= context.Candidates.Count;
        }
        return true;
    }

    private static bool NextCandidate(SkkEngine engine, SkkContext context)
    {
        context.CandidateIndex++;

        if (context.CandidateIndex >= context.Candidates.Count)
        {
            engine.StartRegistration(engine.GetDictionaryKey());
        }
        return true;
    }

    private static bool BackCandidate(SkkEngine engine, SkkContext context)
    {
        context.CandidateIndex--;
        if (context.CandidateIndex < 0)
        {
            engine.ChangeState(engine.State);
        }
        return true;
    }
}
