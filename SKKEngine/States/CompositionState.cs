using System.Globalization;

namespace Tonono2.SKKEngine.States;
public class CompositionState : StateBase
{
    public static SkkActionResult ProcessKey(SkkEngine engine, SkkKeyCommand command)
    {
        var context = engine.Context;
        var vkCode = command.VkCode;

        if(CommonPreCheck(engine,vkCode) is SkkActionResult preresult)
        {
            return preresult;
        }

        var clearCompletion = context.CompletionIndex >= 0 && vkCode != SkkConstants.VkTab && vkCode != SkkConstants.VkSpace;

        var result = (vkCode, command.Control, command.Shift) switch
        {
            (SkkConstants.VkEscape, _, _) => Handled(engine.ResetBuffers),
            (_, true, _) => HandleCommonCtrlKeys(engine, context, vkCode),
            (SkkConstants.VkQ, _, _) => HandleQKey(engine, context),
            (SkkConstants.VkSlash, _, false) when !context.IsConversionMode && !context.IsAbbreviationMode && context.CompositionBuffer.Length == 0 => HandleEnterAbbreviationMode(engine, context),
            (SkkConstants.VkReturn, _, _) => HandleCommitAll(engine),
            (SkkConstants.VkTab, _, false) when context.CandidateIndex == -1 => HandleTabCompletion(engine, context),
            (SkkConstants.VkSpace, _, false) when context.CompletionIndex >= 0 => HandleAcceptCompletionAndStartConversion(engine, context),
            (SkkConstants.VkSpace, _, false) when context.IsBufferActive => Handled(engine.StartConversion),
            (SkkConstants.VkBack, _, _) => HandleBackspace(engine, context),
            _ => command.Ch.HasValue ? HandleCharInput(engine, context, command.Ch.Value) : Passthrough,
        };
        return clearCompletion ? (() => ClearCompletion(context)) + result : result;
    }

    private static SkkActionResult HandleTabCompletion(SkkEngine engine, SkkContext context) => Handled(() =>
    {
        if (context.CompletionIndex == -1)
        {
            context.OriginalReadingBeforeCompletion = context.CompositionBuffer;
            context.Completions = [.. engine.Dictionary.GetCompletions(context.OriginalReadingBeforeCompletion)];
            if (context.Completions.Count > 0)
            {
                context.CompletionIndex = 0;
            }
        }
        else
        {
            context.CompletionIndex = (context.CompletionIndex + 1) % context.Completions.Count;
        }
    });

    private static void ClearCompletion(SkkContext context)
    {
        context.CompletionIndex = -1;
        context.Completions.Clear();
    }

    private static SkkActionResult HandleAcceptCompletionAndStartConversion(SkkEngine engine, SkkContext context) => Handled(() =>
    {
        context.CompositionBuffer.Clear();
        context.CompositionBuffer.Append(context.Completions[context.CompletionIndex]);
        context.CompletionIndex = -1;
        context.Completions.Clear();
        engine.StartConversion();
    });
}
