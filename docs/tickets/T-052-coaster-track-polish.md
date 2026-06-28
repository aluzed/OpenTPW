# T-052 — Coaster track polish (`.hmp` curved-piece meshes + per-segment rotation)

- **Priority**: 🟢 Low (nice-to-have)
- **Type**: Engine / reverse engineering
- **Status**: ⚠️ Partial — the **`.hmp` format is decoded** (new `HmpFile` parser, unit-tested + verified
  against real files) and drives placement footprints. **Track banking now done** — the swept frame rolls
  into curves (the spline-track realization of per-segment rotation), verified in-game. Authoring discrete
  curved-piece *meshes* from `.hmp` remains (low value — `.hmp` is a footprint/height template, not a curve
  library).
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

## Done (this pass — track banking, the spline realization of per-segment rotation)

`ACTION_COASTER_ROTATE` rotates a discrete track piece; for our spline track the equivalent visible feature
is **banking the swept profile into curves**, which is now implemented in `CoasterTrack.RebuildRibbon`:

- A pure `CoasterTrack.BankAngle(tin, tout, gain, maxBank)` computes a signed roll from each point's
  horizontal heading change (curvature): zero on a straight, opposite signs for left/right turns, scaled by
  `BankGain` and clamped to `MaxBank` (≈34°).
- Per spline point the `(perp, up)` frame is rolled about the travel tangent by that angle (smoothed over a
  ±`BankSmooth` window so it eases through the curve), and both the authored-cross-section sweep and the
  fallback rail/tie strips use the banked `right`/`up` axes — so the track **tilts into its turns** and a
  straight section is unchanged (the banked frame collapses to the old flat one).
- Unit-tested (`CoasterTrackTests`: zero-on-straight, opposite signs left/right + symmetry, hard-turn clamp,
  gain scaling). 184 tests pass, 0 new warnings.
- **Verified in-game** (real jungle assets, `OPENTPW_AUTOPLACE` lays a closed 9-segment loop): the track
  renders as a banked ribbon — the curves visibly roll the channel profile into the turn, pylons intact, loop
  continuous, 0 exceptions. (Screenshot reviewed, not committed.)

## Remaining

1. Discrete curved-piece *meshes* from `.hmp` — **low value**: the `.hmp` decode showed it's a
   footprint/height template, not a curve-mesh library, and the procedural swept+banked ribbon already reads
   as a real coaster.
2. ✅ **(Bonus) `.hmp` footprints now drive placement.** New `PlacementFootprint` mask (`Rectangle(w,h)` for
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

- `.hmp` decoded to a usable per-piece template ✅; the track banks into its curves ✅ (segment rotation's
  spline realization, verified in-game). Discrete curved-piece meshes remain (low value).

## Affected files

A `.hmp` parser under `source/OpenTPW.Files` (`Formats/Map/HmpFile.cs`);
`source/OpenTPW/World/Terrain/PlacementFootprint.cs`, `PlacementGrid.cs`,
`source/OpenTPW/World/Build/BuildMode.cs`, `Level.cs`, `PlacementFootprintTests.cs`.
**Track banking** in `source/OpenTPW/World/Build/CoasterTrack.cs` (`BankAngle` + banked sweep frame) +
`source/OpenTPW.Tests/CoasterTrackTests.cs`. Discrete curved-piece meshes in `CoasterTrack.cs` remain.
