# T-045 — Coaster track editor

- **Priority**: 🟡 Feature
- **Type**: Engine / UI / reverse engineering
- **Status**: ☐ To do (large; later)
- **Parent**: [T-038](T-038-park-management-ui.md). **Needs**: [T-041](T-041-ride-shop-placement.md).
  **Related**: [T-032](T-032-ride-engine.md), [T-033](T-033-ride-animation-keyframes.md).

## Context

Coasters aren't placed like footprint rides — they have a **track editor**. The original exposes a
whole `ACTION_COASTER_*` command family for it, so this is its own feature, deferred until basic
placement (T-041) works.

## Reference (original) — `ACTION_COASTER_*` enum

`PLACENORMAL`(55), `PLACESPECIAL`(56), `LOFT`(57), `ROTATE`(58), `WOBBLE`(59), `MOVE`(60),
`STACKUP`(61)/`STACKDOWN`(62) (height), `DELETEMULTI`(63), `GENERATETRACKOLD`(64)/`GENERATETRACK`(67)
(rebuild the mesh from the control path), `BACKTRACK`(65), `SETEDITOLD`(66)/`SETEDIT`(68) (enter edit).
Also `MapDelta::Rotate - illegal angle`, `c_rotate.ani`. The coaster ride WADs (`coaster1`, `coaster3`,
`minecart`, …) carry the track-piece meshes; `RideShape` already parses the `<`/`>` track connectors.

## Work

1. **RE the track model**: how control points → track pieces (the `GENERATETRACK` step) and the segment
   piece set in the coaster WADs.
2. **Editor tool**: place/extend/rotate/raise (`STACKUP/DOWN`) track segments on the grid; backtrack;
   delete; finalise (`GENERATETRACK`) into a renderable + rideable track.
3. **Run the coaster**: cars follow the generated track (ties into the ride engine, T-032/T-033, and
   peep boarding).

## Acceptance criteria

- The player lays a closed coaster track, finalises it, and a car runs the track with peeps aboard.

## Affected files

new `source/OpenTPW/World/Rides/Coaster*`, `source/OpenTPW/World/Rides/RideShape.cs`,
`RideEngine.cs`, `source/OpenTPW/World/Build/*`, UI.
