# Event Registry

Living registry of every `EventChannelSO` asset in the project. Keep this table updated whenever a new event channel is added, renamed, or repurposed — it is the single source of truth for who publishes and who subscribes to each event.

| Channel Asset | Payload Type | Published By | Subscribed By |
|---|---|---|---|
| OnSanityChanged | float (new sanity value) | SanitySystem (via NetworkGameState) | PlayerSanityView, HUDManager |
| OnTaskGenerated | TaskData | TaskTicketSystem | StationView, HUDManager |
| OnMonsterStateChanged | MonsterState | MonsterFSM (via NetworkGameState) | MonsterView, HUDManager |
