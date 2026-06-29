# T-057 — Minimap (2dmap overlay)

- **Priority**: 🟢 Low (UX)
- **Type**: UI / rendering
- **Status**: ✅ Done (corner map + pins + camera box + layer toggles + click-to-pan; verified in-game)

## Context

TPW shows a corner minimap built from small per-category sprites. OpenTPW has no minimap — `PlacementGrid.cs`
notes it as "a separate, larger effort". The data is small and the sprite/UI infra already exists.

## Done

- **Projection** (`MinimapProjection`, pure + 6 unit tests): maps the placement grid's world bounds
  (`Grid.Origin` + `Width/Height·TileSize`) into the minimap's on-screen rect — clamped to the edge, optional
  Y-flip for the top-down image — with an inverse for click-to-pan.
- **`MinimapPanel : HudPanel`**: draws the level's `2dmap.tga` (512×512, via the VFS → `Texture`) as a backdrop,
  then one coloured pin per `Ride`/`Shop`/`Peep`/`Litter` at its projected `Position`, plus a yellow box at
  `BuildCameraMode.Focus`. A `RIDE/SHOP/PEEP/LITR` legend toggles each layer; **M** toggles the whole map; a
  click on the map pans the build camera (`Unproject` → `Focus`). Tucked just left of the BUILD column so the
  two never overlap; registered in `HudPanel.PointerOverUi`.
- Exposed `BuildMode.Grid` + `Level.Name` for the projection bounds + per-level asset path.
- Verified in-game: jungle backdrop with blue ride / orange shop / white peep / brown litter pins and the
  camera box; the autoplaced park renders correctly on the map.

## Notes

- Uses solid-colour pins keyed by category rather than the tiny `2dmap/{r,s,v,l}sprite.tga` icons — they read
  better at the small pin size; swapping in the real sprites is a trivial follow-up if wanted.

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
