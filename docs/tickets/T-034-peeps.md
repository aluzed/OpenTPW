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
- **Queue-path following**: each ride's queue path exposes ordered waypoints (outer end → entrance); a
  peep picks a ride's queue, walks to its outer end, follows the waypoints up to the entrance, pauses
  ("rides"), then heads to another ride's queue. The dev park spawns a crowd of 40 following 3 queues.

## Remaining

1. **Decode the sprite format** (`.ESP`/`.TPC`/`.FPC`) and render the real animated peep sprites
   (direction + walk-cycle frames) instead of coloured billboards.
2. **Full path network**: a walkable path graph + A* so peeps route over real paths (not straight
   lines) between rides, park gate, shops; queue *along* the path rather than to its outer end.
3. **Ride interaction**: actually enter via the entrance cell, "ride" (board the ride object), exit via
   the exit cell; proper queueing/capacity.
4. **Needs/stats** (hunger/fatigue/happiness), staff (guards/handymen/entertainers), and the
   peep→ride excitement/income loop.
