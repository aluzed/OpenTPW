# T-036 — Peep pathfinding (walkable path graph + A*)

- **Priority**: 🟡 Feature
- **Type**: Engine
- **Status**: ☐ To do
- **Related**: [T-034](T-034-peeps.md) (queue spacing *along* a path — done).

## Context

Peeps currently walk **straight lines** between their entry point, ride queues, shops and the exit
(they can clip through rides, water and scenery). Queue discipline (spacing along a ride's queue path)
is already done; what remains is real cross-park routing.

## Remaining work

1. Build a **walkable path graph** for the park — either from the laid path tiles / `PlacementGrid`
   free cells, or the level's real path layout once that's loaded.
2. **A\*** (or flow-field) routing so a peep walks the path network from A to B instead of a straight
   line, routing around rides/water/occupied tiles.
3. Space the **queue along the real path** leading to the entrance (extends the current waypoint queue).
4. Park **gate / entry & exit** nodes so visitors arrive and leave through the gate rather than a ring.

## Acceptance criteria

- Peeps follow paths (not straight lines) and never walk through rides or water on the way to a target.

## Affected files

`source/OpenTPW/World/Peep.cs`, new `source/OpenTPW/World/PathGraph.cs`,
`source/OpenTPW/World/Terrain/PlacementGrid.cs`, `Level.cs`.
