# T-059 — Save / load games

- **Priority**: 🟡 Feature (high impact)
- **Type**: Engine / RE
- **Status**: ✅ Done (Route A) — a native versioned-JSON save (**v3**): `SaveGame` with fault-tolerant file I/O;
  `Level.CaptureSave`/`ApplySave` snapshot + rebuild the park (demolish → restore finances/clock → rebuild
  placements for free via `CommitPlacement(charge:false)`). The save now round-trips the **whole park**:
  balance + loans + clock + placed rides/shops; **per-ride research/upgrade level + in-progress research
  (fraction + park-wide queue order) + reliability/breakdown** (`Ride.RestoreProgress`); **hired staff** (role +
  wander centre + patrol zone, respawned since staff roam off the grid); **player-built coaster tracks** (laid
  tiles + per-tile heights + closed-loop flag, replayed on the rebuilt coaster via `CoasterTrack.Restore`); and
  the **goal progression** — the active/offered **challenge** (`ChallengeManager.RestoreState`, baseline
  re-anchored from the live metric so the gain keeps counting) + the **golden-ticket** win flag
  (`GoldenTicketGoals.RestoreAwarded`). A **3-slot UI**: `SaveGame.SlotPath`/`SlotExists`, **F6 cycles the slot**,
  **F5 saves / F9 loads** the active one, HUD `SAVE SLOT n [used/empty]`. Versioning is **additive +
  back-compatible** — an older save simply lacks the newer blocks, which default to "none"/level-0/reliable.
  Round-trip unit-tested (15 `SaveGameTests` + 2 `ChallengeManager.RestoreState` tests). **Route B** (original
  `.TPWS` compat) stays open — it needs a real `.TPWS` sample (none in this install), tracked under
  [T-017](T-017-tpws-saves.md).
- **Related**: [T-017](T-017-tpws-saves.md) (the `.TPWS` container is already RE'd + writable; the `SAD_*`
  module payloads are not).

## Context

OpenTPW persists only audio/settings (`GameSettings`), not game state — you can't save a park. This is the
biggest missing meta-game feature. There are two viable routes.

## What we know (RE recon)

- **Container:** the `.TPWS` format is fully RE'd + round-trips under test (T-017) — version 500, 1280-byte
  legal block, 256-byte header, BILZ+zlib payload.
- **Payload (Ghidra):** the payload is a sequence of **17 self-describing `SAD_*` modules**, each with a
  saved-vs-loaded byte-count check: `SAD_ADV_SCORING, SAD_CHEAT, SAD_SOUND, SAD_ADV, SAD_COASTERS, SAD_CAMERA,
  SAD_RSSE, SAD_FLYERS, SAD_TRACK, SAD_RIDESYS, SAD_GAMESYS, SAD_VANILLA_TIME, SAD_CLOCK, SAD_MESSAGE,
  SAD_PARTICLES, SAD_SPRITE_SCRIPTS, SAD_AI`. Each module = one serialize/deserialize function pair to RE.
  Orchestrator strings: `"Loaded savegame: %s"`, per-module `"UI: loaded %d bytes"` etc.

## Scope — pick a route

**Route A (recommended): a native OpenTPW save.** Serialize our own world state (rides, shops, staff, peeps,
finances, research/queue, clock, challenges/goals) to a versioned JSON/binary file. Pros: unblocks play
sessions now, no RE, evolves with our model. Cons: not compatible with original `.TPWS` saves.

**Route B: reverse the `.TPWS` `SAD_*` modules** for original-save compatibility. Pros: load real saves +
documents every subsystem's persisted fields. Cons: large, phased (17 modules), and **needs a real `.TPWS`
sample** to validate (none in this install — `save/` is empty).

## Scope (Route A)

1. A `SaveGame` snapshot: capture/restore each subsystem's state (entities, finances, clock, progression).
2. Versioned writer/reader (tolerant, like `GameSettings`); a save/load UI + slots.
3. Round-trip test: save → clear → load → state matches.

## Acceptance criteria

- The player can save a park and reload it with rides/shops/staff/finances/progression intact (Route A), or
  load an original `.TPWS` save (Route B, if a sample is obtained).

## Affected files (anticipated)

`source/OpenTPW/World/SaveGame.cs` (new) + per-subsystem capture hooks, `UI` save/load panel,
`source/OpenTPW.Tests/SaveGameTests.cs` (new). (Route B: `source/OpenTPW.Files/Formats/Save/`.)
