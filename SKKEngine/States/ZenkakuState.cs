namespace Tonono2.SKKEngine.States;

public class ZenkakuState : StateBase
{
    public static bool ProcessKey(SkkEngine engine, SkkKeyCommand command)
    {
        var vkCode = command.VkCode;

        if (command.Control && vkCode == SkkKeyConstants.VkJ)
        {
            return SwitchState(engine, SkkState.Hiragana);
        }

        if (vkCode == SkkKeyConstants.VkBack || vkCode == SkkKeyConstants.VkReturn || vkCode == SkkKeyConstants.VkEscape || IsNavigationKey(vkCode))
        {
            return false; // Pass through: BS, Enter, Esc, Arrows, Home/End, PgUp/Dn
        }

        if (command.Ch is { } cz && cz >= 0x20 && cz <= 0x7E)
        {
            string commitText = engine.zenkakuTable.TryGetValue(cz.ToString(), out var zenkaku) ? zenkaku : cz.ToString();
            engine.CommitProducedText(commitText);
            return true;
        }

        return false;
    }
}
