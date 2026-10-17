# 01_PIVOT_PLAN — Dedicated Server Pivot Plan

Status: **planning only**. No code, scene, asset, package, or settings changes were made to produce this document. Builds on `00_AUDIT.md` and your 8 decisions (recorded there, §7).

Source-of-truth version: **Unity `6000.6.0f1 (f7f8ed4d1e24)`** (from `ProjectSettings/ProjectVersion.txt`) — use this exact version everywhere below, not "6.3 LTS".

---

## 1. Target design for the Unity side

### 1.1 Launch modes

Three modes, selected by a single new enum, not three separate projects:

```
public enum LaunchMode { LegacyClientHost, DedicatedServer, Client }
```

- **`LegacyClientHost`** — exactly today's code path (Relay + Authentication + `NetworkManagerExt.StartHost()/StartClient()`, `GameManager`'s existing `[R]`-driven flow). Zero behavior change. This is the default when no override is present, so a teammate who does nothing still gets the game that works today (Rule 7).
- **`DedicatedServer`** — new. No Relay, no Unity Services sign-in, no keyboard-driven session start, no Lobby/HUD Canvas rendering. Listens directly on UTP, validates a join token per connecting client, and starts the match programmatically once expected players have connected.
- **`Client`** — new. Connects directly to a dedicated server's IP:port with a join token instead of a Relay join code. Reuses 100% of `PlayerController`, `DoorController`, `NetworkGameState`, HUD, and EventChannelSO code unchanged — only the *connection* step differs from `LegacyClientHost`'s Client role.

**How the mode is selected** (additive, nothing deleted):
- A new `LaunchModeConfig` `ScriptableObject` (or a `--mode=` command-line argument, checked first) determines the mode at startup, read by a new, small `1_Core/Bootstrap/GameBootstrap.cs` that runs before `GameManager`/`NetworkManagerExt` do anything. In the Editor, a dropdown on this asset lets a developer pick the mode for Play Mode testing without touching any existing script.
- `GameManager`, `NetworkManagerExt`, and the view-layer guards below all read `GameBootstrap.CurrentMode` (a static, set once at boot) instead of re-deriving "am I a dedicated server" from scattered checks. This is the one new seam the whole plan hangs off; everything else is additive around it.
- Nothing in `LegacyClientHost` mode changes: it is `GameBootstrap` short-circuiting to "do exactly what the code already does today."

### 1.2 Server bootstrap flow and command-line argument contract

```
ServerBuild.exe -mode server -port 7777 -matchId "a1b2c3" -backendUrl "https://api.example.com" -serverSecret "<shared-secret>" [-maxPlayers 4] [-bindAddress 0.0.0.0]
```

| Argument | Required | Purpose |
|---|---|---|
| `-mode` | effectively yes (defaults to `server` if `UNITY_SERVER` scripting symbol is active, but explicit is safer) | Selects `DedicatedServer` launch mode. |
| `-port` | Yes | UDP port UTP binds to. Must match the container's published port (`-p <port>:<port>/udp`, decision 3). |
| `-matchId` | Yes | Opaque match identifier the backend assigned when it started this container. Used in heartbeats and the result report (Deliverable C). |
| `-backendUrl` | Yes (empty string allowed only when `-backendUrl` is omitted entirely, which selects the local stub `IBackendClient` — see §1.3 and Deliverable C) | Base URL the server's `IBackendClient` implementation calls for token validation, heartbeats, and result reporting. |
| `-serverSecret` | Yes once a real backend exists; optional against the stub | Shared secret the server presents to the backend so the backend trusts this specific container (see Deliverable C, "server auth to backend"). |
| `-maxPlayers` | No (default 4) | Survivor + Shade slot count; kept configurable for the Practice/offline mode and future testing. |
| `-bindAddress` | No (default `0.0.0.0`) | UTP bind address; `0.0.0.0` inside a container, published via Docker's `-p`. |

A new `1_Core/Bootstrap/ServerBootstrap.cs` (new file, server-mode only) parses `System.Environment.GetCommandLineArgs()` once at startup, logs the parsed values (minus the secret), and is the single place that calls `NetworkManager.StartServer()` directly against `UnityTransport` with `-bindAddress:-port` — no Relay allocation, matching decision 3.

