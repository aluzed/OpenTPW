# T-041 — Ride & shop placement (build tool)

- **Priority**: 🟡 Feature
- **Type**: Engine / UI
- **Status**: ⚠️ Core done — catalog + footprint preview (green/red) + place-on-click with cost
  charging + queue registration are in; `SetupDevPark` is now an empty park the player fills. Rotation
  and sell/demolish remain (deferred — see below).
- **Parent**: [T-038](T-038-park-management-ui.md). **Needs**: [T-040](T-040-build-mode-foundation.md).
  **Couples with**: [T-036](T-036-peep-pathfinding.md) (paths/queues).

## Done

- **Catalog** (`BuildCatalogItem`): the jungle rides (footprint from `RideShape`, **cost read from the
  `.sam`** `Upgrades[0].CostOfUpgrade`) + a food shop.
- **Clickable build/manage UI** (T-038): a shared `UI/Widgets/HudPanel` base (mouse→base-space mapping,
  hit-test, button drawing) backs two panels —
  - **`BuildPanel`** (right column): a button per catalog item that **selects on click**, so every item is
    mouse-reachable (the old number-key list capped at 1–9; the added drink stall had pushed `researcher`
    past key 9). Selected item highlighted green, unaffordable items dimmed red, re-click toggles off.
  - **`ManagePanel`** (bottom-left bar): clickable buttons for the previously keyboard-only manage actions
    — admission fee ±, take/repay loan, and (when a ride is selected via the Default tool) its ticket
    price ± and research/upgrade. Each calls the same `ParkFinances`/`Ride` methods as the keyboard
    shortcuts (those methods are covered by the T-042/T-044 tests); disabled actions are dimmed.
  `BuildMode` consults `HudPanel.PointerOverUi()` so a click on either panel doesn't also act on the tile
  behind it; the number-key/keyboard shortcuts still work. `ParkStatsPanel` no longer duplicates the
  catalog. Verified in-game: panels render (all 10 catalog items + the FEE−/FEE+/LOAN economy row), no
  exceptions. (Clicks couldn't be synthesised in this env — no `xdotool` — so the hit-test glue is by code
  review; the underlying actions are test-covered.)
- **Lobby vs in-park HUD split**: the lobby front-end (`LobbyLayout` — logo + Create-Player/Quit buttons)
  was being drawn over the loaded park. `Level.InPark` (set when the park loads, cleared on load failure)
  now selects the HUD: **in-park** → the build/manage/stats panels; **lobby** (or park-load failure) →
  the front-end. The cursor shows in both. Verified in-game: the park HUD renders alone, no front-end
  overlay; affordability dimming is now visible (e.g. `coaster1` $10000 dimmed at $9998 balance).
- **Footprint preview**: the selected item's `Width×Height` quad follows the cursor, **green** when
  `grid.CanPlace` & affordable, **red** otherwise.
- **Commit** (`CommitPlacement` → `SpawnRideAt`/`SpawnShopAt`): validates + `TryPlace`, spawns the
  `Ride`/`Shop`, **charges the cost** (`ParkFinances.PayBuild`, refused if unaffordable), and registers
  the ride's `RideQueue` into the shared list so peeps use it. Replaces the hardcoded dev layout.
- **Verified in-game** (deterministic `OPENTPW_AUTOPLACE` exercising the exact commit path): totem +
  monkey + shop placed, money debited by exactly their costs (3250+2000+500), 2 queues registered,
  peeps queue/ride/eat. Interactive select+preview+click dispatch verified via T-040.

## Remaining (follow-up)

- **Rotation** (`ACTION_SET_RIDE_ROTATION`): needs rotating the ride's mesh parts about the placement
  centre + rotating the footprint/entrance — deferred.
- **Sell / demolish**: needs ride teardown (despawn parts, remove queue, clear cells, refund) — a
  tile→object map + `Ride` teardown, deferred.

## Context

Rides and shops are placed by the hardcoded `SetupDevPark`. This ticket adds the player build tool:
pick a ride/shop from a catalog, preview its footprint, place it (charging the cost), rotate it, and
sell/demolish it.

## Reference (original)

`ACTION_SET_RIDE`(5) selects the ride to place, `ACTION_SET_RIDE_ROTATION`(10) rotates it,
`ACTION_LMB_DOWN/UP`(15/20) commit, `ACTION_SET_RIDE_QUEUE`(35) lays the queue. Placement validates the
grid cell type ("Cannot place … the cell is of type %d"). We already have `RideShape` (footprint +
entrance/exit), `PlacementGrid.TryPlace`, ride cost in the `.sam` (`Upgrades[0].CostOfUpgrade`), and
`ParkFinances`.

## Work

1. **Catalog menu**: list available rides (`levels/*/rides/*`) and shops; selecting one enters a
   "place" tool (T-040 mode) with that item.
2. **Footprint preview**: show the `RideShape` footprint on the grid under the cursor, green/red for
   valid/invalid (`grid.CanPlace`), with rotation (`SET_RIDE_ROTATION`).
3. **Commit**: on LMB, `grid.TryPlace` + spawn the `Ride`/`Shop`, **charge the cost** via
   `ParkFinances` (refuse if unaffordable), drop on terrain. Replace `SetupDevPark`'s fixed layout.
4. **Queue/path stub**: place the entrance queue path (`SET_RIDE_QUEUE`); full path graph is
   [T-036](T-036-peep-pathfinding.md).
5. **Sell/demolish**: select a placed object (T-040) → remove it, clear its grid cells, partial refund.

## Acceptance criteria

- The player picks a ride from the menu, sees a valid/invalid footprint preview, places it (money
  drops by its cost), rotates and sells it; peeps then queue for the newly placed ride.

## Affected files

`source/OpenTPW/World/Build/*`, `source/OpenTPW/World/Rides/Ride.cs`, `RideShape.cs`,
`source/OpenTPW/World/Shop.cs`, `World/Terrain/PlacementGrid.cs`, `ParkFinances.cs`, `Level.cs`, UI.
