# T-015 — Decode the static `.MD2` variant (frameCount 0)

- **Priority**: 🟡 Feature
- **Type**: Reverse engineering
- **Status**: ☐ To do
- **Split from**: [T-012](T-012-partial-formats.md) (which is now closed as a record of the
  animated-`.MD2` work).

## Context

The animated `.MD2` variant (frameCount ≥ 1, e.g. `PAUSED.MD2`) parses and renders
(`OpenTPW.Files/Formats/Model/ModelFile.cs`). The **static variant** (`frameCount == 0`,
e.g. `Data/generic/dynamic/garrow.MD2`, `rarrow.MD2`) uses a different header layout — the
frame-list / mesh-table pointers the animated path reads at 0x54 / 0x70 hold unrelated data.
`ModelFile` currently **detects `frameCount == 0` and throws a clear `InvalidDataException`**
rather than seeking to a bogus offset.

## Findings so far

From `garrow.MD2` (1669 bytes) vs `paused.MD2`:
- Header magic `0x1CD15D46` and the name (`garrow.MD2`) are at the same place.
- `0x40` holds the file size (`0x685` = 1669); `0x44` = mesh count (1).
- The real section pointers appear in the `0x40–0x78` block at different offsets than the
  animated layout — observed values `0xC4 / 0x1C4 / 0x1E6 / 0x1F2 / 0x3B2 / 0x5F9 / 0x681`,
  all < file size.
- Floats (`1.0f` = `0x3F800000`) cluster around `0x272–0x615` (vertex / matrix data).

## Remaining work

1. Map the static-variant header (mesh-table offset, vertex/face/UV offsets and counts).
2. Reuse the existing mesh-decode path once the offsets are known.
3. Remove the `frameCount == 0` rejection and parse it; keep PAUSED/BANKRUPT/CONGRATS working.

## Acceptance criteria

- `garrow.MD2` / `rarrow.MD2` parse to meshes with in-range triangle indices and finite
  positions (extend `ModelFileTests` with a static-variant sample).

## Reverse-engineering aid

Best done with **Ghidra** on `TP.EXE`'s model loader — see [05-ghidra-reverse.md](../05-ghidra-reverse.md).

## Affected files

`source/OpenTPW.Files/Formats/Model/ModelFile.cs`, `source/OpenTPW.Tests/ModelFileTests.cs`.
