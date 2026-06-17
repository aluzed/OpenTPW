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
  tired out).
- **Park economy**: `ParkFinances` tracks the bank balance. Each visitor pays a gate **entry fee** on
  arrival (and again when recycled as a fresh arrival), and a **ride ticket** every time it boards
  (`Ride.TicketPrice`, derived from excitement); each ride drains **upkeep** per second
  (`Ride.UpkeepPerSecond`, scaled by capacity). Money in/out is logged (`OPENTPW_ECON_DEBUG`) and the
  books balance (verified: from 10000 → 10609 over ~50 s = +400 gate +383 tickets −174 upkeep). Ride
  prices / entry fee are derived defaults (the original lets the player set them) pending a build/manage UI.
- **Park-stats HUD**: a top-left readout (`ParkStatsPanel`) shows the live balance and flows
  (MONEY / TICKETS / GATE / FOOD / UPKEEP / WAGES) plus the VISITORS and LITTER counts, so the economy
  and crowd are visible on screen rather than only in logs. No-op without a park (harmless in the lobby).
- **Staff (entertainers + handymen)**: `Staff` entities roam the park (role-coloured billboards —
  orange entertainers, blue handymen) and draw a **wage** every second (`ParkFinances.PayWages`, shown
  as WAGES). Entertainers lift the **happiness** of nearby visitors (`Peep.Cheer`); handymen seek out
  the nearest **litter** and pick it up.
- **Litter**: visitors occasionally drop `Litter` (tracked in `Litter.Active`); standing among litter
  sours the mood (so a filthy park drives peeps home unhappy) until a handyman clears it. Shown as
  LITTER in the HUD. Verified: litter accumulates from the crowd but stays bounded as handymen clear it,
  the books reconcile, and the sim runs crash-free.
- **Hunger + shops**: hunger builds while a peep is in the park; past a threshold it leaves the ride
  line and detours to the nearest `Shop` (a green food stall, tracked in `Shop.Stalls`), pays for a
  snack (a **FOOD** income flow) and has its hunger reset before resuming. Verified: FOOD revenue
  accrues as the crowd eats and the books reconcile. (All billboards — peeps, staff, shops — now share
  one `Billboard.Make` helper.)

## Remaining

