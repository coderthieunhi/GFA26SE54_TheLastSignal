# RUNBOOK_SERVER — Dedicated Server: Build, Run, Connect, Smoke Test

Status: written as part of TLS-E03-S04. The Docker build/run steps in this runbook were **not**
executed by me this session — Docker Desktop's engine was installed but not running at the time (see
§0). Everything here is accurate to the project's code and config as of this story, but treat the
exact commands as a first pass to verify yourself, not as something already proven end-to-end.

---

## 0. Prerequisites (manual — do these yourself first)

| # | What | Why | How to check |
|---|---|---|---|
| 1 | Unity Editor `6000.6.0f1 (f7f8ed4d1e24)` installed | Source-of-truth version (see `docs/pivot/00_AUDIT.md` §6) | Unity Hub → Installs |
| 2 | Linux Dedicated Server build support | Build the Linux player | **Already confirmed present on this machine** — `LinuxStandaloneSupport` ships the `linux64_server_*` build variations. If a teammate's Hub shows the build failing with a missing-module error, install "Linux Build Support (IL2CPP)" via Hub → Installs → ⋮ → Add modules — the "Dedicated Server" variant is bundled inside it in this Unity version, not a separate module. |
| 3 | Docker Desktop, **running** (not just installed) | Build/run the container | `docker info` — confirmed installed on this machine (v29.1.3, Compose v2.40.3) but the engine was **not running** this session (`docker info` reported "failed to connect to the docker API ... daemon is running?"). **Start Docker Desktop** before continuing. |
| 4 | A reachable cross-network target (cloud VM or VPN) | Day-5-style test from a different network | Only needed for the final cross-network check — see `docs/pivot/01_PIVOT_PLAN.md` §4. Not needed for the local smoke test below. |

---

## 1. Build the Linux Dedicated Server player

In the Unity Editor:

**`TheLastSignal` menu → `Build` → `Dedicated Server (Linux)`**

This runs `Assets/_Project/Editor/ServerBuildScript.cs`, which:
- Builds only the scenes enabled in Build Settings (today: `NetworkTestScene` only).
- Targets `BuildTarget.StandaloneLinux64` with `StandaloneBuildSubtarget.Server`.
- Outputs to `Builds/DedicatedServer/Linux/TheLastSignalServer` (gitignored — `/[Bb]uilds/` is already in `.gitignore`, nothing extra needed).

Watch the Console for `[ServerBuildScript] Build succeeded: ...` or a `Build <Failed/Cancelled>` error.

A Windows-headless fallback menu item exists too (`Dedicated Server (Windows, fallback)`), matching the Stop-criteria fallback in `01_PIVOT_PLAN.md` if the Linux path ever turns out unusable on a teammate's machine.

---

## 2. Build and run the container

Once step 1 has produced `Builds/DedicatedServer/Linux/TheLastSignalServer`:

```bash
docker compose up --build
```

Expected log output (stdout, via `-logFile -`):
```
[GameBootstrap] Launch mode resolved: Mode=DedicatedServer, Port=7777, ...
[GameManager] DedicatedServer using StubBackendClient for join-token validation.
[GameManager] DedicatedServer listening on 0.0.0.0:7777. Starting match.
[GameManager] State -> GameStart
...
[GameManager] State -> Gameplay
```

If the container exits immediately with a "shared library not found" error, run:
```bash
docker run --rm -it --entrypoint /bin/bash thelastsignal-server:local
ldd /server/TheLastSignalServer | grep "not found"
```
and add whatever package provides the missing `.so` to the `apt-get install` line in `Dockerfile` (see its comment — the current library list is a reasonable starting point, not one verified against an actual run yet).

**Without Compose** (equivalent, one-off):
```bash
docker build -t thelastsignal-server:local .
docker run --rm -p 7777:7777/udp thelastsignal-server:local -port 7777 -bindAddress 0.0.0.0 -matchId local-dev -testToken dev-token
```

Remember: `-p 7777:7777/udp` — the `/udp` suffix is required. A plain `-p 7777:7777` publishes TCP only and the game will silently be unreachable.

---

## 3. Connect with a client

Two different ways to connect, depending on what you're testing:

### 3a. The real end-to-end flow (RoomLobbyUI, TLS-E03-S05) — what a real player actually does

