# T-052 — Coaster track polish (`.hmp` curved-piece meshes + per-segment rotation)

- **Priority**: 🟢 Low (nice-to-have)
- **Type**: Engine / reverse engineering
- **Status**: ⚠️ Partial — the **`.hmp` format is decoded** (new `HmpFile` parser, unit-tested + verified
  against real files). Authoring curved track pieces from it + per-segment rotation remain (and need a
  working renderer to verify).
- **Parent**: [T-045](T-045-coaster-track-editor.md) (coaster editor — these are the remaining nice-to-haves).

## Context

The coaster is fully playable (station + track tool closing into a loop, authored cross-section swept
along the spline, animated CrocCar train, riders + scream, `STACKUP/DOWN` hills). Two nice-to-haves
remain from T-045.

## Scope

1. **`.hmp` curved-piece meshes**: the `.hmp` per-piece footprint/height template (`coaster1.hmp` /
   `StdPylon.hmp`) is only recon'd (header + tile-type code grid, same family as the `.map` attribute
   maps). Decode it and author proper curved track pieces from it instead of the procedural swept ribbon.
2. **Per-segment rotation** (`ACTION_COASTER_ROTATE`): allow rotating a track segment — limited value for
   a grid-aligned tile track, hence deferred, but listed for completeness.

## Done (`.hmp` decoded)

`.hmp` turned out to be a **general footprint/height template** used across the game (coaster pieces,
queue paths, hoardings/fences, sideshows, upgrades — not just track), so decoding it has broad value.
RE'd from samples + the loader `FUN_004629d0`; new `OpenTPW.Files/Formats/Map/HmpFile.cs`:

```
0x00 u16 version(3) · 0x02 u32 magic(0x0005AB1E) · 0x06 u16 scale(100) ·
0x08 u16 cols · 0x0a u16 rows · 0x0c u32 tileDataOff(0x30) ·
0x10 u32 codeGridOff · 0x14 u32 footprintOff · 0x18 3×f32 anchor/bounds
0x30: per tile a 25-byte (5×5) sub-grid of height/slope codes (the .map code family), cols*rows tiles
codeGridOff:  cols*rows bytes — one summary code per tile
footprintOff: cols*rows bytes — one footprint byte per tile (1 = solid, 0 = passable)
```

Exposed `Version/Scale/Cols/Rows/Anchor`, the per-tile 5×5 `Tiles`, `CodeGrid`, `Footprint`, `IsSolid()`.
Unit-tested (synthetic) + verified against the real `coaster1.hmp` (2×3, footprint all solid = the
station), `StdPylon.hmp`/`questra.hmp` (1×1), `ho_fos1.hmp` (fence 2×2, footprint passable).

## Remaining

1. Author curved track pieces from the per-tile height grids (needs a working renderer to verify).
2. Per-segment rotation (`ACTION_COASTER_ROTATE`).
3. ✅ **(Bonus) `.hmp` footprints now drive placement.** New `PlacementFootprint` mask (`Rectangle(w,h)` for
   the common case — kept allocation-free for the per-frame preview — and `FromHmp(HmpFile)`, which marks a
   tile solid where the footprint byte is non-zero). `PlacementGrid` gained masked `CanPlace`/`TryPlace`/
   `Clear` overloads that reserve **only** the solid tiles, so a piece with passable cells (queue paths,
   fences/hoardings) leaves those tiles walkable + buildable instead of the rectangular approximation.
   `BuildCatalogItem` carries an optional `HmpPath`, and `CommitPlacement` reserves via the resolved
   footprint (loads the `.hmp` when present, falls back to the `rw×rh` rectangle on any error). Unit-tested
   (`PlacementFootprintTests`: rectangle, passable/solid mask, fully-solid≡rectangle, masked place/clear,
   blocked-solid-tile / off-grid / water). Wiring real catalog items to their `.hmp` templates awaits a
   game install (the bundled game is a SafeDisc disc image, not an extracted tree).

## Acceptance criteria

- `.hmp` decoded to a usable per-piece template ✅; the track optionally uses it for curved pieces; a
  segment can be rotated. *(decode done; the visual application awaits a working renderer.)*

## Affected files

A new `.hmp` parser under `source/OpenTPW.Files` (`Formats/Map/HmpFile.cs`);
`source/OpenTPW/World/Terrain/PlacementFootprint.cs` (new), `source/OpenTPW/World/Terrain/PlacementGrid.cs`,
`source/OpenTPW/World/Build/BuildMode.cs`, `source/OpenTPW/World/Level.cs`,
`source/OpenTPW.Tests/PlacementFootprintTests.cs` (new). Curved-piece meshes + per-segment rotation in
`source/OpenTPW/World/Build/CoasterTrack.cs` remain (renderer-blocked).
