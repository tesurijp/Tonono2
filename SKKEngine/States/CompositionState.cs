using System.Globalization;

namespace Tonono2.SKKEngine.States;
public class CompositionState : StateBase
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
                return HandleCommonCtrlKeys(engine, context, vkCode);
            }
            case (SkkKeyConstants.VkQ, false, _):
            {
                return HandleQKey(engine, context);
            }
            case (SkkKeyConstants.VkSlash, false, false) when !context.IsConversionMode && !context.IsAbbreviationMode && context.CompositionBuffer.Length == 0:
            {
                return EnterAbbreviationMode(engine, context);
            }
            case (SkkKeyConstants.VkTab, false, false) when context.CandidateIndex == -1:
            {
                return HandleTabCompletion(engine, context);
            }
        }

        if (context.CompletionIndex >= 0 && vkCode != SkkKeyConstants.VkTab && vkCode != SkkKeyConstants.VkSpace)
        {
            ClearCompletion(context);
        }

        return (vkCode, command.Shift) switch
        {
            (SkkKeyConstants.VkSpace, false) when context.CompletionIndex >= 0 => AcceptCompletionAndStartConversion(engine, context),
            (SkkKeyConstants.VkSpace, false) when IsBufferActive(context) => StartConversion(engine, context),
            (SkkKeyConstants.VkBack, _) => BackspaceRomaji(context) || BackspaceComposition(engine, context),
            _ => command.Ch.HasValue && HandleCharInput(engine, context, command.Ch.Value)
        };
    }

    private static bool HandleTabCompletion(SkkEngine engine, SkkContext context)
    {
        if (context.CompletionIndex == -1)
        {
            context.OriginalReadingBeforeCompletion = context.CompositionBuffer.ToString();
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

        context.NotifyBufferChanged();
        return true;
    }

    private static void ClearCompletion(SkkContext context)
    {
        context.CompletionIndex = -1;
        context.Completions.Clear();
    }

    private static bool AcceptCompletionAndStartConversion(SkkEngine engine, SkkContext context)
    {
        context.CompositionBuffer.Clear();
        context.CompositionBuffer.Append(context.Completions[context.CompletionIndex]);
        context.CompletionIndex = -1;
        context.Completions.Clear();
        return StartConversion(engine, context);
    }
}
