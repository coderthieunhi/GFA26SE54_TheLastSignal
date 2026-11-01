# Event Registry

Living registry of every `EventChannelSO` asset in the project. Keep this table updated whenever a new event channel is added, renamed, or repurposed — it is the single source of truth for who publishes and who subscribes to each event.

| Channel Asset | Payload Type | Published By | Subscribed By |
|---|---|---|---|
| ~~OnSanityChanged~~ | ~~float~~ | **Superseded (Epic 5)** — sanity is per-Survivor, not one shared value; NetworkGameState's singleton `sanity`/`ServerSetSanity` scaffold was never wired up (its own comments call it "Story 1 scaffold... still TODO") and is left as dead code, not deleted. Replaced by each player's own `PlayerSanityState` NetworkVariable (a direct C# event, not an EventChannelSO — see that class, and PlayerHealth for the same per-player pattern). | — |
| OnSanityThresholdCrossed | SanityThresholdEvent (playerId, SanityLevel) | SanitySystem (via GameManager) | *(none yet — available for HUD alerts / audio stingers / future Shade-awareness hooks)* |
| OnTaskGenerated | TaskData | TaskTicketSystem | StationView, HUDManager |
| OnMonsterStateChanged | MonsterState | MonsterFSM (via NetworkGameState) | MonsterView, HUDManager |
| OnObjectiveChanged | ObjectiveData | SimpleObjectiveSystem (via GameManager) | HUDManager |
| OnGameSessionStateChanged | GameSessionState | GameManager | HUDManager |
