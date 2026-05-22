namespace Tonono2.SKKEngine.States;

public class ZenkakuState : StateBase
{
    public static bool ProcessKey(SkkEngine engine, SkkKeyCommand command)
    {
        var vkCode = command.VkCode;

        if (IsNavigationKey(vkCode))
        {
            return false;
        }

        if (command.Control && vkCode == SkkConstants.VkJ)
        {
            return SwitchState(engine, SkkState.Hiragana);
        }

        if (command.Ch is char cz && engine.zenkakuTable.TryGetValue(cz.ToString(), out var zenkaku))
        {
            engine.CommitProducedText(zenkaku.ToString());
            return true;
        }

        return false;
    }
}
