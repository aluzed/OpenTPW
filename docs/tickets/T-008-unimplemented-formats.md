# T-008 — Unimplemented file formats

- **Priority**: 🟡 Feature
- **Type**: Feature / reverse engineering
- **Status**: ⚠️ **In progress.** `.TQI`/`.TGQ` **video container** is now parsed
  (`OpenTPW.Files/Formats/Video/VideoFile.cs`): the EA FourCC block layout — chunk index,
  `pIQT` frame count, EA audio detection. Validated by `VideoFileTests` (a synthesized
  container + a real movie via the `TPW_VIDEO_SAMPLE` env var, which tiled all 523 chunks
  of `BF.TGQ` to a clean EOF). **Remaining** for video: decode the TQI frames + EA-ADPCM
  audio. `.BF4`, `.MTR`, `.LIPS` still to do.

## Tooling: WAD extractor

Added `source/OpenTPW.WadTool` (a small console tool over the engine's `WadArchive`
reader) to list/extract `.wad` (DWFB) archives and unblock format sampling:

```
dotnet run --project source/OpenTPW.WadTool -- <archive.wad>        # list
dotnet run --project source/OpenTPW.WadTool -- <archive.wad> -x out # extract
```

Validated against a real `ESPRITES.WAD` (121 files; contents are `.TPC`/`.ESP`/`.FPC`
sprite data). `WadArchive` itself now has a unit test (`WadArchiveTests`).

## Disc inventory (where the formats actually live)

A full ISO walk of the install disc shows most formats are **directly on the disc**, not
inside WADs: **185 `.BF4`**, **17 `.MD2`**, **11 `.MTR`**, **20 `.LIP`**, 111 `.MAP`,
41 `.WCT`, plus 316 `.WAD`. So `.BF4`/`.MTR`/`.MD2`/`.MAP` can be sampled without WAD
extraction.

## `.MTR` research (no published spec — RE notes from real samples)

`.MTR` are localized UI meshes/materials at `<LANG>/MESHES/<LANG>/*.MTR`
(BANKRUPT/PAUSED/CONGRATS, ~5–17 KB). From two samples:

- **Magic**: `AF 15 59 2E` (little-endian `0x2E5915AF`), constant across files.
- Header continues `06 00 00 00`, `01`, `01`, `00`, then a size-like field (≈ data size).
- Followed by an increasing small-int array (looks like an index/face buffer) and an
  embedded ASCII name (e.g. `s_bkrupt`, `s_congrats`).

The full vertex/face/material layout is **not** safely derivable from a couple of samples;
implementing it should start from these notes + cross-referencing the `.MD2` mesh format.
**Not implemented** to avoid shipping a speculative/incorrect parser.

## Formats to handle

| Format | Status | Samples (on the CD) | Notes |
|--------|:------:|---------------------|-------|
| `.BF4` fonts | ❌ | `DATA/FONTS.WAD` | — |
| `.MTR` materials | ❌ | inside the `.WAD`s | Needed for correct model rendering. |
| `.LIPS` lip-sync | ❌ | `DATA/GLOBAL/SPEECH` | — |
| `.TQI`/`.TGQ` video | ❌ | `DATA/MOVIES/*.TGQ` | Bullfrog video codec. |
| `.PLB` particles | (not listed) | `DATA/PARTICLE/TP2.PLB` | **`PAR_LIB.H` on the CD documents the format!** |
| `.MD2` models | ⚠️ | `.WAD`s | `ModelFile.cs` partial — to finish. |
| `.MAP` maps | ⚠️ | `DATA/LEVELS/...` | parsing to generalize (terrain currently hardcoded). |

## Recommended approach

1. For each format: analyze a real sample (hex editor) + reverse the read routine in
   `TP.EXE` via **Ghidra** ([../05-ghidra-reverse.md](../05-ghidra-reverse.md)).
2. Document the structure under `docs/`.
3. Implement the parser in `source/OpenTPW.Files/Formats/` (follow the existing
   pattern: `BaseStream`, `*Reader`).
4. Add a test with a fixture.

## Quick win

`DATA/PARTICLE/PAR_LIB.H` is an **original C header**: it likely describes the `.PLB`
particle format without any reversing — tackle it first.

## Affected files

`source/OpenTPW.Files/Formats/` (new parsers), `source/OpenTPW.Files/Formats/Model/ModelFile.cs`
