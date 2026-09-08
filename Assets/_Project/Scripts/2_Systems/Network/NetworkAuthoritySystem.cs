namespace TheLastSignal.Systems.Network
{
    /// <summary>
    /// Host-side gatekeeper for incoming ServerRpc requests. Validates that a request is legal (sender
    /// owns the action, cooldowns respected, state allows it, etc.) before any 2_Systems/ class is
    /// allowed to act on it — see ARCHITECTURE_RULES.md Rule 3.
    /// </summary>
    public class NetworkAuthoritySystem
    {
        // TODO: define the set of client requests that require validation (interact, minigame input, etc.).

        public bool ValidateRequest()
        {
            // TODO: implement per-request validation and return whether the request may proceed.
            return false;
        }
    }
}
