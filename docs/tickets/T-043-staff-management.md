# T-043 — Staff hiring & management

- **Priority**: 🟡 Feature
- **Type**: Engine / UI
- **Status**: ☐ To do
- **Parent**: [T-038](T-038-park-management-ui.md). **Needs**: [T-040](T-040-build-mode-foundation.md).
  **Related**: [T-039](T-039-peep-needs-staff-depth.md).

## Context

Staff (`Staff` — entertainers, handymen→mechanics, guards) are spawned by the hardcoded dev layout.
This ticket lets the player hire, place, and fire them, paying a hiring cost + wages, with the four
real staff types.

## Reference (original)

Staff types are **Entertainers, Mechanics, Guards, Researchers** (`AvgGradeOf*`,
`BeginningNumberOf*`, `MaxResearchers`). Constants live in `AllStaffConstants` (`BaseWage`,
`BaseCostPerStaff`, `ChanceToGetGreat*`). Placement is a mode (`CMMPlaceStaff::OnLeftClick`) that
**picks up / drops** a staff member on a valid cell ("Cannot place staff member here - the cell is of
type %d", `mStaffMemberPickedUp`, "Picking up staff member %d") and patrol areas ("Could not find or
reach a destination in the patrol area"). `CAdvisor::HandleStaffInfo` drives the advisor/info panel.

## Work

1. **Hire**: a staff panel (per type) — hiring debits `BaseCostPerStaff`; spawn the `Staff` with its
   role sprite (already wired). Add **Mechanic** + **Researcher** roles (researcher feeds T-044).
2. **Place / pick up**: enter a place-staff tool (T-040), drop on a valid cell; allow picking an
   existing staff up to re-place them (`mStaffMemberPickedUp`).
3. **Wages**: per-staff `BaseWage` (already drained globally — make it per-head and shown).
4. **Fire**: select a staff member → dismiss (removes it, stops its wage).
5. **Patrol areas** (optional, links to [T-039](T-039-peep-needs-staff-depth.md)): assign a guard/
   handyman a zone.

## Acceptance criteria

- The player hires a guard (cash drops), places it, sees its wage in the finances, and can fire it.

## Affected files

`source/OpenTPW/World/Staff.cs`, `source/OpenTPW/World/Build/*`, `ParkFinances.cs`, `Level.cs`, UI.
