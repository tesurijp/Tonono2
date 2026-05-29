namespace Tonono2.SKKEngine.States;

public class ConversionState : StateBase
{
    public static SkkActionResult ProcessKey(SkkEngine engine, SkkKeyCommand command)
    {
        var context = engine.Context;
        var vkCode = command.VkCode;

        if(CommonPreCheck(engine,vkCode) is SkkActionResult preresult)
        {
            return preresult;
        }

        if (context.ListConversion)
        {
            int targetIdx = GetSelectionIndex(context, vkCode);
            if (targetIdx >= 0 && targetIdx < context.Candidates.Count)
            {
                return HandleSelectCandidateDirectly(engine, context, targetIdx);
            }
        }
        return (vkCode, command.Control, command.Shift) switch
        {
            (SkkConstants.VkEscape, _, _) => HandleCancelComposition(engine, context),
            (SkkConstants.VkJ, true, _) => HandleCommitAll(engine),
            (SkkConstants.VkG, true, _) => HandleCancelComposition(engine, context),
            (SkkConstants.VkN, true, _) => HandleNextCandidate(engine, context),
            (SkkConstants.VkP, true, _) => HandleBackCandidate(engine, context),
            (SkkConstants.VkX, true, _) when context.CandidateIndex >= 0 && context.CandidateIndex < context.Candidates.Count => HandleRemoveWord(engine, context),
            (_, true, _) => Passthrough,
            (SkkConstants.VkSpace, _, false) => HandleNextPage(engine, context),
            (SkkConstants.VkBack, _, _) => HandleCancelComposition(engine, context),
            (SkkConstants.VkReturn, _, _) => HandleCommitAll(engine),
            (SkkConstants.VkQ, _, _) => HandleCommitAll(engine),
            _ when command.Ch is { } c => char.IsControl(c) ? Pass(engine.CommitAll) : HandleCommitAndProcessKeyInNextState(engine, vkCode),
            _ => Passthrough
        };
    }

    private static SkkActionResult HandleCommitAndProcessKeyInNextState(SkkEngine engine, int vkCode) => Handled(() =>
    {
        engine.CommitAll();
        engine.ProcessKey(vkCode);
    });

    private static SkkActionResult HandleCancelComposition(SkkEngine engine, SkkContext context) => Handled(() =>
    {
        context.CandidateIndex = -1;
        engine.ChangeState(engine.State);
    });

    private static int GetSelectionIndex(SkkContext context, int vkCode)
    {
        var selection = vkCode switch
        {
            SkkConstants.VkA => 0,
            SkkConstants.VkS => 1,
            SkkConstants.VkD => 2,
            SkkConstants.VkF => 3,
            SkkConstants.VkJ => 4,
            SkkConstants.VkK => 5,
            SkkConstants.VkL => 6,
            _ => -1
        };
        if (selection == -1) return -1;

        var targetIdx = context.PageStart + selection;
        return targetIdx;
    }

    private static SkkActionResult HandleSelectCandidateDirectly(SkkEngine engine, SkkContext context, int targetIdx) => Handled(() =>
    {
        context.CandidateIndex = targetIdx;
        engine.CommitAll();
    });

    private static SkkActionResult HandleRemoveWord(SkkEngine engine, SkkContext context) => Handled(() =>
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
    });

    private static SkkActionResult HandleNextCandidate(SkkEngine engine, SkkContext context, int? newindex = null) => Handled(() =>
    {
        context.CandidateIndex = newindex ?? context.CandidateIndex + 1;

        if (context.CandidateIndex >= context.Candidates.Count)
        {
            engine.StartRegistration(engine.GetDictionaryKey());
        }
    });

    private static SkkActionResult HandleNextPage(SkkEngine engine, SkkContext context) => Handled(() =>
    {
        int? idx = null;
        if (context.ListConversion)
        {
            idx = context.PageStart + SkkContext.ListPageSize;
        }

        context.CandidateIndex = idx ?? context.CandidateIndex + 1;

        if (context.CandidateIndex >= context.Candidates.Count)
        {
            engine.StartRegistration(engine.GetDictionaryKey());
        }
    });

    private static SkkActionResult HandleBackCandidate(SkkEngine engine, SkkContext context) => Handled(() =>
    {
        context.CandidateIndex--;
        if (context.CandidateIndex < 0)
        {
            engine.ChangeState(engine.State);
        }
    });
}
