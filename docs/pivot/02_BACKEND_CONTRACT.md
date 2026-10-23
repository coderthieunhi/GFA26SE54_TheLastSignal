# 02_BACKEND_CONTRACT — Draft REST Contract (Unity ↔ Java/Spring Boot Backend)

Status: **draft, for discussion with the Java backend team**. Described here only — no backend code, and no Unity `IBackendClient` implementation code, was written to produce this document (per your instructions, Deliverable C is description-only).

This covers only the three endpoints your brief named as minimal: **validate join token**, **heartbeat**, **report match result**. It does not attempt rooms/quick-play/accounts/admin-portal endpoints (`FR-ROOM-*`, `FR-ACC-*`, `FR-ADM-*` from `TheLastSignal_Document.md`) — those belong to a separate contract once room/matchmaking orchestration is actually being built; this draft is scoped to exactly what the dedicated-server spike (`01_PIVOT_PLAN.md`) needs to run standalone first.

---

## 1. Actors and trust boundary

- **Unity dedicated server** (one per match container) — the only Unity-side actor that talks to the backend. Clients never call the backend directly for anything in this draft; they only ever receive a join token from wherever room/matchmaking logic lives (out of scope here) and present it to the dedicated server over UTP (NGO connection approval, per `01_PIVOT_PLAN.md` §1.3) — **not** over HTTP.
- **Java/Spring Boot backend** — issues join tokens, tracks container heartbeats, and records match results. Source of truth per `ARCHITECTURE_RULES.md` Rule 6.

```
Client  --(join token, over UTP connection approval payload)-->  Dedicated Server
Dedicated Server  --(HTTPS REST, this contract)-->  Backend
```

The client never calls the backend in this draft; this keeps the contract minimal and matches "the server reports results through `IBackendClient` and never trusts a client for result data" (Rule 6) literally.

## 2. How the game server authenticates to the backend

- Each dedicated server container is started by the backend's orchestrator with a **server secret** passed as a command-line argument (`-serverSecret`, per `01_PIVOT_PLAN.md` §1.2) — a single-use or short-lived credential the orchestrator generated specifically for this container/match, not a long-lived static API key shared across all servers.
- Every request below sends it as a bearer token: `Authorization: Bearer <serverSecret>`.
- The backend validates that the secret matches the specific `matchId` it was issued for (the orchestrator knows which secret it handed to which container) — a leaked secret from one match should not let a caller act on a different match's container.
- **Decided (TLS-E04-S03):** the secret is an opaque token, looked up by `matchId` and compared against a stored hash (bcrypt) — not a JWT. Unity only ever treats it as an opaque string it was given at launch either way, so this doesn't change anything client-side.
- **Decided (TLS-E04-S03): every server-auth failure returns `401`, never `403`.** This covers a missing/malformed header, a secret that's simply wrong, a secret valid for a *different* match, and a `matchId` that doesn't exist at all — all four collapse to the same `401 Unauthorized`. Unity's own handling for "secret invalid" and "matchId doesn't belong to this secret" was already identical ("deny all pending connections, log loudly" — see §3.1's error table below), so distinguishing them would have meant the backend hashing-comparing the presented secret against *every* match in the system just to pick a status code nobody acts on differently. The error table below reflects this; the three "401-family" rows have been replaced by one.
- Client-facing join tokens (what a player presents to the dedicated server, not to the backend) are a **separate** credential from the server secret — see §3. How a client originally obtains its join token (from a room/matchmaking flow) is out of scope for this draft.
- **Decided (TLS-E04-S03):** join tokens are opaque strings of the form `{matchPlayerId}.{randomSecret}` (a UUID, a literal dot, then a random opaque string) — still fully opaque from Unity's side (never parse it, just forward whatever string the client presented, exactly as `HttpBackendClient` already does). The structure only matters to the backend, which uses the prefix to look up the specific row directly instead of hash-comparing against every issued token in the system.

## 3. Endpoints

### 3.1 `POST /api/v1/matches/{matchId}/validate-token`

Called once per connecting client, from the server's `ConnectionApprovalCallback` (synchronously blocking NGO's approval decision, so this call must be fast — a timeout/fallback is specified below).

**Request**
```http
POST /api/v1/matches/a1b2c3/validate-token
Authorization: Bearer <serverSecret>
Content-Type: application/json

{
  "joinToken": "eyJhbGciOiJIUzI1NiJ9...",
  "clientTransportId": 2
}
```
- `clientTransportId` — NGO's own transport-level client id for this pending connection (not a player/account id), included so the backend's response can be correlated back to the right `ConnectionApprovalCallback` invocation if the server validates several tokens concurrently.

