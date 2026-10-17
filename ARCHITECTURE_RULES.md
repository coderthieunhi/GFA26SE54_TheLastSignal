# Architecture Rules (dedicated server path)

Rule 1: The server owns Systems.
Every class in 2_Systems/ is either Pure C# or a NetworkBehaviour that runs its logic only when IsServer is true. This holds for both the dedicated server and the legacy client-host mode.

Rule 2: Clients react, they do not decide.
Scripts under 3_Entities/ and 5_UI/ may read NetworkVariables and subscribe to EventChannelSO events. They must never call a method on a 2_Systems/ class directly. View scripts must also be safe on a headless server: they must not depend on cameras, UI, audio, or local input, or they must be disabled when running as the server.

Rule 3: Input goes up, state comes down.
Client input travels to the server through ServerRpc. Resulting state travels back through NetworkVariable or ClientRpc. If you are about to write code that reads local input and changes Sanity, a Station, or the Shade's state without going through the server, stop and route it through a ServerRpc.
Exception: each player's own movement and animation are owner-authoritative and are replicated to others (NetworkTransform). This is intentional.

Rule 4: The server has no local player.
Server code must not assume a local client, a camera, a HUD, local input, or LocalClientId. Guard client-only code with IsClient or IsOwner, and keep it out of the server path.

Rule 5: Hidden information.
The server sends each client only what that player is allowed to see. Do not replicate state to everyone and hide it on the client.

Rule 6: The backend is the source of truth.
Join tokens, accounts, and match results are validated and stored by the backend. The server reports results through IBackendClient and never trusts a client for result data.

Rule 7: Preserve the legacy mode.
Do not delete or break the client-host code paths. Select them through the launch mode setting so they remain available as a fallback.