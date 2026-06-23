# T-050 — Peep simulation depth (ride ratings & thoughts, water-aware pathfinding, real gate)

- **Priority**: 🟡 Feature
- **Type**: Gameplay / AI
- **Status**: ✅ Done — **ride ratings & thoughts**, **water-aware pathfinding**, and the **real entrance
  gate** are all in and unit-tested.
- **Parents**: [T-039](T-039-peep-needs-staff-depth.md), [T-036](T-036-peep-pathfinding.md).
- **Related**: [T-034](T-034-peeps.md).

## Context

The peep crowd loop, needs, economy, staff interaction and A* pathfinding are all **core done**. The
remaining depth is the peeps' *opinions* and a couple of pathfinding refinements.

## Done (ride ratings & thoughts)

- `Peep.RateRide(excitement, ticketPrice, reliability)` (pure, unit-tested) → a **0..100 satisfaction** +
  a **`RideThought`** (`GreatRide`/`GoodValue`/`TooExpensive`/`Unreliable`/`Mediocre`/`Rubbish`). It blends
  the ride's excitement with **value-for-money** (price vs the "fair" price ≈ excitement/10) and
  **reliability**.
- On finishing a ride a peep: sets `Peep.LastThought`, feeds the satisfaction into the ride's running
  **`Ride.Rating`** (its reputation, seeded from excitement), and adjusts its own `happiness` by
  `(satisfaction − 50)` — so a great-value ride lifts the mood and an overpriced/unreliable one sours it
  (and thus moves the park rating, which is the crowd's average happiness).
- **Ride choice now weights by `Ride.Rating`** (not raw excitement): well-rated rides draw more peeps,
  poorly-rated ones fewer — a feedback loop on pricing/reliability.
- Covered by `PeepRatingTests` (7 cases incl. value-for-money monotonicity).

## Done (water-aware pathfinding)

- `PlacementGrid` gained a **water layer**: `MarkWater`/`IsWater`/`WaterTileCount` + the terrain-driven
  `MarkWaterFromTerrain(sampleHeight, waterLevel)` (flags tiles whose terrain height ≤ the water level).
  Water tiles are **impassable** (`IsWalkable` returns false even where a path is laid — no bridges yet)
  and **unbuildable** (`CanPlace` rejects them). The peep A* therefore routes around lakes/moats.
- Wired in the dev park: `Level` flags low terrain as water (level = `Min.Z + 8%` of the height range,
  since the dev park has no explicit water plane) and logs the count.
- Covered by `PlacementGridTests` (water blocks walk/placement; terrain marking) + `PathGraphTests`
  (`RoutesAroundWater`, `WaterIsImpassableEvenUnderAPath`).

## Done (real entrance gate)

- `Level.ReadEntranceTile(Standard.sam)` (pure, unit-tested) returns the park's entrance in heightfield
  tile coords — the centre of the fixed 2-tile gate span (`FixedItemInfo.EntranceA/B PosX/Y`; jungle =
  47-48,17 → tile (48.0, 17.5)), falling back to the heightfield centre if absent.
- The dev park now spawns the crowd fanned just outside that gate (via `grid.PointToWorld`) instead of a
  synthetic ring. Each peep's `home` = the gate, so they **leave through the gate** and re-enter there
  when recycled. Covered by `LevelGateTests`.

## Acceptance criteria

- Peeps rate rides and those ratings move the park rating; peeps route around water; entry/exit uses the
  real gate — each verified in-game (e.g. via the `OPENTPW_AUTOPLACE` / econ-debug drive).

## Affected files

`source/OpenTPW/World/Peep.cs`, `World/Ride.cs` (rating), `World/PathGraph.cs`, `World/ParkTerrain.cs`,
`World/Level.cs` (gate node).
