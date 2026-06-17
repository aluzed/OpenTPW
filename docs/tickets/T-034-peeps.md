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

- `Peep` entity: spawns on the park terrain and **wanders** — walks toward a random point within a
  home radius, picks a new one on arrival, staying dropped onto the terrain surface (`SampleHeight`).
  Rendered as an **upright camera-facing billboard** (cylindrical yaw to the camera), in varied
  clothing colours, double-sided. The dev park spawns a crowd of 40.

## Remaining

1. **Decode the sprite format** (`.ESP`/`.TPC`/`.FPC`) and render the real animated peep sprites
   (direction + walk-cycle frames) instead of coloured billboards.
2. **Path/queue following**: walk on the path network and the ride queue paths (toward an entrance),
   rather than free wander.
3. **Ride interaction**: enter via the entrance cell, "ride", exit via the exit cell; queueing.
4. **Needs/stats** (hunger/fatigue/happiness), staff (guards/handymen/entertainers), and the
   peep→ride excitement/income loop.
