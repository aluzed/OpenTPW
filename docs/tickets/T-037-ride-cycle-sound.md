# T-037 — Ride boarding/unloading SFX + real ride duration

- **Priority**: 🟡 Feature
- **Type**: Engine / reverse engineering
- **Status**: ⚠️ Wrong-sound bugs fixed; correct per-cue SFX pending the EVENT/EventMap binding. The
  duration half is done (ride duration comes from the running-animation length). See "Done" / "Remaining".
- **Related**: [T-034](T-034-peeps.md), [T-016](T-016-map-entry-records.md), [T-031](T-031-game-audio.md).

## Context

The ride animation cycle is occupancy-driven (Load → Start → Main → End → Unload, see T-034) and the
ride **duration** already comes from the ride's own running-animation length. The remaining piece is
**audio per cycle**. The RSE VM already triggers `SPAWNSOUND`/`ADDOBJ` sounds, but the code→asset
mapping is the `.MAP` audio catalog, which isn't decoded — so sounds resolve through an *approximate*
index (e.g. the monkey ride plays `urinal.mp2`). Wiring a per-board cue now would just repeat that
wrong mapping.

## Done

- **[T-016](T-016-map-entry-records.md) landed** — the `.MAP` catalog (BANK bank list + SFX per-sound
  records) is decoded.
- **`SPAWNSOUND` fixed**: its operand is a **string** (RE'd: handler `FUN_005551ab` requires the string
  type tag), naming a sound or — in every jungle ride — the `EventMap.rse` sound-event-map child script.
  We were mis-reading the string offset as an int and playing `RideHD[offset]` (a spurious `arc001` at
  ride start). Now the handler resolves `vm.Strings[…]`; an `.rse` name is recognised as the sound-event
  map (its EVENT bindings await the effects engine) and no clip is played; a plain name plays by name.
- **ADDOBJ "explosion" spam fixed** (user-reported): sound objects were played via `RideHD[param%N]`,
  and `param` is **not** a direct index — `param = -1` (a sentinel) hit `RideHD[1] = Crunch.mp2`, spammed
  ~28×/run (the "explosions"). ADDOBJ sound objects are now registered without playing the approximate
  clip; the real code→asset binding is the ride's EventMap / `.MAP` catalog (deferred). Rider **screams
  remain correct** (KidsHD path, T-032). Verified: 0 `Crunch`/`Backfire`/`arc` clips, screams still fire.
- **Duration**: already taken from the ride's running-animation length (per Context).

## Remaining work

- **Correct per-cue SFX** (boarding / running / unloading): bind ADDOBJ sound objects + `EVENT` codes to
  assets via the ride's `EventMap.rse` + the `.MAP` catalog. This is the **EVENT effects engine**
  (T-032 roadmap) — `EVENT` (`FUN_005573d0`) dispatches positioned sounds/particles from per-type pools,
  and `EventMap.rse` populates the binding table.

## Acceptance criteria

- Boarding a ride plays its correct sound (not an arbitrary clip); no wrong-sound spam while idle.

## Affected files

`source/OpenTPW/World/Rides/RideEngine.cs`, `Ride.cs`, `RideQueue.cs`,
`source/OpenTPW.Files/Public/MapFile.cs`.
