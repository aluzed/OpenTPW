# T-008 — Unimplemented file formats

- **Priority**: 🟡 Feature
- **Type**: Feature / reverse engineering
- **Status**: ⚠️ **In progress.**
  - `.TQI`/`.TGQ` **video container** parsed (`OpenTPW.Files/Formats/Video/VideoFile.cs`):
    the EA FourCC block layout — chunk index, `pIQT` frame count, EA audio detection.
    Validated by `VideoFileTests` (synthesized container + a real movie via
    `TPW_VIDEO_SAMPLE`, which tiled all 523 chunks of `BF.TGQ` to a clean EOF). The
    audio/video chunks are exposed separately (`AudioChunks`/`VideoChunks`).
    **EA-ADPCM audio is now decoded** (`VideoFile.DecodeAudio()`): parses the SCHl "PT"
    header (channels @0x82, sample count @0x85) and decodes the stereo SCDl ADPCM blocks
    (FFmpeg `ea_adpcm_table`, 28-sample blocks, per-channel predictor/shift). **Verified**:
    a real BF.TGQ decodes to exactly 194815 samples/channel (matching the header) and the
    waveform is coherent audio (a jingle), not noise. The **TQI video frame header** is
    parsed too (`GetVideoInfo()` → width/height; confirmed 320×352 on BF.TGQ, matching
    `ffprobe`). The video content was confirmed decodable via ffprobe/ffmpeg (BF.TGQ is the
    Bullfrog frog logo). **TQI pixel decoder — DONE** (`Video/TqiDecoder.cs`,
    `VideoFile.DecodeFrame()`): full from-scratch decode, **verified pixel-accurate** vs
    ffmpeg (frame 120 of BF.TGQ reconstructs the Bullfrog logo). See the notes below.
    Mono-audio support remains.
  - `.BF4` **fonts** parsed (`OpenTPW.Files/Formats/Font/BF4File.cs`): magic "F4FB",
    glyph count, offset table (tiles exactly to the first glyph), per-glyph **char code**,
    and **width/height + 1bpp bitmap decoded** (block offsets 16/18 = width/height; bitmap
    at 24, MSB-first, width bits/row). **Confirmed by rendering an atlas of a real
    GAME6.BF4 — every printable ASCII glyph is legible.** Validated by `BF4FileTests`
    (synthetic 'L' shape + real font via `TPW_FONT_SAMPLE`). **Metrics now complete**:
    x/y bearings (@20/21) and x advance (@22) decoded — confirmed by laying out glyphs by
    advance into correctly-spaced text ("GAME OVER 0123"). `.BF4` is fully readable; only
    engine/UI wiring remains.
  - `.LIP`/`.LIPS` **lip-sync** parsed (`OpenTPW.Files/Formats/Sound/LipSyncFile.cs`):
    a flat list of little-endian uint32 mouth keyframe timestamps, terminated by
    `0xFFFFFFFF`, monotonically non-decreasing (verified on real EN/DANISH samples).
    Validated by `LipSyncFileTests` (synthetic + real via `TPW_LIP_SAMPLE`). **Remaining**:
    the timestamp unit and the mouth-shape semantics.
  - `.MTR` **materials** parsed (`OpenTPW.Files/Formats/Model/MTRFile.cs`): magic
    0x2E5915AF, version, and the material name (the header's name offset points to the
    embedded string, e.g. "s_bkrupt" — confirmed). `.MTR` is the material companion to the
    same-named `.MD2`; the mesh-coupled material/index array is kept raw. Validated by
    `MTRFileTests` (synthetic + real BANKRUPT.MTR via `TPW_MTR_SAMPLE`). **Remaining**:
    decode the index array and bind it to the `.MD2` mesh + textures.
  - `.PLB` **particle libraries** parsed (`OpenTPW.Files/Formats/Particle/ParticleLibraryFile.cs`):
    16-byte header (`uint32 count`, `uint32 recordSize`, 8 reserved), then `count`
    fixed-size records (`recordSize` bytes = a raw parameter block + a 48-byte null-padded
    name). On `Tp2.plb`: 105 records of 320 bytes; the **names decode exactly to the disc's
    `par_lib.h`** `P_EFFECT_*` list in order (NULL, Sparks, Smoke, … Test2D — verified by
    `ParticleLibraryFileTests.ParsesRealPlbSample`). The **per-effect colour ramp is now
    decoded**: the last 64 bytes of each record are 16 D3DCOLOR (`0xAARRGGBB`) stops, exposed
    as `ParticleEffect.ColorRamp`. Verified semantically (Fire ramps dark-red→bright, Sparks
    is yellow-orange, Smoke is white with an alpha fade in/out). The rest of the parameter
    block is kept raw. **Remaining**: decode the other parameter fields (lifetime, spawn rate,
    velocity, sprite ref) and the trailing shared block.
  - `.MD2` and `.MAP` tracked in T-012.

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

