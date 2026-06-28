# T-057 — Minimap (2dmap overlay)

- **Priority**: 🟢 Low (UX)
- **Type**: UI / rendering
- **Status**: ☐ To do (proposed — RE recon done; **assets present, full infra exists**)

## Context

TPW shows a corner minimap built from small per-category sprites. OpenTPW has no minimap — `PlacementGrid.cs`
notes it as "a separate, larger effort". The data is small and the sprite/UI infra already exists.

## What we know (RE recon)

- **Assets: `data/2dmap/`** — 6 TGA sprites + an index `qickload.txt`:
  `1 esprite.tga` (entities) · `2 gsprite.tga` (grid/terrain) · `3 lsprite.tga` (litter) · `4 rsprite.tga`
  (rides) · `5 ssprite.tga` (shops) · `6 vsprite.tga` (visitors). Each 1–16 KB.
- OpenTPW already renders TGA via the UI/texture system and draws sprites for peeps/staff/rides; the
  coordinate transforms (world↔tile↔screen) exist in `PlacementGrid`/`BuildMode`.

## Scope

1. Load the `2dmap/` sprites (TGA via `WadArchive`/the loose-file VFS).
2. A `MinimapPanel`: a corner overlay rendering terrain extent + a per-entity icon at its mapped position
   (rides/shops/peeps/litter), with the camera viewport box. Layer toggles per category.
3. Optional: click-to-pan the camera from the minimap.

## Acceptance criteria

- A corner minimap shows the park (terrain + rides + shops + peeps) and the camera's current view; toggling
  layers works.

## Affected files (anticipated)

`source/OpenTPW/UI/Widgets/MinimapPanel.cs` (new), a world→minimap projection helper (unit-testable),
hooks into the entity lists.
