# T-045 — Coaster track editor

- **Priority**: 🟡 Feature
- **Type**: Engine / UI / reverse engineering
- **Status**: ⚠️ Slice 1 done — the coaster **station** is placeable via the build catalog and
  renders/queues like any ride. The track editor proper (laying/generating track, running cars) is the
  large remainder, broken into slices below.
- **Parent**: [T-038](T-038-park-management-ui.md). **Needs**: [T-041](T-041-ride-shop-placement.md).
  **Related**: [T-032](T-032-ride-engine.md), [T-033](T-033-ride-animation-keyframes.md).

## Recon (coaster1.wad + .sam)

- **Station footprint** (`coaster1.sam` `Info.Shape`): 2×3 — `**` (station) / `<>` (the track in/out
  connectors) / `2N` (exit + entrance). So a coaster = a placeable station + a player-built track that
  attaches at the `<`/`>` connectors.
- **Assets**: track-section meshes (`Trak_sec2/3.wct`, `gtexture/`), support **pylons**
  (`StdPylon.MD2` + `StdPylonI/L/R.MD2` variants), the **car** (`CrocCar.MD2` + `CrocCarM1..3.MD2`
  animation frames), `coaster1.md2` (the 14.9 KB station), and **`coaster1.hmp` / `StdPylon.hmp`**
  (likely the track/pylon profile/path data — format TBD).
- **Editor command enum** (`ACTION_COASTER_*`): `PLACENORMAL`(55)/`PLACESPECIAL`(56), `LOFT`(57),
  `ROTATE`(58), `WOBBLE`(59), `MOVE`(60), `STACKUP`(61)/`STACKDOWN`(62), `DELETEMULTI`(63),
  `GENERATETRACK`(67)/`OLD`(64), `BACKTRACK`(65), `SETEDIT`(68)/`OLD`(66).

## Done (slice 1)

- `coaster1` added to the build catalog; placing it spawns the **`Chac`** coaster station (footprint
  2×3, entrance (1,2) / exit (0,2)), which renders, queues peeps and runs its ride animation like the
  other rides — verified in-game.

## Remaining slices

1. **Track-piece RE + laying tool** — decode `.hmp` + the track-section/pylon piece set; a tool to
   place/extend/rotate/raise (`STACKUP/DOWN`) track segments from the station's `<`/`>` connectors
   (`PLACENORMAL`, `BACKTRACK`, `ROTATE`, `DELETEMULTI`).
2. **Track generation + running cars** — `GENERATETRACK` (control path → renderable + rideable track);
   the `CrocCar` follows the generated track (physics), with peep boarding + scream (ties into the
   ride engine, T-032/T-033).

## Context

Coasters aren't placed like footprint rides — they have a **track editor**. The original exposes a
whole `ACTION_COASTER_*` command family for it, so this is its own feature, deferred until basic
placement (T-041) works.

## Acceptance criteria

- The player lays a closed coaster track, finalises it, and a car runs the track with peeps aboard.

## Affected files

new `source/OpenTPW/World/Rides/Coaster*`, `source/OpenTPW/World/Rides/RideShape.cs`,
`RideEngine.cs`, `source/OpenTPW/World/Build/*`, UI.
