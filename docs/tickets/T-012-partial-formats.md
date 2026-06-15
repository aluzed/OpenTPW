# T-012 — Complete the partially-implemented formats (.MD2, .MAP, .TPWS)

- **Priority**: 🟡 Feature
- **Type**: Feature / reverse engineering
- **Status**: ⚠️ **In progress.** `.MD2` parser **verified against a real model**: a
  real `ENGLISH/MESHES/ENGLISH/PAUSED.MD2` parses and reconstructs as the 3D "PAUSED"
  sign (rendered top-view from the parsed vertices/faces). Added the regression test
  `ModelFileTests.ParsesRealModelSample` (gated on `TPW_MODEL_SAMPLE`; asserts meshes,
  triangle indices in range, finite positions). **Finding**: the parser uses hardcoded
  offsets and is **not robust to all `.MD2` variants** — the small `DATA/GENERIC/DYNAMIC/
  GARROW.MD2` (a static, frameCount-0 variant) used to throw `EndOfStreamException`.
  **Now hardened**: `ModelFile` validates the magic (`0x1CD15D46`) and bounds-checks every
  seek, so GARROW fails with a clear `InvalidDataException` (*"frame list offset 0x10001 is
  out of range … unsupported .MD2 layout"*) while PAUSED still parses. Covered by
  `ModelFileTests.RejectsOutOfRangeOffsets` / `RejectsBadMagic`. Fully decoding the
  GARROW-style variant remains. `.MAP` and `.TPWS` still to do.
- **Note**: distinct from [T-008](T-008-unimplemented-formats.md) (which tracks the
  ❌ *not-started* formats). This ticket tracks the ⚠️ *partial* ones, which had no ticket.

## Scope

| Format | Code | Current state | Remaining |
|--------|------|---------------|-----------|
| `.MD2` models | `OpenTPW.Files/Formats/Model/ModelFile.cs` | parses mesh models (verified: PAUSED.MD2 → readable 3D text) | make it robust to all variants (GARROW.MD2 crashes); render integration. |
| `.MAP` maps | `World/Terrain` | demo terrain hardcoded | generalize parsing; load real level geometry. |
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
