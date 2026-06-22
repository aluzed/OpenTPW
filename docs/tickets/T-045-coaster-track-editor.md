# T-045 — Coaster track editor

- **Priority**: 🟡 Feature
- **Type**: Engine / UI / reverse engineering
- **Status**: ⚠️ Slices 1–3b (boarding+scream) done — the coaster **station** is placeable via the build
  catalog and renders/queues like any ride, a **track-laying tool** extends elevated track segments from
  the station's `>` connector and **closes into a circuit** at the `<` entry connector, a **train of
  real `CrocCar.MD2` cars** runs the track (shuttling when open, a continuous loop when closed), **real
  peeps board the train** (they ride it in view from their queue, replacing the seat-marker stand-ins)
  and the **rider scream** plays while anyone is aboard. Remaining (slice 3b polish): authentic curved
  track-piece geometry / rail profile from `.hmp`, and segment rotation / `STACKUP/DOWN` elevation editing.
- **Parent**: [T-038](T-038-park-management-ui.md). **Needs**: [T-041](T-041-ride-shop-placement.md).
  **Related**: [T-032](T-032-ride-engine.md), [T-033](T-033-ride-animation-keyframes.md).

## Recon (coaster1.wad + .sam)

- **Station footprint** (`coaster1.sam` `Info.Shape`): 2×3 — `**` (station) / `<>` (the track in/out
  connectors) / `2N` (exit + entrance). So a coaster = a placeable station + a player-built track that
  attaches at the `<`/`>` connectors.
- **Assets**: track-section meshes (`Trak_sec2/3.wct`, `gtexture/`), support **pylons**
  (`StdPylon.MD2` + `StdPylonI/L/R.MD2` variants), the **car** (`CrocCar.MD2` + `CrocCarM1..3.MD2`
  animation frames), `coaster1.md2` (the 14.9 KB station, 3 meshes), and **`coaster1.hmp` / `StdPylon.hmp`**.
- **`.hmp` format (recon)**: header `03 00  1E AB 05 00  64 00  <u16 cols> <u16 rows>  30 00 00 00` then
  offsets + float anchors (e.g. `coaster1.hmp` 210 B → 20.0/46.1/30.0 dims) then a **grid of tile-type
  codes** (`13 71 71 … 0C 75 … 37 70 …`) — a per-piece footprint/height template, same code-grid family
  as the `.map` TP2M attribute maps. `StdPylon.hmp` (75 B) is a 1×1 pylon template.
- **No pre-baked track mesh**: `coaster1c.md2` (the "course") is **not a standard MD2** — `ModelFile`
  can't parse it ("read beyond end of stream"), and non-coaster `*c.md2` don't exist. So the track is
  genuinely **procedural** (`.hmp` template + `Trak_sec`/`StdPylon` pieces along the control path);
  there's no shortcut of rendering a baked course mesh.
- **Editor command enum** (`ACTION_COASTER_*`): `PLACENORMAL`(55)/`PLACESPECIAL`(56), `LOFT`(57),
  `ROTATE`(58), `WOBBLE`(59), `MOVE`(60), `STACKUP`(61)/`STACKDOWN`(62), `DELETEMULTI`(63),
  `GENERATETRACK`(67)/`OLD`(64), `BACKTRACK`(65), `SETEDIT`(68)/`OLD`(66).

## Done (slice 1)

- `coaster1` added to the build catalog; placing it spawns the **`Chac`** coaster station (footprint
  2×3, entrance (1,2) / exit (0,2)), which renders, queues peeps and runs its ride animation like the
  other rides — verified in-game.

## Done (slice 2 — track-laying tool)

- `RideShape` parses the `<`/`>` connectors into `TrackIn`/`TrackOut` (`HasTrack`); `Ride` exposes its
  source `Archive` and `TileX/Y/W/H` + `Covers(tx,ty)`.
- New `World/Build/CoasterTrack.cs`: a chain of grid tiles anchored at the station's `>` connector.
  `CanExtend` (on-grid, 4-adjacent to the head, no overlap) / `Extend` / `Backtrack`. Each laid segment
  spawns an **elevated** track quad (the real `Trak_sec2.wct` texture, 10 units up) on a grey support
  **pylon** so it reads as a coaster rather than a ground path.
- `BuildMode` wires the tool: with a coaster selected (Default-tool click), **`T`** toggles track
  laying from its connector, left-click lays the highlighted tile (green = extendable / red = not),
  **`B`** backtracks, `T` again finishes. HUD shows the live segment count.
- Verified deterministically via the `OPENTPW_AUTOPLACE` diagnostic (`autotrack segments=7`): the tool
  anchors, extends 7 tiles, and spawns the elevated quad+pylon geometry per segment.

## Done (slice 3a — running train)

- New `World/Build/CoasterTrain.cs`: a 3-car train that advances by arc length along the track's
  elevated centre-line (`CoasterTrack.WorldPath()`), orienting each car's long axis to the local
  tangent. The track is open (not a closed loop yet) so the train **shuttles** — it reflects at each
  end and faces its actual travel direction.
