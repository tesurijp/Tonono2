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
            (SkkConstants.VkEscape, _, _) => TryResetBuffers(engine, context),
            (_, true, _) => HandleCommonCtrlKeys(engine, context, vkCode),
            (SkkConstants.VkL, false, false) when !context.IsAbbreviationMode => SwitchState(engine, SkkState.Disabled),
            (SkkConstants.VkL, false, true) when !context.IsAbbreviationMode => SwitchState(engine, SkkState.Zenkaku),
            (SkkConstants.VkQ, false, _) => HandleQKey(engine, context),
            (SkkConstants.VkSlash, false, false) when !context.IsConversionMode && !context.IsAbbreviationMode && context.CompositionBuffer.Length == 0 => EnterAbbreviationMode(engine, context),
            (SkkConstants.VkBack, false, _) => BackspaceRomaji(context) || BackspaceComposition(engine, context),
            (SkkConstants.VkReturn, false, false) when IsBufferActive(context) => CommitAll(engine),
            (SkkConstants.VkSpace, false, false) when (context.IsConversionMode || context.IsAbbreviationMode) && IsBufferActive(context) => StartConversion(engine),
            _ => command.Ch.HasValue && HandleCharInput(engine, context, command.Ch.Value)
        };
    }
}
