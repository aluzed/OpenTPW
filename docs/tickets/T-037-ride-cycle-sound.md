# T-037 — Ride boarding/unloading SFX + real ride duration

- **Priority**: 🟡 Feature
- **Type**: Engine / reverse engineering
- **Status**: ☐ To do (sound half **blocked on [T-016](T-016-map-entry-records.md)**).
- **Related**: [T-034](T-034-peeps.md), [T-016](T-016-map-entry-records.md), [T-031](T-031-game-audio.md).

## Context

The ride animation cycle is occupancy-driven (Load → Start → Main → End → Unload, see T-034) and the
ride **duration** already comes from the ride's own running-animation length. The remaining piece is
**audio per cycle**. The RSE VM already triggers `SPAWNSOUND`/`ADDOBJ` sounds, but the code→asset
mapping is the `.MAP` audio catalog, which isn't decoded — so sounds resolve through an *approximate*
index (e.g. the monkey ride plays `urinal.mp2`). Wiring a per-board cue now would just repeat that
wrong mapping.

## Remaining work

1. Land **[T-016](T-016-map-entry-records.md)** (decode the `.MAP` sound catalog → correct code→asset).
2. Play the correct **boarding / running / unloading / scream** SFX per ride cycle
   (hook `RideEngine.SetActive` / the cycle stages).
3. If a ride script/`UsageInfo` field encodes an explicit duration, prefer it over the
   animation-length heuristic.

## Acceptance criteria

- Boarding a ride plays its correct sound (not an arbitrary clip); no wrong-sound spam while idle.

## Affected files

`source/OpenTPW/World/Rides/RideEngine.cs`, `Ride.cs`, `RideQueue.cs`,
`source/OpenTPW.Files/Public/MapFile.cs`.
