# T-038 — Park management UI + economy controls

- **Priority**: 🟡 Feature
- **Type**: Engine / UI
- **Status**: ☐ To do
- **Related**: [T-034](T-034-peeps.md) (economy + stats HUD), [T-032](T-032-ride-engine.md) (park/placement).

## Context

The park economy runs and is shown read-only (`ParkFinances` + `ParkStatsPanel`: money, tickets, gate,
food, upkeep, wages). Rides/shops are placed by a hardcoded dev layout, prices/fees are **derived
defaults**, and there is no player control. The `Upgrades[*]` / `CostOf*` research fields are already
parsed from the ride `.sam` but unused.

## Remaining work

1. **Build/placement UI** — place rides and shops on the `PlacementGrid` (preview + validity + commit),
   replacing `SetupDevPark`'s fixed layout.
2. **Price controls** — set each ride's **ticket price** and the park **entry fee** (these are
   runtime/player values in the original, not in the `.sam`).
3. **Staff management** — hire/fire entertainers, handymen, guards; show/total their wages.
4. **Research / upgrades** — spend `CostOfResearch`/`CostOfUpgrade` to raise ride capacity
   (`Upgrades[*].InitCapacity`/`RedLineCapacity`), already parsed.
5. **Finances panel** — a proper money panel (history/graph, optional loan) beyond the dev readout.
6. **Lobby vs park scene separation** — the dev park currently piggybacks on the lobby HUD.

## Acceptance criteria

- The player can place a ride, set its price, hire staff and trigger an upgrade, with finances responding.

## Affected files

`source/OpenTPW/UI/*`, `source/OpenTPW/World/Level.cs`, `ParkFinances.cs`,
`source/OpenTPW/World/Rides/Ride.cs`, `World/Terrain/PlacementGrid.cs`.