**Response — 200 OK (approved)**
```json
{
  "approved": true,
  "accountId": "f47ac10b-58cc-4372-a567-0e02b2c3d479",
  "displayName": "ThuanP",
  "role": "Mechanic",
  "slotIndex": 2
}
```
- `role` — one of `DroneOperator`, `LeadEngineer`, `Mechanic`, `Shade` (matches `TheLastSignal_Document.md` §3). The backend decides this (it owns room/role-selection state), not the dedicated server.
- `slotIndex` — 0-3, used by the existing `PlayerSpawner`/`PlayerSpawnPoint` slot system unchanged.
- **Decided (TLS-E04-S03):** `role` is currently a pure function of `slotIndex`, fixed for every match: slot `0` → `DroneOperator`, `1` → `LeadEngineer`, `2` → `Mechanic`, `3` → `Shade`. The backend doesn't persist a separate granular-role column (only a coarse SURVIVOR/SHADE team per player) and derives the display role from the slot at response time. This is informational for the Java team's own future reference — Unity already consumes `role` and `slotIndex` exactly as given and doesn't need to know this derivation exists.

**Response — 200 OK (rejected)**
```json
{
  "approved": false,
  "reason": "TOKEN_EXPIRED"
}
```
`reason` enum (non-exhaustive, extend as needed): `TOKEN_EXPIRED`, `TOKEN_ALREADY_USED`, `TOKEN_WRONG_MATCH`, `MATCH_FULL`, `MATCH_NOT_FOUND`.

**Error cases**
| Condition | HTTP status | Unity-side handling |
|---|---|---|
| Malformed request body | `400 Bad Request` | Treat as `approved: false`, deny connection, log. |
| Server secret missing/malformed/wrong, secret valid for a *different* match, or `matchId` doesn't exist at all | `401 Unauthorized` | Deny **all** pending connections, log loudly — this means the server itself has lost trust, which is a bigger problem than one client. (Decided TLS-E04-S03: all four of these collapse to `401`, not split across `401`/`403` — see §2.) |
| Backend unreachable / timeout | — (network-level) | **Fail closed**: deny the connection. A short client-configurable timeout (proposed default 3s) prevents one slow backend call from hanging NGO's whole approval pipeline. Against the local `StubBackendClient` (no real backend), this path never triggers. |
| Backend returns `5xx` | `5xx` | Fail closed, same as timeout. |

### 3.2 `POST /api/v1/matches/{matchId}/heartbeat`

Called on an interval (proposed default: every 10 seconds) by the running dedicated server for its entire lifetime, so the backend's orchestrator can detect a wedged/crashed container (`FR-ORC-03`).

**Request**
```http
POST /api/v1/matches/a1b2c3/heartbeat
Authorization: Bearer <serverSecret>
Content-Type: application/json

{
  "connectedPlayerCount": 3,
  "sessionState": "Gameplay",
  "uptimeSeconds": 142
}
```
- `sessionState` — the current `GameSessionState` enum value as a string, unchanged from the existing C# enum (`GameStart`, `Gameplay`, `Evacuation`, `Win`, `Lose`, `GameComplete`, `GameOver`, etc.) — reusing the audit's confirmed-reusable `GameSessionState` directly, no new enum needed on the Unity side.

**Response — 200 OK**
```json
{ "acknowledged": true }
```

**Response — 200 OK, with a directive**
```json
{ "acknowledged": true, "directive": "SHUTDOWN" }
```
- `directive` (optional, default none): `SHUTDOWN` — backend has decided this match should end (e.g., an admin action, or the match exceeded `NFR`/`BR` time limits) and the server should wind down gracefully. No other directives are proposed in this draft; adding more is a Java-team design call once orchestration logic is built out.

**Error cases**
| Condition | HTTP status | Unity-side handling |
|---|---|---|
| Server secret invalid/expired | `401` | Log; continue running (do not kill an in-progress match over a heartbeat auth hiccup) but surface this loudly for ops visibility — a repeated `401` likely means the secret rotated or expired mid-match, which the backend team should design around (e.g., a grace period) rather than the Unity side guessing. |
| Backend unreachable / timeout | — | Log and retry next interval. A dedicated server **never** pauses or aborts a match just because one heartbeat failed — only `FR-ORC-03`'s *missing heartbeats* (plural, backend-side threshold) causes the backend to abort, not the client missing one call. |

### 3.3 `POST /api/v1/matches/{matchId}/result`

Called **exactly once**, when the match reaches a terminal `GameSessionState` (`GameComplete` or `GameOver`). Per `BR-07` ("Match terminal outcomes are strictly idempotent") and `FR-RES-02` (single-submission), the dedicated server must guard against double-sending (e.g., a retry after a timeout) — see idempotency note below.

