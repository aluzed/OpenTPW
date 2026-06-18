# T-038 — Park management & build mode (umbrella)

- **Priority**: 🟡 Feature
- **Type**: Engine / UI / reverse engineering
- **Status**: 🗂️ Umbrella — split into focused, sequenced tickets (below).
- **Related**: [T-034](T-034-peeps.md) (economy + stats HUD), [T-032](T-032-ride-engine.md) (park/placement),
  [T-036](T-036-peep-pathfinding.md) (paths).

## Context

The park economy runs and is shown read-only (`ParkFinances` + `ParkStatsPanel`). Rides/shops are placed
by a hardcoded dev layout (`SetupDevPark`), prices/fees are derived defaults, and there is **no player
control** — the single biggest gap to a playable game. This is too large for one ticket, so it is an
umbrella over the children below.

## How the original does it (RE recon — `tp.exe` strings)

The build/manage UI is a **mode state machine** driven by an `ACTION_*` command enum and `CMM*` mode
classes:

- **Modes**: `CMMDefault`, `CMMPlaceStaff`, `CMMOnline` (+ ride/track place modes). Each handles
  `OnLeftClick` etc. (`CMMPlaceStaff::OnLeftClick`).
- **Action enum** (`ACTION_*`): `SET_MODE`(0)/`SET_MODE_NR`(25), `SET_RIDE`(5),
  `SET_RIDE_ROTATION`(10), `SET_RIDE_TRACK`(30), `SET_RIDE_QUEUE`(35), `LMB_DOWN`(15)/`LMB_UP`(20)
  (+`_MODIFIED` 40/45), `SET_SELECTED_OBJ`(50), `COASTER_*`(55–68, the track editor),
  `SET_RIDE_NAME_A/B`(70–85).
- **Placement** validates the grid cell type ("Cannot place … the cell is of type %d", `mGridSizeX/Y`);
  staff are picked up/dropped (`mStaffMemberPickedUp`, "Picking up staff member %d").
- **Economy**: `mCash`/`InitialCash`, `mAdmissionFee`/`InitialAdmissionFee` ("Admission fee set to %d"),
  and a full **loan** system (`LoanInfo`, `mLoans[].APR_in_percent`/`monthly_repayment`, "Bankrupted").
- **Staff**: types Entertainers / Mechanics / Guards / Researchers; `AllStaffConstants`
  (`BaseWage`, `BaseCostPerStaff`, `BeginningNumberOf*`); patrol areas; `CAdvisor::HandleStaffInfo`.
- **Research/upgrade**: `CostOfResearch`/`CostOfUpgrade`/`DurationOfUpgrade`, `MaxResearchers`,
  "Cannot upgrade this object", "Can only build objects at upgrade level 0".

## Children (sequence)

1. **[T-040](T-040-build-mode-foundation.md)** — in-park mode + camera scroll + **mouse→tile picking**
   + the `ACTION_*`/mode dispatch skeleton. *Foundation — blocks the rest.*
2. **[T-041](T-041-ride-shop-placement.md)** — place/rotate/sell rides & shops on the grid (replaces
   `SetupDevPark`); queue/path stub (couples with [T-036](T-036-peep-pathfinding.md)). *Needs T-040.*
3. **[T-042](T-042-economy-controls-loans.md)** — ticket price + admission fee + finances panel + loans.
   *Needs T-040.*
4. **[T-043](T-043-staff-management.md)** — hire/place/fire staff (incl. mechanics & researchers), wages.
   *Needs T-040.*
5. **[T-044](T-044-research-upgrades.md)** — research + per-ride capacity upgrades (`Upgrades[*]`).
   *Needs T-041 + T-042.*
6. **[T-045](T-045-coaster-track-editor.md)** — the coaster track editor (`ACTION_COASTER_*`). *Big; later.*

## Acceptance criteria (umbrella)

- A player can enter a park, place a ride, set its price, hire staff, take a loan and trigger an
  upgrade, with finances responding — i.e. the children together make the park playable.
