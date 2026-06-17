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
- **Queue discipline**: each ride exposes a `RideQueue` (ordered waypoints outer end → entrance, the
  exit world point, ride duration, capacity) that owns the **line** of waiting peeps. A peep joins the
  back of the line and stands at the waypoint for its place (front = the entrance, each place back
  steps one waypoint out, clamping at the outer end), so a visible queue forms along the path instead
  of a pile on the entrance. As those ahead board, the line shifts forward and each peep's spot
  advances. Then…
- **Boarding tied to the ride**: only the **front** peep boards, and only once it has reached the
  entrance and a rider slot is free (occupies one of the ride's `Capacity` slots — sourced from the
  ride's `UsageInfo.MaxCapacity` — so the line keeps growing while the ride is full), hides for the
  ride duration, reappears at the **exit** cell, and
  re-routes. **Occupancy drives the ride's animation cycle**: a ride sits idle until its first rider
  boards (`RideQueue.Board()` edge 0→1 → `RideEngine.SetActive(true)`) and idles again when the last
  rider leaves (`Leave()` edge 1→0). Boarding runs the ride's real animation cycle — **Load → Start →
  Main (loop)** — stepping through each one-shot stage (its length from the decoded keyframe track)
  before settling into the run loop; emptying runs **End → Unload → Idle (loop, or rest)**. Stages a
  ride doesn't ship are skipped, so e.g. the monkey plays its full Load/Start/Main/End cycle while the
  totem just toggles Main↔rest. The dev park runs 40 peeps over 3 queues, each at the ride's real capacity.
- **Real ride duration**: a peep stays aboard for one full pass of the ride's running animation
  (`RideEngine.BodyLoopDuration`, decoded from the loop's keyframe track — ~11 s monkey, ~14 s totem,
  ~3 s wateride), falling back to `Info.DurationUnit × 4 s` for rides whose loop has no decoded
  keyframes, instead of a flat 5 s.
- **Excitement-weighted ride choice**: a peep picks its next ride at random *weighted by the ride's
  excitement* (`UsageInfo.ExcitementLevel`, exposed as `Ride.Excitement`), avoiding an immediate
  repeat of the last ride, so more exciting rides draw bigger crowds (verified: over ~45 s the
  wateride/75 was chosen 37×, totem/70 23×, monkey/50 16×). `Ride.Attraction` (`Info.AttractionValue`)
  is also parsed for later use.
- **Needs + visitor turnover**: each peep carries `happiness` (0–100) and `energy`. Riding an exciting
  ride raises happiness (∝ the ride's excitement); standing in a stalled queue slowly lowers it; energy
  drains while walking/queuing (~50 s of activity). A tired (energy ≤ 0) or fed-up (happiness ≤ 10)
  peep abandons its queue, walks back to its entry point, and is **recycled as a fresh arrival** — so
  the crowd turns over without churning entities (verified: ~23 visitors recycled over ~75 s as they
  tired out). This is the simulation half of the peep→ride loop; the money/income side is still to do.

## Remaining

1. **Decode the sprite format** (`.ESP`/`.TPC`/`.FPC`) and render the real animated peep sprites
   (direction + walk-cycle frames) instead of coloured billboards.
2. **Full path network**: a walkable path graph + A* so peeps route over real paths (not straight
   lines) between rides, park gate, shops. (Queue spacing *along* the path is now done — see "Queue
   discipline" above; what remains is the cross-park routing.)
3. **Per-cycle boarding sound**: the animation cycle and a real ride **duration** are wired (above —
   the duration is one full pass of the ride's running animation, ~11 s monkey / ~14 s totem, falling
   back to `Info.DurationUnit`). Still to do is playing the boarding/unloading **sound** per cycle —
   blocked on the `.MAP` audio catalog (T-016): the script-driven sounds currently resolve through an
   approximate index (e.g. the monkey plays `urinal.mp2`), so a per-board cue would just repeat that
   wrong mapping until T-016 lands.
4. **Income + staff**: ride choice, per-peep happiness/energy, and visitor turnover are done (above);
   still to do is the **money/income** side (ticket/entry revenue, ride upkeep, shops & spending) and
   **staff** (guards/handymen/entertainers), plus richer needs (hunger/thirst driving shop visits).
