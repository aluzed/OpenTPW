# T-035 — Peep sprite animation polish

- **Priority**: ⚪ Polish
- **Type**: Engine
- **Status**: ☐ To do
- **Related**: [T-034](T-034-peeps.md) (peep sprites + directional walk cycles — done).

## Context

Peeps and staff render the real decoded `esprites.wad` sprites (`SpriteSheet` / `TpcFile`), pick a
random kid variant, and cycle the directional walk animation from the `.ESP` table (8 sectors). What
remains is visual refinement.

## Remaining work

1. **Camera-relative facing** — the direction→walk-segment mapping uses *world* movement angle; rotate
   it by the camera yaw so a peep walking left on screen shows the left-facing sprite exactly.
2. **Idle / queue / sit poses** — when a peep is standing in a line or otherwise idle, hold a proper
   idle/sit frame (the non-walk segments in the `.ESP` table) instead of walk-cycle frame 0.
3. **`.FPC` companion** — same container/codec as `.TPC` (decodes via `TpcFile`); likely the drop
   shadow or an alternate frame set. Identify it and render the shadow under each peep.
4. **Hotspot anchoring** — use the per-frame `hotspotX/hotspotY` (parsed in `TpcFile.Frame`) so the
   feet stay planted and frames of different sizes don't appear to bob/shift.
5. **Scale jitter** — per-frame aspect changes the billboard width each frame; smooth or normalise so
   the silhouette doesn't pulse.

## Acceptance criteria

- Peeps face their on-screen travel direction; stand in an idle pose when queuing; optional ground shadow.

## Affected files

`source/OpenTPW/World/Peep.cs`, `Staff.cs`, `SpriteSheet.cs`, `Billboard.cs`,
`source/OpenTPW.Files/Formats/Image/TpcFile.cs`.
