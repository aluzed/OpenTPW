# T-008 — Unimplemented file formats

- **Priority**: 🟡 Feature
- **Type**: Feature / reverse engineering
- **Status**: ⚠️ **In progress.**
  - `.TQI`/`.TGQ` **video container** parsed (`OpenTPW.Files/Formats/Video/VideoFile.cs`):
    the EA FourCC block layout — chunk index, `pIQT` frame count, EA audio detection.
    Validated by `VideoFileTests` (synthesized container + a real movie via
    `TPW_VIDEO_SAMPLE`, which tiled all 523 chunks of `BF.TGQ` to a clean EOF).
    **Remaining**: decode the TQI frames + EA-ADPCM audio.
  - `.BF4` **fonts** parsed (`OpenTPW.Files/Formats/Font/BF4File.cs`): magic "F4FB",
    glyph count, offset table (tiles exactly to the first glyph), per-glyph **char code**,
    and **width/height + 1bpp bitmap decoded** (block offsets 16/18 = width/height; bitmap
    at 24, MSB-first, width bits/row). **Confirmed by rendering an atlas of a real
    GAME6.BF4 — every printable ASCII glyph is legible.** Validated by `BF4FileTests`
    (synthetic 'L' shape + real font via `TPW_FONT_SAMPLE`). **Remaining**: a few header
    fields (bearing/advance) + wiring into the renderer.
  - `.LIP`/`.LIPS` **lip-sync** parsed (`OpenTPW.Files/Formats/Sound/LipSyncFile.cs`):
    a flat list of little-endian uint32 mouth keyframe timestamps, terminated by
    `0xFFFFFFFF`, monotonically non-decreasing (verified on real EN/DANISH samples).
    Validated by `LipSyncFileTests` (synthetic + real via `TPW_LIP_SAMPLE`). **Remaining**:
    the timestamp unit and the mouth-shape semantics.
  - `.MTR` (mesh/material) still to do; `.MD2` and `.MAP` tracked in T-012.

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

## `.BF4` glyph-block notes

Each glyph block (≈24–32 bytes): `uint32 charCode`, then 12 bytes of fields
(`[1]=8` constant — likely cell height; `[2]` varies; `[3]=2` constant), then
`uint16 width` @16, `uint16 height` @18, 4 more bytes (bearing/advance — not pinned
down), then the **1bpp bitmap @24** (MSB-first, width bits/row, height rows). Width,
height and bitmap are confirmed (atlas render). The two fields at @20–23 and the @4–15
fields remain to be identified.

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
