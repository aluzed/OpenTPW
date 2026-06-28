# T-059 — Save / load games

- **Priority**: 🟡 Feature (high impact)
- **Type**: Engine / RE
- **Status**: ⚠️ Core done (Route A) — a native versioned-JSON save: `SaveGame` (balance + loans + clock +
  placed rides/shops) with fault-tolerant file I/O; `Level.CaptureSave`/`ApplySave` snapshot + rebuild the
  park (demolish → restore finances/clock → rebuild placements for free via `CommitPlacement(charge:false)`);
  **F5 saves, F9 loads** the default slot. Round-trip unit-tested + verified in-game (7 placements captured,
  money restored, 5 rides/2 shops rebuilt, 0 exceptions). **Remaining (v1.x)**: staff + coaster tracks +
  fine progression (research-in-progress, the active challenge/goal state — the restored clock keeps timing
  aligned), and a save-slot UI. **Route B** (original `.TPWS` compat) is unchanged (needs a real sample).
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
