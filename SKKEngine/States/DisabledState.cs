namespace Tonono2.SKKEngine.States;

public class DisabledState : StateBase
{
    public static bool ProcessKey(SkkEngine engine, SkkKeyCommand command)
    {
        if (command.Control && command.VkCode == SkkConstants.VkJ)
        {
            engine.ResetBuffers();
            return SwitchState(engine, SkkState.Hiragana);
        }
        return false;
    }
}
