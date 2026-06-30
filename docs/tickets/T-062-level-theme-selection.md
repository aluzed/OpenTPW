# T-062 — Level theme selection (jungle / hallow / fantasy / space)

- **Priority**: 🟡 Feature (content)
- **Type**: Engine
- **Status**: ⚠️ Core done — the park theme is now selectable. Theme Park World ships four themed worlds under
  `levels/<name>/` (jungle / hallow / fantasy / space), each with its own terrain, rides, shops, sideshows and
  `Standard.sam`; only jungle was ever loaded (everything hardcoded `levels/jungle/...`). Now `OPENTPW_LEVEL`
  picks the theme (default + fallback `jungle`), routed through the existing `Level.Name`. The **build catalog is
  data-driven**: `LevelTheme.RideNames`/`SideshowNames` enumerate the theme's `rides/`+`sideshow/` WADs (via the
  VFS `GetDirectories`, which surfaces mountable `.wad` archives), so any theme populates without jungle-specific
  names. **Jungle is unchanged** — it keeps its curated, verified ride/sideshow set, so the default park is
  byte-for-byte the same. Non-jungle themes use a generic autoplace (`AutoplaceGeneric`) that drops the first few
  enumerated rides + the shared shops/staff. Terrain, `Standard.sam` and the advisor speech dir are theme-relative.
  **Verified in-game**: jungle loads identically (`autoplace totem=True … guard=True`, autotrack 9 segs, 0 exc);
  **space loads its own terrain (200-mesh) + its own rides** (bouncy/creature/hoverbot/mbuggy/moonshot/…, 5/6
  autoplaced, peeps enter, 0 exceptions). Unit-tested (`LevelThemeTests`: resolve/fallback + jungle stays curated).

## Context

The four themes are full, self-contained parks in the install, but OpenTPW hardcoded jungle everywhere — the
terrain path, the `Standard.sam` path, and especially the build catalog's seven named jungle rides. Switching
themes needed (a) routing the paths through the level name and (b) making the catalog discover a theme's rides
from the data rather than a fixed name list.

## Scope (done)

1. `LevelTheme`: `Resolve` an `OPENTPW_LEVEL` value to a known theme (fallback jungle); `RideNames`/`SideshowNames`
   (curated for jungle, enumerated for others via `FileSystem.GetDirectories`).
2. `Game.cs` picks the theme; `Level` paths (terrain / `Standard.sam`) + `Advisor` speech go theme-relative.
3. `BuildCatalog` builds from `LevelTheme` lists, loading each WAD defensively (a bad archive skips that item).
4. `AutoplaceGeneric` for non-jungle themes; jungle keeps its exact named autoplace.
5. Unit tests + in-game verification of jungle (no regression) and space (loads).

## Remaining (polish)

- A **front-end theme picker** (currently env-var only) — a lobby menu to choose the world.
- Per-theme **queue/path strip textures** (the jungle `jpa_*` filenames don't exist in other themes → the path
  strip falls back to `Texture.Missing`; cosmetic) and the `Terrain.cs` fallback texture.
- Wider verification of **hallow / fantasy** (only jungle + space were launched); some themes' rides may need
  per-WAD fixes if any fail to load (they skip gracefully today).

## Affected files

`source/OpenTPW/World/LevelTheme.cs` (new), `Level.cs` (paths + catalog + `AutoplaceGeneric`), `Client/Game.cs`,
`World/Advisor.cs`, `source/OpenTPW.Tests/LevelThemeTests.cs` (new).
