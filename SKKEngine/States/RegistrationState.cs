namespace Tonono2.SKKEngine.States;

public static class RegistrationState
{
    public static bool ProcessKey(SkkEngine engine, SkkKeyCommand command)
    {
        var context = engine.Context;
        var vkCode = command.VkCode;
        var shiftPressed = command.Shift;
        var ctrlPressed = command.Control;

        // Navigation keys: Pass through
        if (vkCode >= 0x21 && vkCode <= 0x28)
        {
            return false;
        }

        // ESC: Cancellation
        if (vkCode == SkkKeyConstants.VkEscape)
        {
            engine.CancelRegistration();
            return true;
        }

        // Ctrl keys
        if (ctrlPressed)
        {
            if (vkCode == SkkKeyConstants.VkJ) // Ctrl+J
            {
                engine.CommitAll();
                return true;
            }
            if (vkCode == 0x47) // Ctrl+G: Cancel
            {
                engine.CancelRegistration();
                return true;
            }
            return false;
        }

        // Return
        if (vkCode == SkkKeyConstants.VkReturn && !shiftPressed)
        {
            if (context.CompositionBuffer.Length == 0 && context.RomajiBuffer.Length == 0 && context.CandidateIndex == -1)
            {
                engine.FinishRegistration();
                return true;
            }
        }

        // Backspace
        if (vkCode == SkkKeyConstants.VkBack)
        {
            if (context.RomajiBuffer.Length > 0 || context.CompositionBuffer.Length > 0)
            {
                // Let the "normal" input logic handle it first
            }
            else
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
