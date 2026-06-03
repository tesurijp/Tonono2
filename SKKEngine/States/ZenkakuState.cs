namespace Tonono2.SKKEngine.States;

public class ZenkakuState : StateBase
{
    public static SkkActionResult ProcessKey(SkkEngine engine, SkkKeyCommand command)
    {
        var vkCode = command.VkCode;

        if(CommonPreCheck(engine,vkCode) is SkkActionResult preresult)
        {
            return preresult;
        }
        if (command.Control && vkCode == SkkConstants.VkJ)
        {
            return HandleSwitchState(engine, SkkState.Hiragana);
        }
        if (engine.zenkakuTable.TryGetValue(command.Ch , out var zenkaku))
        {
            return Handled(() => engine.CommitProducedText(zenkaku.ToString()));
        }
        return Passthrough;
    }
}
