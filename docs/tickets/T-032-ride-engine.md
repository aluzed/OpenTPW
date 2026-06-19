# T-032 — Ride engine: make the VM's engine opcodes do something

- **Priority**: 🟡 Feature (the central unlock — backs Batch B of the VM and real gameplay)
- **Type**: Engine
- **Status**: ⚠️ In progress — slice 1 (seam + sound + ride in-scene), stage 1 (lifecycle +
  procedural animation), stage 2 (animation system RE'd + channel-aware engine), real keyframe playback
  and the **rider scream family** done; lights / walk-limbo / coaster motion / park remain. (73/106
  opcodes implemented.)
- **Related**: [T-007](T-007-vm-opcodes-rse.md) (the VM + opcode RE), [T-033](T-033-ride-animation-keyframes.md) (animation keyframes), [05](../05-ghidra-reverse.md)/[07](../07-ghidra-render.md)/[08](../08-ghidra-animation.md).

## Problem

The ride-script VM (`RideVM`) runs, but its **63 "engine" opcodes were silent no-ops** — nothing
spawned objects, played ride sounds, animated, etc. — because there was no engine behind them. A
`Ride : Entity` existed but was never instantiated in-game. So no ride had ever run. The ride engine
is what makes a ride *do* anything, and it backs the remaining VM opcodes (T-007 Batch B) plus
`.PLB` particles, `.LIP` lip-sync and `.MAP` audio actually running.

## Done — slice 1

- ✅ **Engine seam.** `RideVM.Engine` (an `IRideEngine`, `World/Rides/`); default null → engine
  opcodes are guarded no-ops (unit tests stay pure, like the existing `ChildLoader`). New handlers in
  `VM/Handlers/Objects.cs` route `ADDOBJ`/`SPAWNSOUND`/`KILLOBJ` to `vm.Engine?.X` (arities per
  docs/06). Removed the empty duplicate `ADDOBJ` from `Misc.cs` (a duplicate `[OpcodeHandler]` crashes
  the reflection registration). `RideEngineTests` verify routing + the no-engine no-op.
- ✅ **Object registry + sound.** `RideEngine` keeps a `Dictionary<int, RideObject>` (by script
  handle) and plays ride sound objects through the game `Audio` system. (Exact sound-code→asset
  mapping is the `.MAP` catalog — [T-016](T-016-map-entry-records.md); for now it indexes `RideHD.sdt`
  and logs, an audible proof the opcode fired.)
- ✅ **A ride in the scene.** `Ride` loads a real ride from its `.wad` (VFS path omits the `.wad`
  extension), renders its main `.md2` (LobbyIsland pattern), attaches the engine + a VFS `ChildLoader`,
  and runs the VM. `Level` spawns one dev-test ride (jungle `tourride`). Verified: 'Jurassic' loads,
  5 meshes render, the VM runs and `SPAWNSOUND` routes through to audio, no crash.
- ✅ **Incidental fix.** Shaders are now shared by path (`Shader.Load`) and the hot-reload watcher is
  fault-tolerant — the lobby's ~60 per-material `FileSystemWatcher`s were exhausting the OS inotify
  instance limit and crashing ride model loads.

## Done — stage 1 (object lifecycle + procedural animation)

- ✅ **Lifecycle.** `KILLOBJ`/`FADEOBJ` despawn the object (and prune its model from `Entity.All`);
  `SETOBJPARAM` stores params on the `RideObject`.
- ✅ **Animation opcode family** routed to the engine: `TRIGANIM`/`LOOPANIM`/`TRIGANIMSPEED`/
  `FLUSHANIM`/`GETANIM` + the `_CH` (active-child) variants, plus the **`WAIT*` scheduler**
  (`WAITANIM`/`TRIGWAITANIM`/`WAIT4ANIM`) that suspends the script via the VM PC-rewind trick (like
  `WAIT`) — a ride that waits on an animation no longer silently skips the wait. Per-(object, anim)
  checks treat looping anims as never-blocking so a default idle can't hang a `WAITANIM`.
- ✅ **Procedural animation.** `RideEngine` has an object animation state machine + a per-frame
  procedural transform (a model with an active anim bobs); the ride body is registered as a self
  object playing a looping idle so the model is visibly alive. Tested (routing + `WAITANIM` rewind).

## Done — stage 2 (animation system RE'd + channel-aware engine)

- ✅ **Animation reverse-engineered** ([08-ghidra-animation.md](../08-ghidra-animation.md),
  [T-033](T-033-ride-animation-keyframes.md)): ride animation is **vertex keyframes split across
  sibling `.md2` files** (`<base><letter>[<n>].md2`, letter = first letter of the animation name; 12
  channels = `ScriptDefs.Animations`; Main = looping motion). `.sgn` files turned out to be **signs**
  (GDI billboard text), not animation — the original assumption was wrong.
- ✅ **Channel-aware engine.** `Ride` discovers the real channels from the WAD and `RideEngine` maps
  each `ScriptDefs.Animations` value → channel letter + frame count, animates only channels the ride
  ships, scales `WAITANIM` duration by frame count, and loops Main (else Idle) on the body. Verified
  in-game (e.g. `tourride` → `ANIM_Create(c×1)`, `totem` → Create/Main×10/Break×2/Repair). Fixed a
  latent hang: a missing entry in a *mounted* WAD returns a null stream (not an exception), which made
  the frame-probe loop forever.

## To do (roadmap)

1. ✅ **Ride keyframe animation** — rotation, translation/scale and vertex morph all decoded and driven
   from real ride data at the authentic 30 FPS rate, with multi-frame channel merge — [T-033](T-033-ride-animation-keyframes.md).
3. ☐ **Lights** — `ENABLELIGHT`/`DISABLELIGHT`/`SETLIGHT`/`COLOURLIGHT` (needs a multi-light render path).
4. ⚠️ **Walk/limbo** — needs a peep/visitor system, now started ([T-034](T-034-peeps.md): a wandering
   visitor crowd; path/queue following + ride interaction remain).
5. ⚠️ **Scream / coaster** — the **scream family** (`STARTSCREAM`/`STOPSCREAM`/`SINGLESCREAM`/
   `SCREAMLEVEL`) is done: routed through `IRideEngine`, `RideEngine` plays the scream sound (approx
   asset mapping, T-016) at the script's level (`Audio.PlaySfx` gained a per-effect volume), and a
   sustained scream re-triggers each ~1.8 s until `STOPSCREAM`. Operands RE'd from `monkey.rse`:
   `(soundCode, level 0..100)`, `-1` = default — `monkey` uses all four, `totem` `SINGLESCREAM`.
   Routing is unit-tested (`ScreamOpcodesRouteToEngine`); the audio path is the same one proven in-game
   by ride sounds. **In-game audibility is gated upstream**: the script branch that reaches scream
   depends on the **walk/limbo** opcodes (`WALKON` etc., item 4), still no-ops, so the VM doesn't yet
   advance there. The coaster **motion** opcodes (`COAST`×12 in coaster1, `BUMP` in bumper/gokarts,
   `TOUR`/`TURBO`) remain — they drive car objects and need the ride-engine/track tie.
6. ⚠️ **Real park terrain + placement grid** — `PlacementGrid` (tile↔world, footprint, occupancy;
   jungle's 95×84 dims from `Standard.sam`, unit-tested) + `ParkTerrain` rendering the real jungle
   landscape (`terrain.wad`/`base.MD2`, 272 meshes — textured ground, water, paths) with rides placed
   on its surface (height-sampled) via the grid. The terrain needed **double-sided** rendering
   (`MaterialFlags.DoubleSided`) — its heightfield winding isn't uniform after the Y/Z swizzle, so
   back-face culling dropped most of it. Ride **footprints** are now wired: `RideShape` parses a ride's
   `Info.Shape` ASCII grid from its `.sam` (e.g. monkey 4×4, totem 3×4, coaster 2×3; `*`=tile,
   `S`/`2`/`N`=entrance/exit markers), exposed as `Ride.Shape`; the dev park lays rides in a row each
   reserving its real footprint via `PlacementGrid.TryPlace` (no overlap). Unit-tested.
   **Entrance/exit cells** are wired: `RideShape` records the entrance (`S`/`N`/`E`) and exit (`2`)
   tiles; `Ride` reads the `UsageInfo` entry/exit sub-tile stand positions; the dev park drops green
   (entrance) / red (exit) markers at the computed world points on the terrain. Unit-tested.
   Remaining: the `base.lnd`/`*.map` (TP2M attribute) data and a proper terrain heightfield mesh (vs the
   scenery in `base.MD2`); robust grid↔terrain alignment (the bounds have outlier scenery meshes — uses
   the centroid for now); the `Info.Hoarding` perimeter fence + coaster track connectors (`<`/`>`); a
   build/placement UI; and lobby-vs-park scene separation. **Queue paths** are wired: a strip of path
   tiles is laid from each ride's entrance cell (stepping out from the footprint edge it sits on),
   reserved on the grid and rendered on the terrain with the queue path texture (`queue.wad`); the 3D
   queue-fence meshes (`questra`/`quebnd`) remain a follow-up.
7. ☐ Sound-code→asset mapping via `.MAP` ([T-016]); fold `EVENT`/`SETREVERB`/`DIPMUSIC` into sound.

## Affected files

`source/OpenTPW/World/Rides/{IRideEngine,RideEngine,RideObject}.cs` (new),
`source/OpenTPW/VM/Handlers/Objects.cs` (new), `source/OpenTPW/VM/RideVM.cs` (Engine),
`source/OpenTPW/VM/Handlers/Misc.cs` (removed dup ADDOBJ), `source/OpenTPW/World/Ride.cs`,
`source/OpenTPW/World/Level.cs` (dev ride), `source/OpenTPW/Render/Assets/Shader.cs`+`Material.cs`
(shared shaders / resilient watcher), `source/OpenTPW.Tests/RideEngineTests.cs`.
