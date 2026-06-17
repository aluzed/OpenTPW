# T-034 — Peeps (park visitors)

- **Priority**: 🟡 Feature (the park's life — backs queues, ride usage, excitement/fatigue, income)
- **Type**: Engine / format RE
- **Status**: ⚠️ Started — a wandering visitor crowd with placeholder billboards; authentic sprites,
  pathfinding and ride/queue interaction remain.
- **Related**: [T-032](T-032-ride-engine.md) (ride engine — roadmap "walk/limbo" needed a peep system).

## What exists

Peeps are **sprites** in `esprites.wad`, organised by type (`Generic/Kids`, `Generic/Guards`,
`Generic/Handymen`, `Fantasy/Entertainers`, …). Each is a trio: `.ESP` (descriptor, `ESP_FILE2.00` +
the sprite name), `.TPC` and `.FPC` (the image data — a custom encoded format, header `00 03 00 03`
then `0xAARRGGBB`-style colour runs, same family as the `base.lnd` landscape data). There are also
`2dmap/*sprite.tga` (the minimap blips).

## Done

- `Peep` entity rendered as an **upright camera-facing billboard** (cylindrical yaw to the camera), in
  varied clothing colours, double-sided, always dropped onto the terrain surface (`SampleHeight`).
- **Queue-path following**: each ride exposes a `RideQueue` (ordered waypoints outer end → entrance,
  the exit world point, ride duration, capacity); a peep walks to a queue's outer end, follows the
  waypoints to the entrance, then…
- **Boarding tied to the ride**: a peep boards when a rider slot is free (occupies one of the ride's
  `Capacity` slots — sourced from the ride's `UsageInfo.MaxCapacity` — so a queue builds at the
  entrance when the ride is full), hides for the ride duration, reappears at the **exit** cell, and
  re-routes. **Occupancy drives the ride's animation cycle**: a ride sits idle until its first rider
  boards (`RideQueue.Board()` edge 0→1 → `RideEngine.SetActive(true)`) and idles again when the last
  rider leaves (`Leave()` edge 1→0). Boarding runs the ride's real animation cycle — **Load → Start →
  Main (loop)** — stepping through each one-shot stage (its length from the decoded keyframe track)
  before settling into the run loop; emptying runs **End → Unload → Idle (loop, or rest)**. Stages a
  ride doesn't ship are skipped, so e.g. the monkey plays its full Load/Start/Main/End cycle while the
  totem just toggles Main↔rest. The dev park runs 40 peeps over 3 queues, each at the ride's real capacity.

## Remaining

1. **Decode the sprite format** (`.ESP`/`.TPC`/`.FPC`) and render the real animated peep sprites
   (direction + walk-cycle frames) instead of coloured billboards.
2. **Full path network**: a walkable path graph + A* so peeps route over real paths (not straight
   lines) between rides, park gate, shops; space the queue *along* the path rather than piling at the entrance.
3. **Per-cycle boarding sound + real duration**: the load/start/main → end/unload animation cycle is
   wired (above); still to do is playing the boarding/unloading **sound** per cycle and sourcing the
   ride **duration** from the ride script/`UsageInfo` rather than the fixed 5 s.
4. **Needs/stats** (hunger/fatigue/happiness), staff (guards/handymen/entertainers), and the
   peep→ride excitement/income loop.
