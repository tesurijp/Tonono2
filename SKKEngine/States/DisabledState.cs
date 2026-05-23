namespace Tonono2.SKKEngine.States;

public static class DisabledState
{
    public static bool ProcessKey(SkkEngine engine, SkkKeyCommand command)
    {
        if (command.Control && command.VkCode == SkkKeyConstants.VkJ)
        {
            engine.ChangeState(SkkState.Hiragana);
            return true;
        }

        return false;
    }
}
