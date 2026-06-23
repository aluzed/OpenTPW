# T-049 — Management UI depth (finance graph, staff fire/patrol, per-ride upgrade panel)

- **Priority**: 🟡 Feature
- **Type**: UI / gameplay
- **Status**: ☐ To do
- **Parents**: [T-042](T-042-economy-controls-loans.md), [T-043](T-043-staff-management.md),
  [T-044](T-044-research-upgrades.md). **Builds on**: [T-041](T-041-ride-shop-placement.md) (HudPanel/UI).

## Context

The economy, staff and research systems are all **core done** and driven from real data, but their
player-facing UI is still keyboard/HUD-text only. This ticket finishes the clickable panels.

## Scope

1. **Finance** (T-042 tail): a clickable economy panel with an income/expense **graph** over time, and
   per-ride price controls (beyond the global admission/price keys).
2. **Staff** (T-043 tail): **fire** a staff member from the UI, and assign **patrol zones** (handymen/
   guards already patrol toward litter/unhappy peeps — let the player bound the area).
3. **Research** (T-044 tail): a **per-ride upgrade/research panel** — pick what to research, see
   `Upgrades[*]` progress and apply capacity/other bumps per ride (the data + effects already exist).

## Acceptance criteria

- The player can, by mouse: read a finance graph + set a ride's price; fire a staffer + set a patrol
  zone; and drive research / apply an upgrade from a panel — each verified in-game.

## Affected files

`source/OpenTPW/UI/Widgets/ManagePanel.cs`, `ParkStatsPanel.cs`, new per-ride panel; `World/Staff.cs`,
`World/ParkFinances.cs`, the research/upgrade model.
