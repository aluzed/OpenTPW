# T-029 — RE the native TPW main loop & render dispatch (reference)

- **Priority**: 🟢 Low (reference / validation track; not blocking the perf fixes)
- **Type**: Reverse engineering
- **Status**: ✅ Done — documented in [07-ghidra-render.md](../07-ghidra-render.md).
- **Related**: [T-026](T-026-render-resource-churn.md), [05-ghidra-reverse.md](../05-ghidra-reverse.md).

## Why

The OpenTPW renderer is an upstream re-implementation whose per-frame architecture (ephemeral GPU
resource sets, a full queue submit per uniform bind) matches neither modern best practice nor a
1999 Direct3D engine. The Ghidra work so far covered only file-format loaders and the ride-script
VM — the **original renderer and main loop were never reverse-engineered**. RE-ing them lets us
*validate* the engine architecture against the original and recover the original frame pacing
(the `WAIT` opcodes scale by a "framerate factor", hinting at a fixed logic tick — see
[T-007](T-007-vm-opcodes-rse.md)).

## Done

1. ✅ Extracted the no-CD `tp.exe` (PE32, x86, 3.6 MB) and ran a Ghidra 12.1 headless analysis.
2. ✅ Found the main message pump (`FUN_0045a960`, a `PeekMessage` pump-then-render game loop, with
   the decompiled loop quoted), the frame timer (`FUN_005f5f10`, QPC + `timeGetTime`), and DirectDraw
   init (`FUN_00563460`/`FUN_005fa2b0`). Graphics API identified from imports + the DX error tables:
   **DirectDraw + Direct3D Immediate Mode (DX6/7 execute buffers), HAL with an MMX software-rasteriser
   fallback**, presenting via DirectDraw page-flip.
3. ✅ Documented the comparison in [07-ghidra-render.md](../07-ghidra-render.md): the original creates
   textures/materials **once** and submits **per-frame execute buffers** (batched geometry, persistent
   resources) — confirming the [T-026](T-026-render-resource-churn.md)/[T-027](tickets/T-027-ui-draw-batching.md)
   direction and that the upstream per-draw resource churn matched neither the original nor modern practice.

## Acceptance

✅ A documented comparison of the original render loop vs OpenTPW's + the original's frame-pacing,
usable to sanity-check the renderer work.

## Method note

Findings come from the PE import table + embedded DirectX error-string tables (`objdump`, `strings`)
cross-checked with a Ghidra 12.1 headless auto-analysis and a Java post-script that resolved the
IAT-slot references to their calling functions. The import/string evidence alone is conclusive about
the API and loop shape; Ghidra supplied the concrete `FUN_` addresses and the decompiled pump body.

## Affected files

`docs/07-ghidra-render.md` (new), `docs/05-ghidra-reverse.md` (cross-link).
