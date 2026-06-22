# T-035 — Peep sprite animation polish

- **Priority**: ⚪ Polish
- **Type**: Engine
- **Status**: ✅ Done (core) — camera-relative facing, idle pose, hotspot anchoring, no scale jitter, and a
  ground shadow are in for both peeps and staff. `.FPC` identified (an alternate full sprite set, not a
  shadow — the shadow is rendered procedurally instead).
- **Related**: [T-034](T-034-peeps.md) (peep sprites + directional walk cycles — done).

## Context

Peeps and staff render the real decoded `esprites.wad` sprites (`SpriteSheet` / `TpcFile`), pick a
random kid variant, and cycle the directional walk animation from the `.ESP` table (8 sectors). This
ticket was the visual refinement.

## Done

1. **Camera-relative facing** — new pure `SpriteFacing.Sector(worldDir, camRight, camForward)` projects
   the world travel direction onto the camera's horizontal axes, so the chosen directional cycle matches
   the on-screen travel direction and re-orients as the build camera orbits. Peeps/staff store the world
   move direction and recompute facing each frame. Unit-tested (`SpriteFacingTests`).
2. **Idle pose** — an idle peep/staff holds the facing cycle's first (standing) frame (walk phase resets
   to 0 when not moving), so queuing visitors stand rather than moon-walk in place.
3. **Hotspot anchoring + 4. no scale jitter** — `Billboard.MakeSprite` now bakes each frame as a
   **pixel-unit quad anchored at the frame hotspot**; callers scale by one `spriteHeight / RefHeight`
   factor (`RefHeight` = the sheet's tallest frame). Different-sized frames keep correct relative size
   (no per-frame width pulsing) with the feet planted. **Fixed a latent `TpcFile` bug**: the hotspot was
   read as `u16` at offset `0x14/0x16` (into the pixel data); it's a signed `s32` at frame-header offsets
   `12/16` (verified: kid frame 0 = `(-11,-29)`, a top-left offset from the anchor).
3'. **`.FPC` identified** — same container/codec as `.TPC`, **same frame count** but taller frames (e.g.
   kid `27×42` vs `28×32`): an **alternate full sprite set**, not a small drop-shadow. So the shadow is
   rendered **procedurally** (a shared flat dark translucent ground decal under each peep/staff, hidden
   while riding) rather than from `.FPC`.

## Remaining (nice-to-have)

- Distinct idle/sit *art* (mapping the non-walk `.ESP` entries) — needs the `.ESP` table's per-entry
  semantics RE'd (which entries are walk vs idle/special); the standing-frame idle covers the look today.
- Decide whether the taller `.FPC` set is a higher-detail LOD worth swapping in up close.

## Acceptance criteria

- Peeps face their on-screen travel direction ✅; stand in an idle pose when queuing ✅; ground shadow ✅.

## Affected files

`source/OpenTPW/World/Peep.cs`, `Staff.cs`, `SpriteSheet.cs`, `Billboard.cs`,
new `source/OpenTPW/World/SpriteFacing.cs`, `source/OpenTPW.Files/Formats/Image/TpcFile.cs`,
new `source/OpenTPW.Tests/SpriteFacingTests.cs`.
