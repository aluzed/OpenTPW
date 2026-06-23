# T-050 — Peep simulation depth (ride ratings & thoughts, water-aware pathfinding, real gate)

- **Priority**: 🟡 Feature
- **Type**: Gameplay / AI
- **Status**: ☐ To do
- **Parents**: [T-039](T-039-peep-needs-staff-depth.md), [T-036](T-036-peep-pathfinding.md).
- **Related**: [T-034](T-034-peeps.md).

## Context

The peep crowd loop, needs, economy, staff interaction and A* pathfinding are all **core done**. The
remaining depth is the peeps' *opinions* and a couple of pathfinding refinements.

## Scope

1. **Ride ratings & thoughts** (T-039 tail): peeps form an opinion of a ride (excitement/intensity/
   nausea-style rating + value-for-money), which feeds the **park rating** and surfaces as peep
   "thoughts" (and gates re-rides / queue choice).
2. **Water-aware pathfinding** (T-036 tail): the A* over `PlacementGrid` should treat water tiles as
   impassable on the real level terrain (currently footprints block, water doesn't).
3. **Real gate node** (T-036 tail): peeps enter/leave at the level's real park-gate node rather than a
   synthetic spawn edge.

## Acceptance criteria

- Peeps rate rides and those ratings move the park rating; peeps route around water; entry/exit uses the
  real gate — each verified in-game (e.g. via the `OPENTPW_AUTOPLACE` / econ-debug drive).

## Affected files

`source/OpenTPW/World/Peep.cs`, `World/Ride.cs` (rating), `World/PathGraph.cs`, `World/ParkTerrain.cs`,
`World/Level.cs` (gate node).