This is the flow added in TLS-E03-S05 and is the one to use for any test that isn't specifically about
the dedicated-server binary in isolation. It requires the backend running (`./mvnw spring-boot:run` in
`TLS_Backend`, see its own README) with `MATCH_LAUNCHER_TYPE=docker` so the backend launches this exact
container itself — you do not run `docker compose up`/`docker run` by hand for this path; the backend
does it for you via `DockerMatchLauncher`.

1. Open the Unity Editor (or a Client build) in **`LegacyClientHost`/default mode** — `RoomLobbyUI` is
   the default lobby screen now (added in `feat-connectserver`), with a toggle back to the old
   Direct/Relay lobby if you need it (§3b below still works unchanged, per ARCHITECTURE_RULES.md Rule 7).
2. Enter a display name, set the **Backend URL** field to wherever the Spring Boot backend is running
   (`http://localhost:8080` by default), then **Tạo phòng mới** (create a room) or **Vào phòng** (join
   one with a 6-character code from someone who already created it).
3. Once in the room lobby, click **Sẵn sàng** (ready) — every player, including the host, must be ready
   before the host's **Bắt đầu trận** (start match) button is enabled.
4. Host clicks **Bắt đầu trận**. The backend creates the match, launches a real
   `thelastsignal-server:local` container, and returns `serverHost`/`serverPort`/`yourJoinToken` (the
   host's own token) directly in that response. Every other player's client, still polling the room,
   picks up the match start on its next poll and receives **its own** `yourJoinToken` the same way — no
   client ever sees anyone else's token (checked by `RoomStartOrchestrationTest` on the backend side).
5. `RoomLobbyUI.HandleMatchStarting` then calls `NetworkManagerExt.ConnectDirect` with that real token —
   no more hardcoded `"dev-token"`. If a client somehow doesn't have a token yet (a race between the
   match starting and that client's own poll landing), it shows a readable error ("Không nhận được mã
   kết nối...") instead of connecting with nothing.
6. Expected: status text shows "Connected." and the player spawns in-scene for everyone. Server-side log
   (`docker logs tls-match-<matchId>`) shows `[ServerConnectionApproval] Approved client <id>: account=<their real backend account id>, ...` — not `stub-<n>` (that prefix only appears under the stub/no-backend path in §3b).

If a join is rejected, the error banner shows the backend's own reason (token expired/already used/wrong
match/etc. — see `docs/pivot/02_BACKEND_CONTRACT.md` §3.1's `reason` enum); there is no fail-open path.

### 3b. Local stub smoke test (no backend, no Docker) — what §2's `docker run` example is for

For testing the dedicated-server binary/container in isolation, with no backend involved at all:

1. Open the Unity Editor (or a Client build) and switch to the legacy Direct/Relay lobby panel (the
   "⮂ Chuyển sang Direct/Relay Lobby" button on `RoomLobbyUI`'s menu screen).
2. In the "Direct Connect (Dedicated Server)" panel, enter:
   - **Server IP**: `127.0.0.1` (same machine) or the server's actual LAN/public IP.
   - **Port**: `7777`.
   - **Join Token**: `dev-token` (matches the `-testToken` value in `docker-compose.yml` / step 2's `docker run` example — StubBackendClient only ever approves an exact match to this value now; see the note below).
3. Click **Connect**. Expected: status text shows "Connected." and the player spawns in-scene. Server-side log shows `[ServerConnectionApproval] Approved client <id>: account=stub-<id>, role=Unassigned, slot=0.`
4. Try it once more with a **wrong** token first if you want to confirm the fail-closed path: expect "Rejected: Join token rejected: TokenInvalid." and no corresponding approval log server-side.

This was already verified working by you directly (per your "test successful" confirmation during TLS-E03-S02) against an Editor-hosted dedicated server; this runbook step is the same flow against the containerized build.

**TLS-E03-S05 note:** `StubBackendClient` used to also accept *any* token whenever `-testToken` was left
at its default (`dev-token`) — that was a real fail-closed regression (the common case silently accepted
everything) and has been reverted. It now only ever approves an exact match to `-testToken`'s value,
unless you explicitly pass `-stubAcceptAny true` (off by default, logs a loud warning whenever it's on,
never read by `HttpBackendClient`) — only use that for a demo that has no way to hand out the real token
some other way.

---

## 4. UDP smoke test (without a full game client)

Two different things people mean by "ping test" — both are useful, neither is a substitute for the other:

**(a) Reachability check** — confirms a UDP packet can physically get from your test machine to the server's published port (useful for the Day-5 cross-network test, where firewalls/NAT are the main risk, per `01_PIVOT_PLAN.md` §3's Go/Stop criteria):
```bash
# From another machine/network, replace <server-ip>:
echo -n "ping" | nc -u -w1 <server-ip> 7777
```
Unity Transport will not reply to this — it isn't a recognized protocol handshake, so UTP silently drops it. A lack of response here is **expected and not a failure**. What you're checking is whether the packet leaves your machine and the server's own logs/`tcpdump` show it arriving — if nothing arrives server-side, that's a routing/firewall/NAT problem, not a game bug.

To confirm arrival server-side (run inside the container, needs `tcpdump` added to the image temporarily, or run on the Docker host instead):
```bash
# On the host machine running the container:
sudo tcpdump -i any udp port 7777
```

**(b) Real connectivity check** — the only test that actually proves the server works end-to-end: step 3 above (connect with a real client and a valid token). Prefer this whenever you can; use (a) only to isolate "is this a network/firewall problem" from "is this a game logic problem" when a real client can't connect.

---

## 5. Cross-network test (Day 5 / later)

Once local Docker is proven (§2-4), repeat §3 against the reachable target you provisioned per `docs/pivot/01_PIVOT_PLAN.md` §4 (cloud VM public IP, or a VPN mesh address) instead of `127.0.0.1` — same client steps, same join token, different IP. No code or config changes needed; only the IP you type into the Direct Connect panel changes.

---

## 6. What's manual vs. what's automated here

| Step | Automated by this repo | Manual (you) |
|---|---|---|
| Build the Linux player | ✅ `ServerBuildScript.cs` menu item | — |
| Build the container image | ✅ `Dockerfile` | Requires Docker Desktop running |
| Run the container locally | ✅ `docker-compose.yml` | Requires Docker Desktop running |
| Publish the UDP port correctly | ✅ already baked into `Dockerfile`/`docker-compose.yml` | — |
| Cross-network reachable target | — | Provisioning a cloud VM or VPN is entirely your call (`01_PIVOT_PLAN.md` §4) |
| Verifying the container actually boots clean | **Not done this session** — Docker engine wasn't running | You run §2 and confirm the log sequence matches §2's expected output |
| Run EditMode/PlayMode tests on every PR | ✅ `.github/workflows/unity-tests.yml` | Requires a Unity license (§8 below) configured as repo secrets once |
| Build the Linux server + push its image | ✅ `.github/workflows/build-server-image.yml` (manual-trigger or tag-triggered) | Same license requirement; you trigger it or push a `v*` tag |

---

## 7. CI image publishing (TLS-E11)

`.github/workflows/build-server-image.yml` builds the Linux Dedicated Server with GameCI's
`unity-builder` (using this repo's own `ServerBuildScript.BuildLinuxDedicatedServer` as a custom
`buildMethod`, so CI produces exactly the same output as the Editor menu item - no separate build
logic to keep in sync), then builds and pushes the same `Dockerfile` this runbook already documents to
GitHub Container Registry. It's triggered manually (`workflow_dispatch`) or by pushing a tag matching
`v*` - never on an ordinary push, since this is comparatively expensive to run on every commit.

**Image name:** `ghcr.io/coderthieunhi/thelastsignal-server` (lowercased from this repo's actual
GitHub owner, `coderthieunhi` - Docker/GHCR image references must be lowercase).

**Tags:**
- A tag-triggered run (`git tag v1.2.3 && git push --tags`) publishes `:v1.2.3` and `:latest`.
- A manually-triggered run (`workflow_dispatch`, no tag) publishes `:<commit-sha>` and `:latest`.

### How the backend picks up a new image tag

The backend never pulls this image itself as part of a `docker-compose` service - `DockerMatchLauncher`
launches it directly via `docker run` whenever a match starts, using whatever
`MATCH_LAUNCHER_IMAGE` is set to in the backend's own `.env`. To pick up a new build:

1. Set the backend's `MATCH_LAUNCHER_IMAGE` (in its `.env`, or the VM's `deploy/.env` for a real
   deployment - see the backend repo's `docs/ops/RUNBOOK_DEPLOY.md`) to the exact tag you want, e.g.:
   ```
   MATCH_LAUNCHER_IMAGE=ghcr.io/coderthieunhi/thelastsignal-server:v1.2.3
   ```
   (or `:latest` to always use whatever `build-server-image.yml` most recently published).
2. If the package is private, the backend's host (or VM) needs `docker login ghcr.io` once, with a
   token that has `read:packages` scope - `docker run` won't auto-pull a private image otherwise. See
   the backend repo's own deploy runbook for the exact login step.
3. Restart the backend process so the new `MATCH_LAUNCHER_IMAGE` value takes effect (it's read once at
   startup via `MatchLauncherProperties`, not re-read live).
4. No restart or action is needed for ALREADY-RUNNING matches - only matches started AFTER the backend
   restart use the new image.

### Rolling back to an earlier image tag

Every build is tagged with either a version tag or a commit SHA (never only `latest`), so rolling back
is the same mechanic as picking up a new tag (above) - just point `MATCH_LAUNCHER_IMAGE` at an older
tag and restart the backend. Find the tag you want from this repo's GitHub Packages page (lists every
pushed tag with its push date), or from the `build-server-image.yml` run history (each run's log names
the exact tags it pushed).

---

## 8. Branch protection (apply this by hand — GitHub doesn't let a workflow configure it)

In the repo's GitHub settings → **Branches** → add a protection rule for both `main` and `develop`:

1. **Require a pull request before merging** (disables direct pushes to the branch).
2. **Require status checks to pass before merging** → select the `test` check (from
   `.github/workflows/unity-tests.yml`) once it has run at least once (GitHub only lists checks that
   have run - this means the check won't be selectable until the Unity license secrets are configured
   and at least one PR has triggered a real run, not a skipped one).
3. Optionally: **Require branches to be up to date before merging**, **Require approvals** (≥1
   reviewer).
4. No exceptions for admins, matching "no direct push to main/develop."

## 9. Unity license for CI

GameCI's `unity-test-runner`/`unity-builder` actions need an activated Unity license to run the Editor
headless. Three repo secrets carry this: `UNITY_LICENSE`, `UNITY_EMAIL`, `UNITY_PASSWORD`. Both
workflows in this repo check for `UNITY_LICENSE` first and skip (not fail) the whole job with a clear
`::notice::` log line if it's absent - so this repo is safe to push even before you've set these up.

**To obtain and activate one** (GameCI's own documented flow - summarized here, see
[game.ci/docs/github/activation](https://game.ci/docs/github/activation) for the authoritative version
since license activation steps can change with Unity's own licensing system):

1. **Personal/free Unity license:** run `game-ci/unity-request-activation-file` locally or via a
   one-off workflow to generate an `.alf` activation request file for this exact Unity version
   (`6000.6.0f1`, per `ProjectSettings/ProjectVersion.txt`).
2. Upload that `.alf` file at [license.unity3d.com/manual](https://license.unity3d.com/manual) (sign in
   with the Unity account you want this CI identity tied to) to receive a `.ulf` license file back.
3. Add three GitHub repo secrets (Settings → Secrets and variables → Actions):
   - `UNITY_LICENSE` - the full contents of the `.ulf` file.
   - `UNITY_EMAIL` / `UNITY_PASSWORD` - the Unity account's own login, which GameCI's action also needs
     at runtime to re-activate the license each run (free/personal licenses are tied to a single
     machine identity and GameCI's images re-request activation per run).
4. **If you're on a Unity Pro/paid seat instead:** GameCI supports license-server-based activation too
   (no `.ulf` file, points at your organization's floating license server instead) - see GameCI's docs
   for the different secret set that requires (`UNITY_SERIAL` + a reachable license server), not
   documented further here since this project's exact licensing tier wasn't confirmed as part of this
   pass.
5. **If a personal license turns out not to work for CI at all** (Unity's terms restrict some personal
   license uses for automated/CI activation in certain cases - this needs checking against your actual
   account type, not assumed here): the smallest fallback is building the Linux Dedicated Server
   locally yourself (Editor menu, as this runbook's §1 already describes) and running
   `docker build`/`docker push` by hand or via a short local script, skipping GameCI/CI entirely for
   the build step - `.github/workflows/build-server-image.yml` would simply stay unused (its
   license-check job already skips cleanly) while `unity-tests.yml` would need the same license to run
   at all, so a persistently-missing license means no automated test runs either, not just no CI builds.
