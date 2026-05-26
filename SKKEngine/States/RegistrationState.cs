namespace Tonono2.SKKEngine.States;

public class RegistrationState : StateBase
{
    public static SkkActionResult ProcessKey(SkkEngine engine, SkkKeyCommand command)
    {
        var context = engine.Context;
        var vkCode = command.VkCode;

        if (IsNavigationKey(vkCode))
        {
            return Passthrough;
        }
        return (vkCode, command.Control, command.Shift) switch
        {
            (SkkConstants.VkEscape, _, _) => Handled(engine.CancelRegistration),
            (SkkConstants.VkJ, true, _) => HandleCommitAll(engine),
            (SkkConstants.VkG, true, _) => Handled(engine.CancelRegistration),
            (_, true, _) => Passthrough,
            (SkkConstants.VkReturn, false, false) when !context.IsBufferActive && context.CandidateIndex == -1 => Handled(engine.FinishRegistration),
            (SkkConstants.VkBack, false, _) when !context.IsBufferActive => Handled(engine.HandleRegistrationBackspace),
            _ => IdleState.ProcessKey(engine, command)
        };
    }
}
