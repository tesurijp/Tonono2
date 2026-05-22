using System.Globalization;

namespace Tonono2.SKKEngine.States;

public class IdleState : StateBase
{
    public static bool ProcessKey(SkkEngine engine, SkkKeyCommand command)
    {
        var context = engine.Context;
        var vkCode = command.VkCode;

        if (IsNavigationKey(vkCode))
        {
            return false;
        }

        return (vkCode, command.Control, command.Shift) switch
        {
            (SkkKeyConstants.VkEscape, _, _) => TryResetBuffers(engine, context),
            (_, true, _) => HandleCommonCtrlKeys(engine, context, vkCode),
            (SkkKeyConstants.VkL, false, false) when !context.IsAbbreviationMode => SwitchState(engine, SkkState.Disabled),
            (SkkKeyConstants.VkL, false, true) when !context.IsAbbreviationMode => SwitchState(engine, SkkState.Zenkaku),
            (SkkKeyConstants.VkQ, false, _) => HandleQKey(engine, context),
            (SkkKeyConstants.VkSlash, false, false) when !context.IsConversionMode && !context.IsAbbreviationMode && context.CompositionBuffer.Length == 0 => EnterAbbreviationMode(engine, context),
            (SkkKeyConstants.VkBack, false, _) => BackspaceRomaji(context) || BackspaceComposition(engine, context),
            (SkkKeyConstants.VkReturn, false, false) when IsBufferActive(context) => CommitAll(engine),
            (SkkKeyConstants.VkSpace, false, false) when (context.IsConversionMode || context.IsAbbreviationMode) && IsBufferActive(context) => StartConversion(engine, context),
            _ => command.Ch.HasValue && HandleCharInput(engine, context, command.Ch.Value)
        };
    }
}
