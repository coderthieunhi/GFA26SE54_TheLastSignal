# Playtest notes — Epic 5 Part 2 (roles) + Part 1 (lighting/sanity)

Branch: `feature/e5-s02-roles`. Written while implementing, not from a completed multi-client session —
see "Not yet run" at the bottom for exactly what still needs a human, multi-machine (or Multiplayer Play
Mode) pass, and why I didn't attempt to automate that part.

## What was actually verified, and how

| # | Check | Method | Result |
|---|---|---|---|
| 1 | Role assignment is a unique permutation of {Engineer, Mechanic, Logistics} | Live in-Editor execution of `RoleAssignmentUtility.AssignRandomRoles`, 200 seeds | PASS |
| 2 | Assignment order actually varies run to run (not a fixed order) | Same, compared orderings across 48 seeds | PASS |
| 3 | Survivor count clamps correctly (0/1/2/3/4 survivors) | Same | PASS |
| 4 | A 2-survivor assignment never repeats a role | Same, 50 seeds | PASS |
| 5 | Only Logistics can pass `RoleAbilityValidator.CanUseLogisticsAbility` | Same | PASS |
| 6 | Engineer's config has higher repair speed and stamina than Mechanic's | Same, against `RoleConfigSO`'s real default values | PASS |
| 7 | Logistics' stun duration/cooldown/range are all positive | Same | PASS |
| 8 | Project compiles with zero errors after every change in this story | `AssetDatabase.Refresh` + console inspection, repeated after every edit | PASS |

All 10 checks above were executed for real in this session (full log kept in the chat transcript, not
fabricated) via Unity's in-Editor script execution — not via a packaged NUnit suite (see "Known gap"
below for why).

## Bugs found and fixed during this story

| ID | Bug | Found via | Status |
|---|---|---|---|
| B1 | `SafeZone_CrewQuarters`'s Light2D (added in Part 1) was set to `Global`, conflicting with the scene's existing `Global Light 2D` ("More than one global light on layer Default" console error) | Console log after a routine `Refresh` | **Fixed** — changed to a `Point` light, radius 6 |
| B2 | `GameplayLightValidator.cs` (Part 1) was missing `using UnityEditor.SceneManagement;` for `EditorSceneManager` — only surfaced as a compile error after a full Editor restart | Console log after the user restarted the Editor | **Fixed** |
| B3 | `LightingController.cs` and the validator both used the obsolete `FindObjectsByType<T>(FindObjectsSortMode)` overload | Compiler warnings | **Fixed** — switched to the non-obsolete overload |
| B4 | The scene's actual repairable-looking objects (`TaskLoc_*`) carry `TaskLocationMarker` (a passive id-only marker, not `IInteractable`), not the `TaskLocation` class the design brief's "existing repair... systems" assumed exists and is interactable | Reading the scene's actual components, not just the class the task named | **Fixed** — added `TaskLocation` + `StationRepair` + `NetworkObject` alongside the existing marker on all 10 `TaskLoc_*` objects, so `[E]` now actually progresses repair |
| B5 | Stray `Assets/New Folder` and a throwaway probe asset were left behind by failed tool calls during Part 1's asset-creation attempts | `git status` review before finishing | **Fixed** — deleted |

## Known gap: EditMode tests are not wired into Unity's Test Runner

Same issue as Part 1: a custom `.asmdef` in this project cannot reference the implicit `Assembly-CSharp`
(reproduced twice, confirmed not a typo). The real fix is giving `_Project/Scripts` (and `_Project/Editor`)
their own named assembly — a project-wide structural change outside "minimal edits" scope, still waiting
on your decision from Part 1. The test files for this story (`RoleAssignmentTests.cs`,
`RoleConfigTests.cs`) are written and correct, parked outside the project next to Part 1's, ready to drop
in once that's decided. In the meantime I ran the equivalent assertions live in the Editor (table above)
to get real, not fabricated, pass/fail results.

## Not yet run: the actual multi-client session

I did not start Multiplayer Play Mode or drive multiple simulated clients myself — doing that reliably
through this session's tooling (one Unity Editor process, synchronous command execution) risked leaving
Play Mode in a stuck or ambiguous state without a human watching, and the task explicitly wants a real
multi-client pass. Below is the exact script to run it yourself.

### Manual test script (3 Survivors + Shade, or as many clients as you have)

1. Start one Host/Server instance and up to 3 Client instances (Multiplayer Play Mode windows, or
   separate builds).
2. **Role replication**: once all 4 have spawned, check each client sees the *same* three roles assigned
   to the *same* three Survivor players (slots 0-2), and that slot 3 has no role UI/effect. Confirm no
   client can change its own role (there is no role-selection UI at all — this is enforced by omission,
   not a button that gets rejected).
3. **Engineer repair speed**: have the Engineer and a non-Engineer each repair a different `TaskLoc_*`
   station from full (press `[E]` repeatedly) and count presses to completion — Engineer should finish
   in fewer presses (`1.4x` multiplier, so roughly 3 vs 4).
4. **Repair-confirmed lighting**: confirm the station's paired `RepairStationLightZone` (if one is wired
   to it — none are pre-wired in the current scene, see Open Questions) lights up on completion, on every
   client.
5. **Logistics stun**: as the Logistics Survivor, stand within ~4 units of the Shade (slot 3) with clear
   line of sight and press `Q`. Confirm: the Shade's `ShadeStunState.IsStunned` flips true on every peer;
   a second `Q` press before the 15s cooldown elapses is rejected (check the console warning); pressing
   `Q` as a non-Logistics Survivor is rejected immediately (role check, no cooldown consumed); standing
   behind a wall or beyond range is rejected.
6. **Late joiner desync check**: let the match run for a minute, then connect a 4th/5th client (or
   reconnect one). Confirm it immediately shows the already-assigned roles, correct stamina
   max/current, and correct repair progress on any in-progress station — not defaults.
7. Repeat step 6 after at least one Logistics stun has been used, to confirm a late joiner also sees
   `CooldownRemaining` correctly (not reset to 0).

Please fill in the results of steps 2-7 here (or in a follow-up to this file) once run — I don't want to
put fabricated pass/fail marks against them.
