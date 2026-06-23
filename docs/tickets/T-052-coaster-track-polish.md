# T-052 — Coaster track polish (`.hmp` curved-piece meshes + per-segment rotation)

- **Priority**: 🟢 Low (nice-to-have)
- **Type**: Engine / reverse engineering
- **Status**: ☐ To do
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

## Acceptance criteria

- `.hmp` decoded to a usable per-piece template; the track optionally uses it for curved pieces; a
  segment can be rotated.

## Affected files

`source/OpenTPW/World/Build/CoasterTrack.cs`, a new `.hmp` parser under `source/OpenTPW.Files`.
