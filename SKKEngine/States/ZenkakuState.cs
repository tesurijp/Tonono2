namespace Tonono2.SKKEngine.States;

public static class ZenkakuState 
{
    public static bool ProcessKey(SkkEngine engine, SkkKeyCommand command)
    {
        var vkCode = command.VkCode;

        // Ctrl+J: Switch to Hiragana
        if (command.Control && vkCode == SkkKeyConstants.VkJ)
        {
            engine.CommitAll();
            engine.ChangeState(SkkState.Hiragana);
            return true;
        }

        if (vkCode == SkkKeyConstants.VkBack || vkCode == SkkKeyConstants.VkReturn || vkCode == SkkKeyConstants.VkEscape || (vkCode >= 0x21 && vkCode <= 0x28))
        {
            return false; // Pass through: BS, Enter, Esc, Arrows, Home/End, PgUp/Dn
        }

        if (command.Ch.HasValue)
        {
            var cz = command.Ch.Value;
            
            // Only handle printable characters (0x20 - 0x7E) for Zenkaku conversion
            if (cz >= 0x20 && cz <= 0x7E)
            {
                if (engine.zenkakuTable.TryGetValue(cz.ToString(), out var zenkaku))
                {
                    engine.CommitProducedText(zenkaku);
                }
                else
                {
                    engine.CommitProducedText(cz.ToString());
                }
                return true;
            }
        }

        return false;
    }
}
