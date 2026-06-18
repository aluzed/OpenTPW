# T-040 — Build/manage mode foundation (in-park camera, mouse picking, action dispatch)

- **Priority**: 🟡 Feature
- **Type**: Engine / UI
- **Status**: ✅ Core done — controllable in-park camera, cursor→tile picking + highlight, and a
  mode/click dispatch skeleton are in and verified in-game. Tool plug-ins (selection panels, multi-tile
  footprint hover) land with T-041+.
- **Parent**: [T-038](T-038-park-management-ui.md). **Blocks**: T-041, T-042, T-043, T-044, T-045.

## Context

Everything interactive depends on a foundation that doesn't exist yet: an in-park camera the player
controls, the ability to know **which grid tile is under the cursor**, and a mode/command dispatch to
route clicks to the current tool. Today the park uses a fixed orbit camera and no input.

## Reference (original)

A **mode state machine** + an `ACTION_*` command enum (see [T-038](T-038-park-management-ui.md) recon):
`CMMDefault`/`CMMPlaceStaff`/… modes each handle clicks; actions include `SET_MODE`(0),
`LMB_DOWN`(15)/`LMB_UP`(20) (+`_MODIFIED`), `SET_SELECTED_OBJ`(50), with RMB map scrolling
(`RMBScrollOn`, `fScrollRate`, `ADDSCROLL`). The grid is `mGridSizeX/mGridSizeY`.

## Work

1. **In-park camera**: pan/scroll (RMB-drag + edge/keys) and zoom over the terrain, replacing the fixed
   `ParkOverviewCameraMode` orbit for play.
2. **Mouse → tile picking**: ray/heightfield pick to the `PlacementGrid` cell under the cursor; expose
   `WorldToTile` hit + a highlight quad on that cell.
3. **Mode + action dispatch**: a small `BuildMode` state machine (Default / a placeholder tool) and an
   action router (LMB down/up, modified, select) — the seam T-041/043 plug tools into.
4. **Selection**: click a placed object (ride/shop/staff) → `SET_SELECTED_OBJ` → emit a selection event
   (panels in T-041/T-042/T-043 subscribe).

## Acceptance criteria

- The player can scroll/zoom the park, the tile under the cursor is highlighted, and LMB on a placed
  ride logs/raises a selection — with no tool yet, nothing is built.

## Affected files

new `source/OpenTPW/World/Build/BuildMode.cs` (+ camera), `source/OpenTPW/World/Terrain/PlacementGrid.cs`,
`source/OpenTPW/Engine/Input` / `Camera`, `source/OpenTPW/World/Level.cs`.
