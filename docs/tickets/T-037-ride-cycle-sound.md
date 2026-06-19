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

## EVENT / EventMap sound architecture (RE'd — Ghidra)

The full chain that plays a ride's *intended* sound, decoded end to end:

1. **`SPAWNSOUND("EventMap.rse")`** (handler `FUN_005551ab`): builds the ride path + the operand string
   and calls `FUN_005587f0` to load the ride's **`EventMap.rse`** as its sound-event-map resource.
2. **`EventMap.rse`** is a tiny per-ride binding table — variables `VAR_EVT0..N` (a **sound id** per
   event slot) and `VAR_PAR0..N` (a param per slot), set by `COPY` literals. E.g. `coaster1`:
   `EVT=[200,216,69,203,203]`, `PAR=[0,0,19,0,22]`; `gokarts`: `EVT=[194,195,203,199,203,…]` (id `203`
   recurs as the "no sound" default).
3. **`EVENT(type, target, code)`** (handler `FUN_00552615` → dispatch `FUN_005573d0`): `switch(type)`
   selects a per-type effect pool (`DAT_00803a20..3c`); `FUN_00556b90` computes the **3D position** from
   the `target` node in the ride's model/node graph; the chosen sound id is spawned through the runtime
   **sound manager** (`FUN_0051bfc0` → virtual call on `DAT_00802bcc`).
4. **sound id → file**: the id indexes a **global sound registry** held by that manager, built at load
   from the banks named in the BANK catalog (`cat_ridesBANK`: `Ride/sfUi/Staff/Kids/xKids`) + the SFX
   catalogs. Verified the ids are global, not per-catalog: `coaster1`'s `200/69/203` are in
   `cat_ridesSFX` but `216` is not; `gokarts`'s `199/203` are, `194/195` are not (they live in other
   banks). The SFX `SoundEntries` (T-016) carry `{soundId, variations, params}` but **no filename**, so
   id→file needs the manager's registry reconstructed.

## Remaining work

- **Reconstruct the global sound registry** (banks + SFX catalogs → `id → (bank, index, file)`), then
  wire `EventMap` (`VAR_EVT`) + `EVENT`/`ADDOBJ` through it to play the **correct** asset. This is the
  meat of the **EVENT effects engine** (T-032 roadmap). Deliberately **not guessed** here — an
  approximate id→file mapping reintroduces the wrong-sound bug this ticket just removed. Positioned 3D
  audio (`FUN_00556b90`) and the particle pools are further stages.

## Acceptance criteria

- Boarding a ride plays its correct sound (not an arbitrary clip); no wrong-sound spam while idle.

## Affected files

`source/OpenTPW/World/Rides/RideEngine.cs`, `Ride.cs`, `RideQueue.cs`,
`source/OpenTPW.Files/Public/MapFile.cs`.
