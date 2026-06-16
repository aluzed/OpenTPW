# T-032 — Ride engine: make the VM's engine opcodes do something

- **Priority**: 🟡 Feature (the central unlock — backs Batch B of the VM and real gameplay)
- **Type**: Engine
- **Status**: ⚠️ In progress — slice 1 done (VM→engine seam + sound + a ride in the scene).
- **Related**: [T-007](T-007-vm-opcodes-rse.md) (the VM + opcode RE), [05](../05-ghidra-reverse.md)/[07](../07-ghidra-render.md).

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

## To do (roadmap)

1. ☐ **Object lifecycle + procedural animation** — `TRIGANIM`/`LOOPANIM`/`WAITANIM`/`SETOBJPARAM`
   (Time-driven transforms; reuse the VM WAIT scheduler), real `KILLOBJ` world despawn.
2. ☐ **Real MD2 keyframe animation** (decode keyframes; swap behind `TriggerAnim`).
3. ☐ **Lights** — `ENABLELIGHT`/`DISABLELIGHT`/`SETLIGHT`/`COLOURLIGHT` (needs a multi-light render path).
4. ☐ **Walk/limbo** — needs a peep/visitor system.
5. ☐ **Scream / coaster** — `STARTSCREAM`/`TOUR`/`COAST`/`TURBO`/`BUMP` (depends on peeps + track).
6. ☐ **Real park + placement grid** — unblocks correct ride siting (slice 1 placement is approximate).
7. ☐ Sound-code→asset mapping via `.MAP` ([T-016]); fold `EVENT`/`SETREVERB`/`DIPMUSIC` into sound.

## Affected files

`source/OpenTPW/World/Rides/{IRideEngine,RideEngine,RideObject}.cs` (new),
`source/OpenTPW/VM/Handlers/Objects.cs` (new), `source/OpenTPW/VM/RideVM.cs` (Engine),
`source/OpenTPW/VM/Handlers/Misc.cs` (removed dup ADDOBJ), `source/OpenTPW/World/Ride.cs`,
`source/OpenTPW/World/Level.cs` (dev ride), `source/OpenTPW/Render/Assets/Shader.cs`+`Material.cs`
(shared shaders / resilient watcher), `source/OpenTPW.Tests/RideEngineTests.cs`.
