# T-018 — `.MTR` material semantics + `.MD2` texture binding

- **Priority**: 🟡 Feature
- **Type**: Reverse engineering
- **Status**: ☐ To do
- **Split from**: [T-008](T-008-unimplemented-formats.md).

## Context

`MTRFile` (`OpenTPW.Files/Formats/Model/MTRFile.cs`) decodes the magic (`0x2E5915AF`),
version, name (e.g. `s_bkrupt`), the mesh-coupled body as a `uint32[]` (`Indices`), and the
constant ~847-byte trailing block (`TrailingData`). `.MTR` is the material companion to the
same-named `.MD2` (e.g. `bankrupt.MTR` ↔ `bankrupt.MD2`).

## Findings so far

Cross-referenced with the companion `.MD2` (bankrupt/congrats):
- The `Indices` array starts with a per-vertex ramp up to ≈ the mesh's face count
  (bankrupt ramps to 410, MD2 `faceCount` 411), then a block of small values (0/1/2).
- So it's a per-vertex / per-face grouping table, but the exact per-element meaning and the
  texture references are undetermined.

## Remaining work

1. Identify the two sub-arrays' semantics (per-vertex group, per-face material/flags).
2. Decode the ~847-byte trailing block (likely texture names / material params).
3. Bind the material to the `.MD2` mesh + its textures so models render correctly.

## Acceptance criteria

- A real `.MTR` produces a structured material (texture refs + per-face/vertex grouping)
  that the `.MD2` mesh can use; validated against ≥ 2 samples in `MTRFileTests`.

## Reverse-engineering aid

**Ghidra** on the `.MTR`/material loader in `TP.EXE` — see [05-ghidra-reverse.md](../05-ghidra-reverse.md).

## Affected files

`source/OpenTPW.Files/Formats/Model/MTRFile.cs`, `source/OpenTPW.Tests/MTRFileTests.cs`.
