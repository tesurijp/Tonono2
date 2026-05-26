namespace Tonono2.SKKEngine.States;

public class ZenkakuState : StateBase
{
    public static SkkActionResult ProcessKey(SkkEngine engine, SkkKeyCommand command)
    {
        var vkCode = command.VkCode;

        if (IsNavigationKey(vkCode))
        {
            return Passthrough;
        }
        if (command.Control && vkCode == SkkConstants.VkJ)
        {
            return HandleSwitchState(engine, SkkState.Hiragana);
        }
        if (command.Ch is char cz && engine.zenkakuTable.TryGetValue(cz.ToString(), out var zenkaku))
        {
            return Handled(() => engine.CommitProducedText(zenkaku.ToString()));
        }
        return Passthrough;
    }
}