1. **Decode the sprite format** (`.ESP`/`.TPC`/`.FPC`) and render the real animated peep sprites
   instead of coloured billboards. **Container format fully reverse-engineered via Ghidra** (no-CD
   `tpw_nocd.exe`, project `/var/tmp/nocd/ghidra_proj`, full auto-analysis); only the inner RLE
   control encoding remains. `esprites.wad` holds sprite trios under `Generic/*` / `Fantasy/*`.
   - **`.ESP`** = `ESP_FILE2.00` magic (12 bytes) + the referenced texture name (`SPR_KI.TPS`), zero-padded.
   - **`.TPC`/`.FPC`** layout (read primitive `FUN_005c4f60(dst, elemSize, count, stream)` = fread):
     ```
     u16 fmt          // = 3  (the "03 00"); fmt 2 is an alternate path
     u16 _            // = 3
     u32 frameCount   // SPR_KI 175, SPR_GU 260, SPR_HA 105, SPR_FL 130
     if fmt == 3:
       u8 palette[1024]            // 256 entries × 4 bytes RGBA  (index 0 = transparent)
     frame[frameCount]:
       u32 dataLen                 // compressed pixel-data size in bytes
       u16 width, u16 height
       u16 unkA, u16 unkB          // 128/128 on SPR_KI frame 0
       s32 hotspotX, s32 hotspotY  // signed origin (-14, -25); stored as i16 at struct +0x14/+0x16
       u8  data[dataLen]           // palette-indexed pixels, RLE-compressed (dataLen < width*height)
     ```
     (SPR_KI frame 0: dataLen=736, w=33, h=29 → 957 px, so ~1.3× compression.)
   - **Load call chain (RE'd):** `FUN_00541310` (“Loading Sprites from %s”, catalogs files) →
     `FUN_00540d90` (per-file: reads the sprite descriptor's 16-entry anim/direction table) →
     `FUN_00587db0` (reads fmt/`frameCount` + the 1024-byte palette, allocs `frameCount`×{0x60,0x18}
     structs, loops frames; for 16-bit display `DAT_008bd554==2` it remaps the palette through channel
     LUTs via `FUN_005839e0`) → `FUN_00588660` (reads the 20-byte frame header) → `FUN_00564750`
     (`malloc(dataLen)` + read `dataLen` raw bytes = the compressed indices) and `FUN_00588410`
     (surface setup). The surface lifecycle vtable is `0x701168` (`FUN_00564a80…` are ctor/dtor, not
     the codec). Not string-anchored — `.tpc`/`.fpc`/`*.ESP` are all `std::string` boilerplate; the
     `03 00 03 00` magic is never compared (loader just consumes the header).
   - **Per-frame pixel data = `height` RLE scanlines** (confirmed empirically: the command count per
     frame equals `height` for **all 175** SPR_KI frames, and the per-row literal totals trace the
     figure's silhouette). Each scanline is one outer command `count c` followed by `c` bytes of an
     **inner run-length scanline** that expands to exactly `width` pixels:
     - **transparent skip** — a control byte `b ≥ 0xF0` skips `256 − b` transparent pixels (so `0xFE`→2,
       `0xFC`→4, `0xF2`→14, `0xF0`→16). Verified: row 1 `F2 00 05 <5> F2 00` = skip14 + 5 literals +
       skip14 = 33; row 2 `FC 00 07 <7> FE 00 0A <10> F6 00` = 4+7+2+10+10 = 33.
     - **literal run** — a low control byte = pixel count, followed by that many palette-index bytes.
     - **Remaining nuance:** literal *data* bytes can also be ≥0xF0, so distinguishing a skip marker
       from literal data at a command boundary needs the authoritative inner decoder (the per-scanline
       blit/decompress routine) — the one piece not yet pinned. Empirical fit reconstructs ~⅓ of rows
       exactly; the rest hit this ambiguity.
   - **Texture pipeline mapped:** load → `FUN_00564750` stores the **compressed** scanlines at
     `surface+4` → **[decompress → flat base texture]** *(the one function still unlocated)* →
     `FUN_0055e780` generates mip levels (bilinear downsample) → `FUN_0055f780` does the **scaled
     blit** from the flat mip texture (`param_1 + n*0xD8` mip structs; its `0xF0`s are pixel-format
     masks). Sprites reach the screen via the 3D textured-quad rasterizer `FUN_0056e7e0`. So
     `FUN_0055f780`/`FUN_0055e780` operate on the *already-decompressed* flat texture — the RLE
     decompressor is upstream (reads `surface+4`, writes the flat base at `surface+0x20`).
   - Empirically the per-row skip/literal grammar reconstructs clean rows exactly (rows 0/1/2 above)
     and ~⅓ of all rows; a structural subtlety (skip↔literal disambiguation when data bytes collide
     with skip markers) isn't resolvable by inspection.
   - **To finish (recommended: dynamic trace):** break where `surface+4` is read after load and watch
     one scanline expand into `surface+0x20`; that reveals the exact rule directly. (Static alt: find
     the function that reads `surface+4` and writes `surface+0x20`.) Then render through the palette
     (index 0 = transparent). Everything up to the per-row byte stream is solved and validated; until
     then peeps/staff/shops render as flat-colour billboards (`Billboard.Make`).
2. **Full path network**: a walkable path graph + A* so peeps route over real paths (not straight
   lines) between rides, park gate, shops. (Queue spacing *along* the path is now done — see "Queue
   discipline" above; what remains is the cross-park routing.)
3. **Per-cycle boarding sound**: the animation cycle and a real ride **duration** are wired (above —
   the duration is one full pass of the ride's running animation, ~11 s monkey / ~14 s totem, falling
   back to `Info.DurationUnit`). Still to do is playing the boarding/unloading **sound** per cycle —
   blocked on the `.MAP` audio catalog (T-016): the script-driven sounds currently resolve through an
   approximate index (e.g. the monkey plays `urinal.mp2`), so a per-board cue would just repeat that
   wrong mapping until T-016 lands.
4. **Guards + build/manage UI**: ride choice, needs, turnover, the **economy** (gate, tickets, food,
   upkeep, wages), **entertainers**, **handymen + litter**, and **hunger + shops** are done (above);
   still to do are **guards** (safety/vandalism), a richer thirst need, and a player-facing build/manage
   UI to place rides/shops and set prices, the entry fee, and research/upgrades (the `Upgrades[*]`/
   `CostOf*` fields are parsed already).
