namespace Tonono2.SKKEngine.States;

public class DisabledState : StateBase
{
    public static SkkActionResult ProcessKey(SkkEngine engine, SkkKeyCommand command)
    {
        if (command.Control && command.VkCode == SkkConstants.VkJ)
        {
            return Handled(() =>
            {
                engine.ResetBuffers();
                engine.CommitAll();
                engine.ChangeState(SkkState.Hiragana);
            });
        }
        return Passthrough;
    }
}
