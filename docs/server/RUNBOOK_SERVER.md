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

1. Open the Unity Editor (or a Client build) on **`LegacyClientHost`/default mode** — the Direct Connect panel lives on the existing Lobby screen regardless of mode (added in TLS-E03-S02), so no special launch argument is needed on the client side.
2. In the "Direct Connect (Dedicated Server)" panel below the Host/Join controls, enter:
   - **Server IP**: `127.0.0.1` (same machine) or the server's actual LAN/public IP.
   - **Port**: `7777`.
   - **Join Token**: `dev-token` (matches the `-testToken` value in `docker-compose.yml` / step 2's `docker run` example).
3. Click **Connect**. Expected: status text shows "Connected." and the player spawns in-scene. Server-side log shows `[ServerConnectionApproval] Approved client <id>: account=stub-<id>, role=Unassigned, slot=0.`
4. Try it once more with a **wrong** token first if you want to confirm the fail-closed path: expect "Rejected: Join token rejected: TokenInvalid." and no corresponding approval log server-side.

This was already verified working by you directly (per your "test successful" confirmation during TLS-E03-S02) against an Editor-hosted dedicated server; this runbook step is the same flow against the containerized build.

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