**Update**: the header field at offset 20 is a **name offset** (confirmed: points exactly
to the embedded string, e.g. "s_bkrupt"). `MTRFile` decodes magic + version + name, and now
also exposes the mesh-coupled body as a `uint32[]` (`Indices`) plus the constant trailing
block (`TrailingData`, ~847 bytes on real samples). **Cross-referenced with the companion
`.MD2`** (bankrupt/congrats): the array starts with a per-vertex ramp up to roughly the mesh's
face count (bankrupt: ramps to 410, MD2 faceCount 411), then a block of small values
(0/1/2 grouping). The exact per-element semantics and the texture binding are still
undetermined (would benefit from Ghidra on the loader). Decoded faithfully for now.

## `.BF4` glyph-block notes

Each glyph block (≈24–32 bytes): `uint32 charCode`, then 12 bytes of fields
(`[1]=8` constant — likely cell height; `[2]` varies; `[3]=2` constant), then
`uint16 width` @16, `uint16 height` @18, 4 more bytes (bearing/advance — not pinned
down), then the **1bpp bitmap @24** (MSB-first, width bits/row, height rows). Width,
height and bitmap are confirmed (atlas render). The two fields at @20–23 and the @4–15
fields remain to be identified.

## TQI pixel decoder — reverse-engineering notes (sync solved)

A from-scratch decoder was prototyped (Python) and verified against an ffmpeg reference
frame. The full frame decodes **in perfect sync** (consumes the whole bitstream, zero
desyncs) and reconstructs the reference's structure and rough colors. It is an MPEG-1
intra-style codec with EA-specific framing. Findings:

- **Frame (pIQT) header (8 bytes)**: `uint16 width`, `uint16 height`, `byte quant` (@4),
  3 unused bytes; the bitstream starts at offset **8**.
- **Bitstream is byte-swapped in 32-bit words** before bit reading (the EA `bswap_buf`
  step). This was the key blocker — without it everything desyncs. Reverse each aligned
  4-byte group, then read MSB-first.
- **Macroblocks**: row-major, **6 blocks each (4:2:0)** = Y0,Y1,Y2,Y3,Cb,Cr. No per-MB
  header. DC predictors reset once at frame start.
- **Block decode = MPEG-1 intra**: DC size VLC (luma/chroma) + differential DC, then the
  MPEG-1 AC run/level VLC with `0xFFFF` escape and the `0x0001`/next-bit EOB rule. Tables
  + tree-traversal taken from the **MIT-licensed jsmpeg** (so portable into this MIT repo;
  the VLC values are ISO MPEG-1 facts anyway).
- **Implemented** in `Video/TqiDecoder.cs`: the standard MPEG-1 dequant with a qscale
  derived from the header quant byte produces a pixel-accurate frame (verified vs ffmpeg).
  For an exact-to-ffmpeg match one could instead port EA's AAN qtable —
  `qscale = (215 - 2*quant)*5` and
  `intra_matrix[i] = (ff_inv_aanscales[i] * mpeg1_intra[i] * qscale + 32) >> 14`
  (DC: `(ff_inv_aanscales[0] * mpeg1_intra[0]) >> 11`), feeding an AAN IDCT. Using the
  plain dequant makes AC too small relative to DC (blocky) and chroma slightly off. Port
  with `ff_inv_aanscales` + a matching AAN IDCT to get a pixel-accurate frame.
- **Verification harness**: `ffmpeg -i movie.tgq -frames:v 1 -pix_fmt rgb24 ref.rgb`
  (ffmpeg used only as an external reference, never linked/ported).

## Formats to handle

| Format | Status | Samples (on the CD) | Notes |
|--------|:------:|---------------------|-------|
| `.BF4` fonts | ❌ | `DATA/FONTS.WAD` | — |
| `.MTR` materials | ❌ | inside the `.WAD`s | Needed for correct model rendering. |
| `.LIPS` lip-sync | ❌ | `DATA/GLOBAL/SPEECH` | — |
| `.TQI`/`.TGQ` video | ❌ | `DATA/MOVIES/*.TGQ` | Bullfrog video codec. |
| `.PLB` particles | ✅ | `DATA/PARTICLE/Tp2.plb` | Header + records + names decoded; `par_lib.h` gives the effect **names** (the binary struct was reversed from the sample). Param fields remain raw. |
| `.MD2` models | ⚠️ | `.WAD`s | `ModelFile.cs` partial — to finish. |
| `.MAP` maps | ⚠️ | `DATA/LEVELS/...` | parsing to generalize (terrain currently hardcoded). |

## Recommended approach

1. For each format: analyze a real sample (hex editor) + reverse the read routine in
   `TP.EXE` via **Ghidra** ([../05-ghidra-reverse.md](../05-ghidra-reverse.md)).
2. Document the structure under `docs/`.
3. Implement the parser in `source/OpenTPW.Files/Formats/` (follow the existing
   pattern: `BaseStream`, `*Reader`).
4. Add a test with a fixture.

## Quick win — ✅ done

`DATA/PARTICLE/par_lib.h` is an **original C header**. It turned out to list the effect
**catalogue** (`P_EFFECT_*` ids 0–100 + `E_EFFECT_*` emitters), not the binary struct, so
the `.PLB` layout itself was reversed from `Tp2.plb` and cross-checked against those names
(they line up exactly). Parser + tests landed (`ParticleLibraryFile`).

## Affected files

`source/OpenTPW.Files/Formats/` (new parsers), `source/OpenTPW.Files/Formats/Model/ModelFile.cs`
