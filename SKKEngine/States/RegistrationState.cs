namespace Tonono2.SKKEngine.States;

public class RegistrationState : StateBase
{
    public static bool ProcessKey(SkkEngine engine, SkkKeyCommand command)
    {
        var context = engine.Context;
        var vkCode = command.VkCode;

        if (IsNavigationKey(vkCode))
        {
            return false;
        }

        switch (vkCode, command.Control, command.Shift)
        {
            case (SkkConstants.VkEscape, _, _):
            {
                engine.CancelRegistration();
                return true;
            }
            case (_, true, _):
            {
                if (vkCode == SkkConstants.VkJ)
                {
                    return CommitAll(engine);
                }
                if (vkCode == SkkConstants.VkG)
                {
                    engine.CancelRegistration();
                    return true;
                }
                return false;
            }
            case (SkkConstants.VkReturn, false, false) when context.CompositionBuffer.Length == 0 && context.RomajiBuffer.Length == 0 && context.CandidateIndex == -1:
            {
                engine.FinishRegistration();
                return true;
            }
            case (SkkConstants.VkBack, false, _) when context.RomajiBuffer.Length == 0 && context.CompositionBuffer.Length == 0:
            {
                engine.HandleRegistrationBackspace();
                return true;
            }
        }

        // Delegate most input to IdleState (for Romaji/Kana conversion)
        // Since we are in RegistrationMode, CommitProducedText will append to the registrar.
        return IdleState.ProcessKey(engine, command);
    }
}
