# T-014 — Case-insensitive asset path resolution (Linux)

- **Priority**: 🟠 Medium (Linux runtime)
- **Type**: Portability
- **Status**: ☐ To do
- **Context**: documented in [../03-disc-compatibility.md](../03-disc-compatibility.md)
  and [../04-linux-compatibility.md](../04-linux-compatibility.md) but had no ticket.

## Problem

The game references assets in **lowercase** (`levels/jungle/terrain/textures/jgr_bas1.wct`,
`global.sam`), but the disc (ISO9660) stores names in **UPPERCASE 8.3**
(`ESPRITES.WAD`, `CHALLE~1.SAM`). On a case-sensitive Linux filesystem these don't match,
so `BaseFileSystem.OpenRead` / archive lookups fail even with a valid install.

The separator bug is already fixed ([T-001](T-001-backslash-paths-linux.md)); case is the
remaining mismatch.

## Options

1. **Resolution layer** in `BaseFileSystem`: when an exact path miss occurs, retry with a
   case-insensitive directory scan (cache the result). Robust, no install changes.
2. **Normalize on install/extract**: lowercase the asset tree once (documented workaround).
3. Both: normalization as the documented default, the resolution layer as a safety net.

Apply the same case-insensitivity to **archive-internal** lookups (`WadArchive`/`SdtArchive`),
not just the filesystem.

## Acceptance criteria

- With a real install on a case-sensitive FS, `FileSystemTests` (run via `OPENTPW_GAMEPATH`)
  pass without manually renaming assets.

## Affected files

`source/OpenTPW.Common/Files/BaseFileSystem.cs`,
`source/OpenTPW.Files/Formats/Archive/*`.
