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
- **Boarding**: …boards when a rider slot is free (occupies one of the ride's `Capacity` slots, so a
  queue builds at the entrance when the ride is full), hides for the ride duration, reappears at the
  **exit** cell, and re-routes to another ride. The dev park runs 40 peeps over 3 queues (capacity 4 each).

## Remaining

1. **Decode the sprite format** (`.ESP`/`.TPC`/`.FPC`) and render the real animated peep sprites
   (direction + walk-cycle frames) instead of coloured billboards.
2. **Full path network**: a walkable path graph + A* so peeps route over real paths (not straight
   lines) between rides, park gate, shops; space the queue *along* the path rather than piling at the entrance.
3. **Tie boarding to the ride**: trigger the ride's load/start/unload animation + sound on boarding,
   and source capacity/duration from the ride's `UsageInfo` (currently fixed 4 / 5 s).
4. **Needs/stats** (hunger/fatigue/happiness), staff (guards/handymen/entertainers), and the
   peep→ride excitement/income loop.
