# T-036 — Peep pathfinding (walkable path graph + A*)

- **Priority**: 🟡 Feature
- **Type**: Engine
- **Status**: ⚠️ Core done — peeps route around ride/shop footprints via A*; water-avoidance + a real gate
  node wait on the real level terrain/path layout being loaded (terrain is still the hardcoded demo).
- **Related**: [T-034](T-034-peeps.md) (queue spacing *along* a path — done).

## Context

Peeps used to walk **straight lines** between their entry point, ride queues, shops and the exit (they
clipped through rides and scenery). Queue discipline (spacing along a ride's queue path) was already
done; what remained was real cross-park routing.

## Done

1. **Walkability on the `PlacementGrid`**: a ride/shop footprint blocks, while a *laid queue path* is
   reserved against placement but still walkable — a new `path[,]` mask + `MarkPath` / `IsWalkable`
   (`SpawnQueuePath` marks each laid tile). Free ground is walkable.
2. **`PathGraph` (A\*)**: 8-connected octile-heuristic search over the grid, with no diagonal
   corner-cutting through a blocked corner, bounded by a max-expansion cap (falls back to a straight line
   for an unreachable / very distant goal). The start and goal tiles are always traversable so a peep can
   step off a ride exit cell or onto a shop / entrance stand-point cell. Unit-tested
   (`PathGraphTests`): straight run, detour around a footprint, cut through a marked gap, no-route,
   blocked start/goal, corner-cut prevention.
3. **Peep integration**: `Peep.MoveTo` replaces the straight `WalkToward` for the queue approach, shop
   detours and the walk home — it plans a path and re-plans only when the goal moves to a new tile (so a
   peep shuffling up a queue doesn't re-run A* every frame). Queue spacing along the path is unchanged
   (the queue stand points are already a path). Verified in-game (`OPENTPW_AUTOPLACE`): peeps reach the
   rides and board with no exceptions, routing around the placed footprints.

## Remaining

- **Water-avoidance** needs a water mask, which only exists once the **real level heightfield** is
  loaded (the park currently renders the hardcoded demo terrain) — a separate terrain effort.
- **Gate / entry & exit nodes**: visitors still arrive/leave on a spawn ring rather than through a real
  park gate; wire the gate once the real level layout is loaded.

## Acceptance criteria

- Peeps follow paths (not straight lines) and never walk through rides on the way to a target. ✅
  (water-avoidance deferred with the real terrain — see Remaining.)

## Affected files

`source/OpenTPW/World/Peep.cs`, new `source/OpenTPW/World/PathGraph.cs`,
`source/OpenTPW/World/Terrain/PlacementGrid.cs`, `Level.cs`,
new `source/OpenTPW.Tests/PathGraphTests.cs`.