- The car is the ride's **real `CrocCar.MD2` mesh** (single mesh, 76 tris, 6 materials) built with the
  same `ModelFile` + per-material `.wct` pipeline the ride/lobby meshes use; it's centred on its own
  centroid and scaled so its long axis spans ~1.3 tiles. A green-box placeholder is the fallback if
  the mesh won't load. (`coaster1c.md2` — the "course" — remains unparseable, so the track itself
  stays procedural; that's a separate decode.)
- `CoasterTrack` spawns the train in its ctor (it hides itself until ≥1 segment is laid) and exposes
  `WorldPath()` + a `Despawn()` teardown.
- **Loop closing**: `CoasterTrack` records the station's `<` entry tile; laying the track back onto it
  sets `IsClosed`, and the train then runs a **continuous loop** (the path wraps last→first) instead of
  shuttling. `Backtrack` reopens it; the HUD shows `LOOP CLOSED`.
- **Spline smoothing + smooth track ribbon** (the `GENERATETRACK` idea): the ridden centre-line is now
  a Catmull-Rom spline through the tile centres (`CoasterTrack.SmoothedPath`, periodic when closed,
  clamped when open, 8 subdivisions/segment), shared by the train (cars **glide through corners**) and
  by the **rendered track**, which is a generated textured **ribbon mesh** following that spline (real
  `Trak_sec2.wct`, repeated along its length) on support pylons — replacing the old per-tile flat quads.
  - Fix: those old quads used `3d.shader`, which expects a per-vertex-indexed texture *array* a
    hand-built mesh can't supply, so **they never actually rendered**. The ribbon uses `unlit.shader`
    (single `Color` texture sampled at the vertex UVs), so the laid track surface is visible for the
    first time.
- **Riders aboard**: `RideQueue` now back-references its `Ride` (`Ride.Queue` / `Ride.Riders`), so the
  coaster train carries **visible riders** — bright seat markers (2 per car) shown for as many seats as
  the coaster's live occupancy fills. Verified in-frame (forced full occupancy): the markers sit on the
  CrocCar, which renders clearly (red body, green croc head). Scream SFX still awaits the ride engine.
- **CrocCar animation**: the car now loads its `CrocCarM1..3` animation frames alongside the base mesh
  (4 frames) and ping-pongs through them at 8 fps (all cars in sync), so the croc chomps as it runs.
  Frames share the base frame's scale (they animate, don't pulse); a single procedural box frame is the
  fallback. Verified: `CrocCar loaded (4 frame(s))`, cycling with no exceptions.
- Verified via `OPENTPW_AUTOPLACE`: `CrocCar loaded (134 verts, 76 tris)`; an auto-laid ring around the
  station logs `autotrack segments=9 closed=True`, the looping train runs with no exceptions, and the
  smooth textured track ribbon + riders render (confirmed in-frame).

## Done (slice 3b — rideable CrocCar + scream)

- **Real peep boarding**: `Ride.Train` back-references the coaster's `CoasterTrain` (set by
  `CoasterTrack`), so when a peep reaches the front of the coaster's queue it climbs *onto* the train
  (`CoasterTrain.TryBoard`) and rides it **in view** — sitting on a seat the train repositions each frame
  (`Peep.SeatAt`, still camera-facing) instead of vanishing like an ordinary-ride rider. The old yellow
  seat-marker stand-ins are gone. A peep boarding a track-less coaster, or one past the train's 6 seats,
  falls back to the hidden-rider path. On ride-end the peep unboards and reappears at the exit as before.
- **Scream**: the train raises the ride's sustained scream the moment the first peep boards and ends it
  when the last leaves (`Ride.StartRiderScream`/`StopRiderScream` → the engine's existing
  `StartScream`/`StopScream`, which re-plays a `KidsHD` peep voice every period — T-037).
- Verified via `OPENTPW_AUTOPLACE`: peeps walk from the crowd to the auto-laid 9-segment looped coaster,
  board the train (logged `peep boarded train (n/6 seats)`, seats filling), and `STARTSCREAM`/`STOPSCREAM`
  bracket the occupancy. Build clean (0 errors), 65/0 tests.

## Remaining slices

3b (polish). **Authentic pieces** — authoring the track profile from the `.hmp` templates (rails +
    cross-ties geometry) rather than a flat textured ribbon strip, and 3D *curved* piece meshes.
    Rotation / `STACKUP/DOWN` elevation editing of segments also lands here.

## Context

Coasters aren't placed like footprint rides — they have a **track editor**. The original exposes a
whole `ACTION_COASTER_*` command family for it, so this is its own feature, deferred until basic
placement (T-041) works.

## Acceptance criteria

- The player lays a closed coaster track, finalises it, and a car runs the track with peeps aboard.

## Affected files

new `source/OpenTPW/World/Rides/Coaster*`, `source/OpenTPW/World/Rides/RideShape.cs`,
`RideEngine.cs`, `source/OpenTPW/World/Build/*`, UI.