**Request**
```http
POST /api/v1/matches/a1b2c3/result
Authorization: Bearer <serverSecret>
Idempotency-Key: a1b2c3-result
Content-Type: application/json

{
  "outcome": "SurvivorVictory",
  "durationSeconds": 614,
  "players": [
    { "accountId": "f47ac10b-...", "role": "Mechanic", "incapacitated": false, "finalStatus": "Evacuated" },
    { "accountId": "9c858901-...", "role": "LeadEngineer", "incapacitated": true, "finalStatus": "Incapacitated" },
    { "accountId": "2fcab588-...", "role": "DroneOperator", "incapacitated": false, "finalStatus": "Evacuated" },
    { "accountId": "e95d2f15-...", "role": "Shade", "incapacitated": false, "finalStatus": "Active" }
  ]
}
```
- `outcome` — `SurvivorVictory`, `ShadeVictory`, or `Aborted` (server crash / Shade disconnect per `FR-NET-09`, matching `BR-07`'s idempotent terminal-outcome rule — an `Aborted` result is still exactly one final POST, not a non-report). An `Aborted` result does not affect any player's win/loss/incapacitated statistics - only a dedicated "aborted matches" counter.
- `Idempotency-Key` header — proposed as `"{matchId}-result"`, letting the backend safely ignore an accidental retry of the same result (e.g., if the HTTP response to the first attempt was lost even though the backend received and recorded it) without double-counting statistics. This directly implements `FR-RES-02`/`BR-07` at the transport level rather than leaving it to Unity-side bookkeeping alone.
- **Decided (TLS-E04-S04): idempotency is scoped to the match, not to the exact key value.** Once a match has a recorded result, every later POST for that `matchId` returns the duplicate response below - regardless of whether its `Idempotency-Key` matches the first one - and the original result is never overwritten. This is a stronger reading of `BR-07` than "the same key twice is deduplicated": since Unity always sends the one fixed key `"{matchId}-result"` for a given match, "same match" and "same key" are already equivalent in this system, so there's no real-world case this discards that simple key-matching wouldn't also have deduplicated.
- **Decided (TLS-E04-S04): the server secret is authenticated before this idempotency check runs, under the row lock described below** - a request with a wrong/expired secret always gets `401`, never a duplicate-result response, even against a match that already has a result. Concurrently, two genuinely simultaneous posts for the same match are serialized via a database row lock (`SELECT ... FOR UPDATE` on the match's row) so they cannot both observe "no result yet" and double-process it - exactly one becomes the recorded result, the other becomes a duplicate reply, and per-player statistics are updated exactly once. If a later post ever disagrees with the already-recorded result (different outcome/duration/key), the backend logs it loudly server-side for investigation, but still returns the original result unchanged - the first result always wins.

**Response — 200 OK**
```json
{ "recorded": true }
```

**Response — 200 OK (already recorded — idempotent replay)**
```json
{ "recorded": true, "duplicate": true }
```

**Error cases**
| Condition | HTTP status | Unity-side handling |
|---|---|---|
| Server secret invalid/expired | `401` | Log loudly; retry with backoff — a result must not be silently dropped. |
| Malformed payload | `400` | Log loudly; this indicates a Unity-side bug (schema mismatch), not a transient condition — do not retry blindly, surface it. |
| Backend unreachable / timeout | — | Retry with exponential backoff for a bounded period (proposed: up to 60s total) before giving up and logging a hard error for manual ops follow-up. A match result must never be silently lost — this is the one call in this contract where "fail and move on" is not acceptable. |

---

## 4. `IBackendClient` — proposed Unity-side interface (description only, no code written)

A single interface in `1_Core/Backend/`, matching exactly the three endpoints above, so the dedicated server's gameplay code (`GameManager`, `ServerBootstrap`) never talks HTTP directly — it only calls this interface, per `ARCHITECTURE_RULES.md` Rule 6:

- `Task<JoinValidationResult> ValidateJoinTokenAsync(string joinToken, ulong clientTransportId)` — wraps §3.1. `JoinValidationResult` is a small data class/struct mirroring the JSON response (`Approved`, `Reason`, `AccountId`, `DisplayName`, `Role`, `SlotIndex`).
- `Task<HeartbeatResult> SendHeartbeatAsync(int connectedPlayerCount, string sessionState, float uptimeSeconds)` — wraps §3.2. `HeartbeatResult` carries `Acknowledged` and an optional `Directive` enum (`None`, `Shutdown`).
- `Task<bool> ReportMatchResultAsync(MatchResult result)` — wraps §3.3, called exactly once by `GameManager` when it reaches a terminal state; `MatchResult` mirrors the request JSON (outcome + per-player list).

**Two implementations, matching `01_PIVOT_PLAN.md`'s task list (T5, T14):**
- **`StubBackendClient`** (local, no network calls) — `ValidateJoinTokenAsync` approves any non-empty token and round-robins slot/role assignment; `SendHeartbeatAsync` logs and returns `Acknowledged = true, Directive = None`; `ReportMatchResultAsync` logs the result and returns `true`. This is what the entire 5-day spike runs against — **the dedicated server can run a complete match with zero backend dependency**, which is the whole point of defining the interface before the Java side exists.
- **`HttpBackendClient`** (real, once the Spring Boot backend exists) — implements the same interface against §3's actual endpoints, including the timeout/retry/fail-closed behaviors specified per-endpoint above. Not built in this pivot; this document is what the Java team builds the server side of, and what a future Unity task implements the client side of, independently and in parallel, because both sides are specified here rather than inferred from each other's code.

`ServerBootstrap` picks which implementation to construct based on whether `-backendUrl` was supplied on the command line (per `01_PIVOT_PLAN.md` §1.2) — no `-backendUrl` means `StubBackendClient`, matching the spike's zero-backend-dependency goal.

---

---

## 5. Addendum (TLS-E03-S05): room start → dedicated server launch

This section was out of scope when the rest of this document was written (§0 above explicitly excludes
`FR-ROOM-*`) but room/matchmaking orchestration now exists on the backend, so it's documented here
rather than in a separate file, since it's the thing that produces the join token §3 is about.

**`POST /api/rooms/{roomCode}/start`** — called once by the room host. Request: `{"playerId": "<hostPlayerId>"}`.
Response (on success):
```json
{
  "success": true,
  "data": {
    "sessionId": "a1b2c3-...",
    "serverHost": "127.0.0.1",
    "serverPort": 7777,
    "yourJoinToken": "<caller's own opaque token, format per §2>"
  }
}
```
- `yourJoinToken` is **only ever the calling player's own token** — never any other player's. This is
  the same opaque join-token credential §2/§3.1 describe; present it to the dedicated server exactly as
  `HttpBackendClient` already does, never parse it.
- `serverHost`/`serverPort` is where to dial the real dedicated server (NGO connection), once it's
  actually listening - see the orchestration lifecycle note below for why "the response came back" does
  not always mean "the server is ready yet."
- Validation failures (not host, not enough players, not all ready, double-start) return the existing
  flat error shape with a `4xx` status, not a new shape.

**`GET /api/rooms/{roomCode}?playerId=<playerId>`** — the existing room-polling endpoint, **now requires
a `playerId` query param to receive a token at all**. The returned room/player list itself never
contains any join token, for any player, under any circumstance (checked at the JSON-serialization
level, not just by convention) — `yourJoinToken` is a separate top-level field, populated only for the
specific `playerId` the caller passed, and `null` if omitted or unrecognized.

**Unity-side changes this requires (none of this is implemented in `feat-connectserver` today):**
- `RoomLobbyUI.HandleMatchStarting` currently hardcodes `string token = "dev-token"`. It must instead
  read `yourJoinToken` from the room-start response (or a subsequent poll) and use that as the real join
  token presented to the dedicated server. Hardcoding `"dev-token"` only "works" today because
  `StubBackendClient`'s token check has a separate bug (accepts any token when the server's own
  `expectedToken` happens to equal its own default `"dev-token"`) — against a real server this path must
  not be relied on.
- `RoomManager`'s room-polling call must now pass `?playerId=<thisPlayer'sId>` to ever receive a
  non-null `yourJoinToken` — passing nothing (today's behavior) always gets `null` back, by design.
- `RoomManager`'s connect step should dial the room-start response's `serverHost`/`serverPort` rather
  than any fixed/placeholder value.

**Orchestration lifecycle (for context, not a Unity-side contract change):** a match moves
`CREATED → STARTING → RUNNING → ENDED|ABORTED|FAILED` server-side. The backend returns the start
response as soon as the container is launched (`STARTING`), not once it's confirmed running — the
dedicated server's first heartbeat (§3.2) is what flips it to `RUNNING`. A client dialing immediately
on receiving `serverHost`/`serverPort` may need to retry its connection briefly while the container
finishes booting; this draft does not propose a specific client-side retry policy.

---

*End of Deliverable C. All three deliverables (`00_AUDIT.md`, `01_PIVOT_PLAN.md`, `02_BACKEND_CONTRACT.md`) are now in `docs/pivot/`.*
