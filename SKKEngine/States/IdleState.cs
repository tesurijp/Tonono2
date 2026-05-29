namespace Tonono2.SKKEngine.States;

public class IdleState : StateBase
{
    public static SkkActionResult ProcessKey(SkkEngine engine, SkkKeyCommand command)
    {
        var context = engine.Context;
        var vkCode = command.VkCode;

        if(CommonPreCheck(engine,vkCode) is SkkActionResult preresult)
        {
            return preresult;
        }

        return (vkCode, command.Control, command.Shift) switch
        {
            (SkkConstants.VkEscape, _, _) => HandleTryResetBuffers(engine, context),
            (_, true, _) => HandleCommonCtrlKeys(engine, context, vkCode),
            (SkkConstants.VkL, false, false) when !context.IsAbbreviationMode => HandleSwitchState(engine, SkkState.Disabled),
            (SkkConstants.VkL, false, true) when !context.IsAbbreviationMode => HandleSwitchState(engine, SkkState.Zenkaku),
            (SkkConstants.VkQ, false, _) => HandleQKey(engine, context),
            (SkkConstants.VkSlash, false, false) when !context.IsConversionMode && !context.IsAbbreviationMode && context.CompositionBuffer.Length == 0 => HandleEnterAbbreviationMode(engine, context),
            (SkkConstants.VkBack, false, _) => HandleBackspace(engine,context),
            (SkkConstants.VkReturn, false, false) when context.IsBufferActive => HandleCommitAll(engine),
            (SkkConstants.VkSpace, false, false) when (context.IsConversionMode || context.IsAbbreviationMode) && context.IsBufferActive => Handled(engine.StartConversion),
            _ => command.Ch.HasValue ? HandleCharInput(engine, context, command.Ch.Value) : Passthrough,
        };
    }
}
