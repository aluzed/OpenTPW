# T-029 — RE the native TPW main loop & render dispatch (reference)

- **Priority**: 🟢 Low (reference / validation track; not blocking the perf fixes)
- **Type**: Reverse engineering
- **Status**: ☐ To do
- **Related**: [T-026](T-026-render-resource-churn.md), [05-ghidra-reverse.md](../05-ghidra-reverse.md).

## Why

The OpenTPW renderer is an upstream re-implementation whose per-frame architecture (ephemeral GPU
resource sets, a full queue submit per uniform bind) matches neither modern best practice nor a
1999 Direct3D engine. The Ghidra work so far covered only file-format loaders and the ride-script
VM — the **original renderer and main loop were never reverse-engineered**. RE-ing them lets us
*validate* the engine architecture against the original and recover the original frame pacing
(the `WAIT` opcodes scale by a "framerate factor", hinting at a fixed logic tick — see
[T-007](T-007-vm-opcodes-rse.md)).

## To do

1. ☐ Extract the no-CD `tp.exe` from `crk-02988-Theme_Park_World_Nocd.7z` (`7z e ...`) and import it
   into Ghidra (no project exists yet). It is unencrypted (the disc binary is SafeDisc — see
   [05-ghidra-reverse.md](../05-ghidra-reverse.md)).
2. ☐ Find `WinMain` + the Windows message pump (`PeekMessage`/`GetMessage`/`DispatchMessage`) and
   the per-frame render dispatch. Identify the graphics API (Direct3D immediate-mode expected;
   confirm — `DirectDrawCreate`/`Direct3DCreate`/`IDirect3DDevice` imports).
3. ☐ Document: scene model (retained vs immediate), how vertex/texture/state are managed per frame
   (persistent buffers vs per-draw rebuild), and the frame-pacing/timing loop. Write up in a new
   `docs/07-ghidra-render.md` and cross-link from `05-ghidra-reverse.md`.

## Acceptance

A documented comparison of the original render loop vs OpenTPW's, plus the original's frame-pacing
mechanism, usable to sanity-check [T-026](T-026-render-resource-churn.md)/[T-030](T-030-async-level-load.md).

## Affected files

`docs/07-ghidra-render.md` (new), `docs/05-ghidra-reverse.md` (cross-link).
