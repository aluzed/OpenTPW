# T-044 — Research & ride upgrades

- **Priority**: 🟡 Feature
- **Type**: Engine / UI
- **Status**: ⚠️ Core done — the full `Upgrades[*]` table is parsed; researchers advance research; an
  applied upgrade raises capacity (and the live queue). Verified in-game. Per-ride UI + research
  duration from the `.sam` remain.
- **Parent**: [T-038](T-038-park-management-ui.md).
  **Needs**: [T-041](T-041-ride-shop-placement.md) + [T-042](T-042-economy-controls-loans.md);
  **researchers** from [T-043](T-043-staff-management.md).

## Done

- **Full `Upgrades[*]` table parsed** into `Ride.Upgrades` (`InitCapacity` / `CostOfResearch` /
  `CostOfUpgrade` per level); level 0 = as built. `Ride.Capacity` is now the current level's capacity,
  and `RideQueue.Capacity` delegates to it so an upgrade is immediately live for peeps.
- **Research**: `Ride.StartResearch()` + `TickResearch(dt, researchers)` (driven each frame from
  `Staff.ResearcherCount`) accumulates researcher-seconds to unlock the next level; no researchers → no
  progress. `ApplyUpgrade()` raises the level once researched.
- **Controls**: select a ride, `R` pays `CostOfResearch` + starts research, `U` pays `CostOfUpgrade` +
  applies it. HUD shows level / capacity / research % / next cost.
- **Verified in-game** (`OPENTPW_AUTOPLACE`): totem L0 cap 6 → research complete → apply → L1 cap 8
  (3 levels parsed), matching the `.sam`.

## Remaining (follow-up)

- A proper per-ride research/upgrade **panel** (progress bar, level list); source the research
  **duration** from the `.sam` (`DurationOfUpgrade`) instead of the fixed constant; global research
  queue across rides.

## Context

Each ride `.sam` already parses an `Upgrades[*]` table (`InitCapacity`, `RedLineCapacity`,
`CostOfUpgrade`, `CostOfResearch`, `QueueWaitTimeConstant`) — currently unused. This ticket spends
money + researcher time to unlock and apply upgrades that raise a ride's capacity/throughput.

## Reference (original)

`CostOfResearch` / `CostOfUpgrade` / `DurationOfUpgrade`; research is staffed by **Researchers**
(`MaxResearchers`, `AvgGradeOfResearchers`, `mAllResearchCompleted`, `FeaturesResearchMore`). Build/
upgrade gating: "Can only build objects at upgrade level 0 at the moment", "Cannot upgrade this
object", "Found upgrade... modify?". So an object starts at upgrade level 0 and is upgraded in steps.

## Work

1. **Parse the full `Upgrades[*]` table** into `Ride` (currently only `MaxCapacity` is read): per-level
   `InitCapacity`/`RedLineCapacity`/costs/`QueueWaitTimeConstant`.
2. **Research progress**: spend `CostOfResearch` + researcher-time (`DurationOfUpgrade`, scaled by the
   number/grade of researchers) to unlock the next upgrade level.
3. **Apply upgrade**: pay `CostOfUpgrade` to raise the ride to the next level → bump its capacity
   (`Ride.Capacity` → next `InitCapacity`) and queue throughput.
4. **UI**: a research/upgrade panel per ride (current level, cost, progress).

## Acceptance criteria

- With a researcher hired, a ride's next upgrade unlocks after its research duration; paying the
  upgrade cost raises its capacity, and the bigger queue/throughput is observable in-game.

## Affected files

`source/OpenTPW/World/Rides/Ride.cs`, `RideQueue.cs`, `source/OpenTPW/World/ParkFinances.cs`,
`source/OpenTPW/World/Staff.cs` (researchers), UI, `Level.cs`.
