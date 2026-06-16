# T-012 — Partially-implemented formats (.MD2, .MAP, .TPWS) (umbrella — closed)

- **Priority**: 🟡 Feature
- **Type**: Feature / reverse engineering
- **Status**: 🗂️ **Closed as an umbrella.** This ticket was a catch-all; the work it
  delivered is recorded below, and the **remaining** work is split into focused tickets:
  - `.MD2` static variant (frameCount 0) → **[T-015](T-015-md2-static-variant.md)**
  - `.MAP` per-entry records + SFX variant → **[T-016](T-016-map-entry-records.md)**
  - `.TPWS` save read + write → **[T-017](T-017-tpws-saves.md)**
- **What was delivered:** `.MD2` parser **verified against a real model**: a
  real `ENGLISH/MESHES/ENGLISH/PAUSED.MD2` parses and reconstructs as the 3D "PAUSED"
  sign (rendered top-view from the parsed vertices/faces). Added the regression test
  `ModelFileTests.ParsesRealModelSample` (gated on `TPW_MODEL_SAMPLE`; asserts meshes,
  triangle indices in range, finite positions). **Finding**: the parser uses hardcoded
  offsets and is **not robust to all `.MD2` variants** — the small `DATA/GENERIC/DYNAMIC/
  GARROW.MD2` (a static, frameCount-0 variant) used to throw `EndOfStreamException`.
  **Now hardened**: `ModelFile` validates the magic (`0x1CD15D46`) and bounds-checks every
  seek. The GARROW-style variant is now **identified precisely**: it is the *static* variant
  (`frameCount == 0`), which uses a different header layout — the frame-list/mesh-table
  pointers the animated path reads at 0x54/0x70 hold unrelated data there. `ModelFile` detects
  `frameCount == 0` and throws a clear `InvalidDataException` ("static variant … not yet
  supported") while PAUSED/BANKRUPT/CONGRATS (frameCount 1) still parse. Covered by
  `ModelFileTests.RejectsOutOfRangeOffsets` / `RejectsBadMagic`. **Remaining**: decode the
  static-variant header (offsets observed in GARROW: section pointers at the 0x40–0x78 block —
  0xC4/0x1C4/0x1E6/0x1F2/0x3B2/0x5F9/0x681 — vs the animated layout; needs cross-referencing
  in Ghidra).
  - **`.MAP` finding**: these are **not terrain maps**. Every `.MAP` on the disc is a
    `CAT_*` file under SOUND/MUSIC/SPEECH — an **audio category catalog** starting with a
    16-byte COM class GUID (DirectMusic family `{e9612c0?-31d0-11d2-b409-00?0c993f203}`).
    Two variants, by GUID: **BANK** (`…a0c993f203`) and **SFX** (`…b0c993f203`).
    `MapFile` decodes the category GUID and, for the **BANK** variant, the entry table:
    after the GUID + 8 reserved bytes + a `uint32` count come `count` fixed 11-byte records
    then `count` length-prefixed ASCII entry names (e.g. `Sound\Kids`, `Sound\UI`,
    `Sound\Ride`). Names are located by a self-validating scan (the table must consume the
    file exactly), so no record-size constant is hard-coded. **Verified** on every
    `Data/global/sound/cat_*BANK.map` (counts 1/3/4/5). The 11-byte records and the SFX
    variant body are kept raw. Covered by `MapFileTests` (synthetic BANK + SFX, and a real
    sample via `TPW_MAP_SAMPLE`). **Remaining**: decode the 11-byte per-entry records and the
    SFX variant.
  - `.TPWS` saves still to do (spec exists, but no save sample on the install disc).
- **Note**: distinct from [T-008](T-008-unimplemented-formats.md) (which tracks the
  ❌ *not-started* formats). This ticket tracks the ⚠️ *partial* ones, which had no ticket.

## Scope

| Format | Code | Current state | Remaining |
|--------|------|---------------|-----------|
| `.MD2` models | `OpenTPW.Files/Formats/Model/ModelFile.cs` | animated models parse (PAUSED.MD2 → readable 3D text); static variant (frameCount 0) detected & rejected cleanly | decode the static-variant header; render integration. |
| `.MAP` maps | `OpenTPW.Files/Public/MapFile.cs` | category GUID + BANK entry-name table decoded | decode the 11-byte per-entry records + the SFX variant. |
| `.TPWS` saves | `OpenTPW.Files/Formats/Save/SaveReader.cs` | partial read | complete read; implement write. |

## Approach

1. Cross-check each parser against a real asset (extract from the disc's `DATA/`, see
   [../03-disc-compatibility.md](../03-disc-compatibility.md)) and the format docs
   (`OpenTPW/OpenTPW.FileFormats` repo).
2. Add fixtures + tests (follow the `RideScriptTests` pattern; gate on game data with
   `OPENTPW_GAMEPATH` like [T-002](T-002-tests-absolute-paths.md) where a full asset is needed).

## Acceptance criteria

- Each format round-trips a real sample (read; write where applicable) under test.

## Affected files

`source/OpenTPW.Files/Formats/Model/ModelFile.cs`,
`source/OpenTPW.Files/Formats/Save/SaveReader.cs`, `source/OpenTPW/World/Terrain/*`.
