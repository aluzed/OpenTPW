# T-018 — `.MTR` material semantics + `.MD2` texture binding

- **Priority**: 🟡 Feature
- **Type**: Reverse engineering
- **Status**: ✅ **Done (reframed).** Ghidra showed the premise was wrong: the runtime
  **never loads `.MTR`** — model textures are bound from the `.MD2` itself, which `ModelFile`
  already decodes. The `.MTR` is a modeling-tool artifact, so its internal index-array
  semantics are not needed for the engine. The actionable goal (texture binding) is met and
  tested.
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

## Ghidra finding (no-CD `tp.exe`, 2026-06-16)

The runtime has **no `.MTR` loader**: the magic `0x2E5915AF` and the string `.mtr` are absent
from the depacked executable (no immediate, no data byte, no string — triple-checked), while
`.md2` (×43), `.tga` and `.wct` are everywhere. The model textures are carried **inside the
`.MD2`**: each material names an embedded texture, e.g.

| model | embedded texture |
|-------|------------------|
| BANKRUPT.MD2 | `text_grad.tga` |
| PAUSED.MD2 | `paws_grad.tga` |
| CONGRATS.MD2 | `cong_grad.tga` |

`ModelFile` already decodes these as `Mesh.Materials[].Name`. So texture binding is intrinsic
to the `.MD2`; the `.MTR` (the `s_bkrupt` material with its index array) is a **modeling-tool /
source artifact** the game never reads.

## Done

- Confirmed via Ghidra that `.MTR` is not a runtime format.
- The `.MD2` texture binding (the real goal) is decoded by `ModelFile` and now asserted in
  `ModelFileTests.ParsesRealModelSample` (a real model binds a non-empty texture name).
- `MTRFile` remains as a faithful reader of the tool artifact (header + name + index array),
  documented as not runtime-used.

## Not pursued (tool-only)

The `.MTR` index-array per-element semantics and the trailing block are a modeling-tool
concern with no engine consumer, so they're intentionally left undecoded.

## Affected files

`source/OpenTPW.Files/Formats/Model/ModelFile.cs` (texture binding),
`source/OpenTPW.Files/Formats/Model/MTRFile.cs` (tool-artifact reader),
`source/OpenTPW.Tests/ModelFileTests.cs`.
