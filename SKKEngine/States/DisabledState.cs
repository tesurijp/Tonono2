namespace Tonono2.SKKEngine.States;

public class DisabledState : ISkkEditorState
{
    public bool ProcessKey(SkkEngine engine, SkkKeyCommand command)
    {
        if (command.Control && command.VkCode == SkkKeyConstants.VkJ)
        {
            engine.ChangeState(SkkState.Hiragana);
            return true;
        }

        return false;
    }
}