### 1.3 Connection approval with a join token (stubbed first)

- `NetworkManager.NetworkConfig.ConnectionApproval = true`; a new `ConnectionApprovalCallback` (wired by `ServerBootstrap`, not `NetworkManagerExt`, to keep the Legacy path untouched) reads the token from `ConnectionApprovalRequest.Payload` (the bytes NGO already gives you for exactly this purpose — no protocol change needed).
- A new `IBackendClient.ValidateJoinTokenAsync(string token, ulong clientId)` interface call decides approve/deny (full contract in Deliverable C). **First implementation is a local stub** (`StubBackendClient`) that approves any non-empty token string and assigns the next free survivor/Shade slot round-robin — this lets the whole spike run with zero dependency on the Java team's progress.
- On the **Client** side, the token is supplied at `StartClient()` time via `NetworkConfig.ConnectionData` — a new, small `DirectConnectUI` (5_UI, additive, not a replacement for `LobbyUI`) takes an IP, port, and token instead of a Relay room code, and calls a new `NetworkManagerExt.ConnectDirect(string ip, ushort port, byte[] tokenBytes)` method (additive, `LegacyClientHost`'s `JoinRelay`/`StartClient` path is untouched).

### 1.4 Server-side player spawn and scene flow

- Reuses NGO's existing default-player-prefab auto-spawn (`Player.prefab` is already the registered `PlayerPrefab` — nothing to change there) and `PlayerSpawner`'s existing `AdoptNetworkedPlayers()` path (already correctly branches on `IsNetworkSessionActive`, which is true on a server — §5 of the audit already flagged this as reusable unchanged).
- `ServerBootstrap`, once the backend/stub confirms the expected player count for `-matchId` has connected (or a short grace timer elapses in the stub), calls a **new** `GameManager.ServerStartMatch()` method that does exactly what `RestartSession()`/`StartSession()` already do today — this new method exists only because the current sole entry point is a keyboard press (audit §2, highest-priority finding), **not** because the underlying session-start logic needs to change.
- Scene flow stays single-scene for the spike (`NetworkTestScene`, loaded by both the dedicated server build and clients) — matching what already exists, not introducing a Lobby/Gameplay scene split in week one. A server-only "headless" scene variant is a candidate follow-up once §1.5's guards are proven, not a spike requirement.

### 1.5 What is disabled under server mode (`UNITY_SERVER` / `DedicatedServer` launch mode)

| Component | Action | Why |
|---|---|---|
| `GameManager.InitializeUnityServicesAsync()` | Skipped entirely when `LaunchMode == DedicatedServer`. | Audit §2/§4 — dedicated server must not depend on Unity Services/Relay reachability. |
| `NetworkManagerExt.CreateRelay()`/`JoinRelay()` | Never called in server mode (new `ConnectDirect`/`ServerBootstrap` paths used instead). | Decision 3. |
| `LobbyUI`, `HUDManager`, `InteractionPromptUI`, `MiniGamePanel` | Their root Canvas/GameObject is deactivated when `LaunchMode == DedicatedServer` (a 2-line guard in each `Awake()`, or — preferred — one guard on their shared parent Canvas GameObject if they share one). | Audit §3 — Rule 2's explicit "must be disabled when running as the server". Not a crash risk, but wasted work and a rule violation. |
| `CameraFollow` (and the `Camera` GameObject it sits on) | Camera GameObject deactivated server-side. | No render target exists on a headless build; already unreachable via code (`BindLocalCamera` is `IsOwner`-gated) but the inert `Camera` component itself is cleaned up for clarity. |
| `SpawnManager`'s `autoPositionLocalPlayer` `Start()` behavior | Guarded behind `LaunchMode != DedicatedServer` (minimal, additive guard — **not** a rewrite of `SpawnManager`, which stays untouched per decision 4; this is the one exception decision 7 explicitly allows: a guard against headless misbehavior, added during the spike). | Audit §2 — confirmed live, unconditional `GameObject.Find("Player")` with zero network awareness; left alone it would race `PlayerSpawner`'s own placement on a server build. |
| `DoorIndicatorLight` (`Light2D`) | Left as-is for the spike (cosmetic-only, non-crashing); candidate for a guard in the post-spike hardening story, not required to hit the spike's DoD. | Audit §3 — Low risk, not blocking. |

### 1.6 Running the server in the Editor, as a Linux build, and in Docker

- **In the Editor (day 1-3 of the spike):** Unity 6's Multiplayer Roles / Play Mode lets a Play session run as Server-only without a build — but since `m_EnableMultiplayerRoles: 0` today (audit §6), the simplest path for the spike is: a normal Editor Play session with `GameBootstrap`'s mode set to `DedicatedServer` via the `LaunchModeConfig` asset, which calls `NetworkManager.StartServer()` instead of `StartHost()`. This needs no Editor module install and is how Days 1-2 validate the bootstrap/approval logic before touching a real headless build.
- **As a built Linux executable (day 4):** `File > Build Profiles > Dedicated Server > Linux`, `-batchmode -nographics -mode server -port 7777 ...`. Requires the **Linux Dedicated Server Build Support** module (manual prerequisite, §4) installed against this exact Editor version (`6000.6.0f1`).
- **In Docker (day 4-5):** a minimal Dockerfile (`FROM debian:bookworm-slim` or similar, copy the Linux server build output, `ENTRYPOINT ["./ServerBuild.x86_64", ...]`) with the container's UDP port published: `docker run -p 7777:7777/udp ...`. UTP is UDP-based, so the publish flag **must** include `/udp` — a plain `-p 7777:7777` (TCP) will silently not work. This is called out explicitly because it's an easy, hard-to-diagnose mistake.

### 1.7 Hidden information — plan only, not implemented this pivot

Per `ARCHITECTURE_RULES.md` Rule 5, each client should receive only what its own role is allowed to see (e.g., a Survivor should never receive The Shade's position when out of FOV). NGO's `NetworkObject` supports per-client visibility (`NetworkObject.NetworkShow/NetworkHide`, or a custom `CheckObjectVisibility` delegate) and per-client `NetworkVariable` values are not natively supported (a `NetworkVariable` replicates the same value to every observer; true per-client-different values need either `NetworkShow/Hide` of separate objects, or a custom snapshot/RPC layer). This needs its own design spike once a Shade entity exists — **out of scope for this pivot's 5-day spike and the plan beyond this note**, flagged here only so it isn't forgotten when Shade work begins.

---

## 2. Task list (15 tasks)

| # | Task | Files likely touched | Definition of Done (observable in Play) | Est. hours | Risk | Dependencies | Manual step? |
|---|---|---|---|---|---|---|---|
| T1 | `LaunchMode` enum + `GameBootstrap` + `LaunchModeConfig` asset | New: `1_Core/Bootstrap/LaunchMode.cs`, `GameBootstrap.cs`, `LaunchModeConfig.cs` | Switching the asset's dropdown and pressing Play logs the active mode before any other script runs. | 4 | Low | None | No |
| T2 | `ServerBootstrap`: CLI arg parsing | New: `1_Core/Bootstrap/ServerBootstrap.cs` | Running a build with `-mode server -port 7777 -matchId X ...` logs every parsed value except the secret. | 4 | Low | T1 | No |
| T3 | `NetworkManagerExt.StartServerDirect()` / `ConnectDirect()` (additive) | `NetworkManagerExt.cs` | In Editor, `DedicatedServer` mode binds and listens on the given port with zero Relay/Auth calls in the console. | 3 | Low | T2 | No |
| T4 | Bypass Unity Services/Relay in server mode | `GameManager.cs` (guard `InitializeUnityServicesAsync`) | A server-mode Play session never logs "Unity Services ready" / never calls Authentication. | 2 | Low | T1 | No |
| T5 | Stub join-token connection approval | New: `1_Core/Backend/IBackendClient.cs`, `StubBackendClient.cs`; `ServerBootstrap.cs` (wires `ConnectionApprovalCallback`) | A client connecting with any non-empty token is approved and spawned; an empty token is rejected with a visible disconnect reason on the client. | 6 | Medium | T3 | No |
| T6 | Headless view-layer guards | `LobbyUI.cs`/`HUDManager.cs`/`InteractionPromptUI.cs`/`MiniGamePanel.cs` (Canvas-level guard), `SpawnManager.cs` (minimal `LaunchMode` guard around its `Start()` auto-position only — no other change) | Running as `DedicatedServer` produces no Canvas/Camera console warnings, and `SpawnManager` does not reposition anything via `GameObject.Find`. | 5 | Low | T1 | No |
| T7 | `GameManager.ServerStartMatch()` (keyboard-free entry point) | `GameManager.cs` | A dedicated server build with no input devices attached still starts a match and spawns players, with zero reliance on `Keyboard.current`. | 3 | Medium | T2, T3, T5 | No |
| T8 | Two-client spawn/move/sync on one server | `PlayerSpawner.cs` (small additive hook if slot assignment needs the token's role — stubbed as round-robin otherwise, no change needed) | Two separate client processes connect to one running server, each spawns at a distinct point, moves independently, and sees the other move. | 6 | Medium | T5, T7 | No |
| T9 | `DirectConnectUI` (Client launch mode UI) | New: `5_UI/DirectConnectUI.cs` | A client build/Editor instance can type an IP, port, and token and connect to a running dedicated server (no Relay code path touched). | 5 | Medium | T1, T5 | No |
| T10 | Disconnect safety on a dedicated server | Verification + any null-ref fixes surfaced in `NetworkSessionManager.cs`/`GameManager.cs` for the no-Host-as-player case | Force-closing one client process leaves the server process and the remaining client running with no exception in either console. | 4 | Medium | T8 | No |
| T11 | Linux Dedicated Server build pipeline | New: `Assets/Editor/ServerBuildScript.cs` (optional convenience) | `File > Build Profiles > Dedicated Server > Linux` (or the editor script) produces a runnable headless Linux executable. | 4 | Medium | T1–T7 | **Yes** — Linux Dedicated Server Build Support module must already be installed (§4). |
| T12 | Dockerfile + local container run | New: `Dockerfile`, optionally `docker-compose.yml` | `docker build` then `docker run -p 7777:7777/udp ...` starts the container and logs "listening"; a client on the host OS connects to `127.0.0.1:7777`. | 4 | Medium | T11 | **Yes** — Docker Desktop + WSL2 must already be installed (§4). |
| T13 | Cross-network reachable target + final vertical slice | No new files — infrastructure + verification | Two clients on genuinely different networks connect via the chosen target's public IP:port, spawn, move, see each other, open an existing Epic-1 door through the server, and one disconnecting doesn't crash the server. | 3 (your/team time, excludes VM/VPN provisioning time) | Medium-High | T12 | **Yes** — provisioning a cloud VM or VPN mesh (§4). |
| T14 | `IBackendClient` stub wiring (heartbeat + result-report no-ops) | `StubBackendClient.cs` (extends T5's stub) | Server logs a stub heartbeat line on an interval and a stub "match result reported" line on match end, with zero real HTTP calls. | 4 | Low | T2 | No |
| T15 | **Story: Server authority hardening** (scheduled week 7, immediately after the spike — see §3's placement note, not a spike task) | `ExitZone.cs`, `HazardZone.cs`, `PlayerController.TriggerInteraction()` fallback path | A modified/cheating client can no longer self-report reaching the exit, dying, or interacting with any station — every such outcome requires a server-validated `ServerRpc`. | 16–24 (sized as a story, not a small task) | **High** | Spike complete (T1–T13) | No |

---

## 3. Day-by-day 5-day dedicated-server spike

Scope is deliberately narrow, per decision 7: **build, connect, spawn, move, sync, disconnect.** Client-trust hardening (T15) is explicitly *not* in the spike — it is scheduled as its own story immediately afterward, in week 7, High priority, ahead of any Shade work.

| Day | Focus | Tasks | Network target | End-of-day check |
|---|---|---|---|---|
| **1** | Bootstrap & mode switch, localhost only | T1, T2, T4, (start T3) | Editor-only, localhost | `DedicatedServer` mode Play session starts listening with zero Relay/Auth calls. |
| **2** | Headless guards & stub join approval | T3 (finish), T5, T6, (start T7) | Editor "server" + Editor "client" (two Editor instances via MPPM, or one Editor + one dev build), localhost | A client with a stub token connects, is approved, and spawns; Canvas/Camera guards confirmed inert server-side. |
| **3** | Two-client LAN test & disconnect safety | T7 (finish), T8, T9, T10 | Two processes on the same LAN (or same machine, two processes) | Two players move, see each other, open an existing Epic-1 door through the server; killing one client leaves the server and the other client stable. |
| **4** | Linux headless build + Docker, local only | T11, T12 | `127.0.0.1` against the container | Containerized Linux dedicated server accepts a local client connection. |
| **5** | Cross-network reachability + full vertical slice | T13, T14 | Cloud VM public IP **or** VPN mesh (decision 3 — a home NAT is explicitly out, per your instruction) | Full slice, see Go criteria below. |

### Go / Stop criteria

**GO (spike succeeded) — all of:**
1. The headless Linux container starts via `docker run -p <port>:<port>/udp` and logs "listening" without crashing.
2. Two clients on two different networks (cloud VM or VPN target, not LAN) each connect using the server's reachable IP:port and a stub join token.
3. Both clients spawn, move independently, and each sees the other move.
4. At least one existing Epic-1 interaction (a networked door) works through the dedicated server and is visible to both clients.
5. Force-closing one client process does not crash the server process or desync/crash the remaining client (Story 3.6's existing disconnect-handling design is the template here, re-verified in a true server context rather than a Host-as-player context).

**STOP / escalate (do not silently continue) — any of:**
- NGO's direct `StartServer()` (no Relay) cannot establish *any* stable connection by the end of Day 2, even on localhost — this is the foundational risk; escalate immediately rather than spending Days 3-5 on a broken base.
- Cross-network reachability (cloud VM or VPN) cannot be established by Day 5 — fall back to a LAN-only demo for the review and flag the networking/infra gap explicitly; do not claim the spike fully succeeded.
- The Linux Dedicated Server Build Support module cannot produce a working headless executable by Day 4 (install friction, licensing, or a machine that can't run the module) — fall back to a **Windows headless** server (`-batchmode -nographics` on a Standalone Windows build) as an interim Day-4/5 target, and flag true Linux-container support as a carried-over risk into the following week rather than silently declaring it done.

---

## 4. Manual prerequisites (you perform these — not done by me)

1. **Unity Editor version lock.** Every teammate installs **exactly** `6000.6.0f1 (f7f8ed4d1e24)` via Unity Hub (`Installs` → `Locate or install` → match the build hash, not just the `6000.6.0f1` label, since LTS/non-LTS point releases can share a label but differ in hash). Do not upgrade or downgrade — per your instruction, the installed version is the source of truth, not the brief's "6.3 LTS" text.
2. **Linux Dedicated Server Build Support module**, installed against that exact Editor install: Unity Hub → `Installs` tab → the gear/⋮ icon next to the `6000.6.0f1` entry → `Add modules` → check **Linux Dedicated Server Build Support** (confirm the exact wording in your installed Hub version, as it has shifted across Hub releases). Required before T11.
3. **Docker Desktop**, with the WSL2 backend enabled: install Docker Desktop → `Settings` → `General` → "Use the WSL 2 based engine" checked. Requires a WSL2 Linux distribution installed first (`wsl --install` from an elevated PowerShell, reboot if prompted). Required before T12.
4. **A reachable cross-network target for Day 5** — pick one, both are acceptable, neither is code:
   - A small cloud VM (any provider) with a public IPv4 and a firewall/security-group rule opened for your chosen UDP port, **or**
   - A VPN mesh between two developer machines (e.g. Tailscale or ZeroTier) so each machine gets a routable address without router configuration.
   Router port-forwarding on a home NAT is explicitly **not** the first choice (per your instruction — it's not reachable without it, and it requires admin access to a router you may not control); treat it only as a last-resort fallback if neither option above is available in time.
5. **Commit the currently-uncommitted `ARCHITECTURE_RULES.md` edit** (flagged in the audit, §0.2) so it isn't lost to a stash, checkout, or a teammate's pull.

---

## 5. Risks and fallback plan

| Risk | Why it matters | Mitigation |
|---|---|---|
| Unity `6000.6.0f1` is not on the `6000.3` LTS line named in your brief/document. | Package versions, API behavior, and even which modules exist can differ from what generic "Unity 6" tutorials/docs describe. | Verify every API/package claim in this plan against what `6000.6.0f1` actually ships (as the audit already did for NGO/Transport/Services versions) before relying on it; do not copy guidance written for a different release line without checking. |
| NGO direct-connect (no Relay) may simply not traverse real-world NAT/firewalls without the cloud-VM/VPN target. | This is the single highest-probability blocker for the whole spike's Day 5 goal. | Day-5-only scope, with an explicit LAN-only fallback demo and a flagged follow-up if it fails (see Stop criteria). |
| Linux Dedicated Server Build Support module install friction (large download, admin rights, a locked-down lab machine). | Blocks T11/T12 entirely for whoever hits it. | Prerequisite called out explicitly (§4) before Day 4, not discovered on Day 4; Windows-headless fallback defined in the Stop criteria. |
| Docker Desktop + WSL2 issues on team Windows machines (BIOS virtualization disabled, Hyper-V conflicts, corporate-locked machines). | Same — blocks T12 for whoever hits it. | Same prerequisite-first approach; one teammate validates Docker locally before Day 4 arrives for everyone. |
| Team has stated zero prior experience with Docker, Linux, or server orchestration. | Steep learning curve inside a ~10-week capstone with 15 weeks total project duration per the SRS. | Spike is deliberately narrow (connect/spawn/move/sync/disconnect only); defer all hardening/orchestration complexity (T15 and beyond) until this foundation is proven. |
| Hidden-information filtering (§1.7) has no proven NGO pattern in this codebase yet. | Could become a late, expensive surprise once Shade work starts. | Explicitly deferred and flagged now (§1.7), not designed under spike time pressure. |
| The Java/Spring Boot backend does not exist yet. | Server bootstrap work could stall waiting on it. | `IBackendClient` + local stub (T5, T14) means the entire Unity-side spike runs with zero backend dependency; Deliverable C's contract lets the Java team build against the same interface independently. |

### Fallback plan — what still works if the dedicated server proves too hard

The **`LegacyClientHost`** mode is never touched or degraded by this plan (Rule 7) — Stories 3.1 through 3.6 (Relay, lobby, room codes, host authority, disconnect handling) are already built, tested, and remain fully playable and buildable throughout and after this pivot attempt, with or without a backend. If the dedicated-server spike's Go criteria aren't met, the fallback is: **ship the player-hosted prototype, optionally paired with the Java backend for accounts/profiles/room-codes/match-history only** (no containerized server orchestration, no server-side anti-cheat authority, no hidden-information filtering) — i.e., the backend's account/room value is kept even if the backend's *dedicated-server-orchestration* value is not.

---

## 6. Jira-ready stories and subtasks — Epic: "Dedicated Server & Orchestration"

**Epic: Dedicated Server & Orchestration**
> Pivot gameplay authority from player-hosted NGO sessions to a backend-orchestrated, containerized dedicated server, while preserving the existing player-hosted mode as a fallback.

- **Story: Launch-mode foundation** *(maps to T1, T4)*
  - Subtask: Implement `LaunchMode` enum + `GameBootstrap` + `LaunchModeConfig` asset
  - Subtask: Gate `GameManager.InitializeUnityServicesAsync()` behind launch mode
  - AC: Switching modes via the config asset changes startup behavior with zero impact on `LegacyClientHost`.

- **Story: Server bootstrap & direct connect** *(maps to T2, T3, T9)*
  - Subtask: `ServerBootstrap` CLI argument parsing (`-mode -port -matchId -backendUrl -serverSecret -maxPlayers -bindAddress`)
  - Subtask: `NetworkManagerExt.StartServerDirect()` / `ConnectDirect()`
  - Subtask: `DirectConnectUI` (Client mode IP:port:token entry)
  - AC: A server started via CLI args listens on the given port with no Relay/Auth calls; a client can connect to it directly by IP:port.

- **Story: Join-token connection approval (stub)** *(maps to T5, T14)*
  - Subtask: `IBackendClient` interface (see `02_BACKEND_CONTRACT.md`)
  - Subtask: `StubBackendClient` (approve-any-token, round-robin slot assignment, stub heartbeat/result-report)
  - Subtask: Wire `NetworkConfig.ConnectionApproval` + `ConnectionApprovalCallback`
  - AC: Valid-looking tokens are approved and spawn a player; empty/missing tokens are rejected with a visible reason.

- **Story: Headless server safety** *(maps to T6, T7)*
  - Subtask: Guard Lobby/HUD/Prompt/MiniGame Canvases and the Camera GameObject under server mode
  - Subtask: Guard `SpawnManager`'s auto-position `Start()` behavior under server mode (no other change to that file)
  - Subtask: `GameManager.ServerStartMatch()` keyboard-free entry point
  - AC: A dedicated server build with no input/render devices starts a match and spawns players with zero console warnings from view-layer scripts.

- **Story: Two-client connectivity proof** *(maps to T8, T10)*
  - Subtask: Verify spawn/move/sync for 2 independent client processes against 1 server
  - Subtask: Verify an existing Epic-1 door interaction works end-to-end through the server
  - Subtask: Verify one client disconnecting does not crash the server or the remaining client
  - AC: Matches the spike's Day 3 end-of-day check.

- **Story: Linux build & containerization** *(maps to T11, T12)*
  - Subtask: Configure Dedicated Server build profile for Linux
  - Subtask: Author `Dockerfile` (+ optional `docker-compose.yml`)
  - Subtask: Verify local `docker run -p <port>:<port>/udp` connectivity
  - AC: Matches the spike's Day 4 end-of-day check. **Depends on manual module/Docker install (§4).**

- **Story: Cross-network reachability proof** *(maps to T13)*
  - Subtask: Provision cloud VM (public IP + firewall rule) **or** VPN mesh
  - Subtask: Run the full vertical-slice test from two different networks
  - AC: Matches the spike's Go criteria (§3). **Depends on manual infra provisioning (§4).**

- **Story: Server authority hardening** *(maps to T15 — scheduled week 7, immediately post-spike, High priority, ahead of any Shade work, per decision 7)*
  - Subtask: Server-validate `ExitZone`/`HazardZone` win-lose triggers (replace direct `GameManager.Instance.Notify...()` calls from client-local `OnTriggerEnter2D` with a server-authoritative check)
  - Subtask: Replace `PlayerController.TriggerInteraction()`'s generic unvalidated `IInteractable.Interact()` fallback with a `ServerRpc`-based pattern (template: `DoorController`/`NetworkGameState`)
  - AC: No gameplay-critical outcome (win, loss, station interaction) can be triggered by a client without a server-side check, matching `ARCHITECTURE_RULES.md` Rule 3 and the SRS's `BR-03`/`FR-NET-06`.

---

## 7. Open decisions for you

1. **Cloud VM provider vs. VPN mesh (§4, item 4)** — no default assumed; pick whichever your team already has access to (a free-tier VM somewhere, or a VPN tool like Tailscale/ZeroTier). Either works with zero code difference, only a different IP to type into `DirectConnectUI`.
2. **Exact CLI argument names** (`-mode`, `-port`, `-matchId`, `-backendUrl`, `-serverSecret`, `-maxPlayers`, `-bindAddress`) are my proposal, matching your brief's suggested set plus two I added (`-maxPlayers`, `-bindAddress`) for completeness — confirm these names, or give me the names the Java team already expects to pass.
3. **Who owns which task** — the task list (§2) and stories (§6) are sized for 1 developer each but don't assign owners; your team should slot these against your 4 members before week 7 starts, since T11-T13 need whoever's machine has Docker/the Linux module working first.
4. **Whether admin-portal/backend work starts in parallel with the Unity spike, or waits until the spike's Go criteria are met** — Deliverable C's contract is written so the Java team *can* start immediately against a stable interface, but that's your call to make, not a technical requirement either way.

---

*End of Deliverable B. Deliverable C (`02_BACKEND_CONTRACT.md`) follows.*
