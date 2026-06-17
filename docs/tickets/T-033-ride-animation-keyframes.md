# T-033 — Ride animation: real MD2 vertex keyframes

- **Priority**: 🟡 Feature (ride engine stage 3 — replaces procedural placeholder with original motion)
- **Type**: Engine / format RE
- **Status**: ⚠️ In progress — animation system reverse-engineered & documented; channel discovery
  wired into `RideEngine`; the per-frame vertex-payload decode remains.
- **Related**: [T-032](T-032-ride-engine.md) (ride engine), [T-015](T-015-md2-static-variant.md) (MD2
  versions), [08-ghidra-animation.md](../08-ghidra-animation.md) (the RE writeup).

## Problem

A ride's `TRIGANIM`/`LOOPANIM`/`WAITANIM` opcodes select a `ScriptDefs.Animations` channel, but the
engine had nothing to play — it bobbed the model procedurally as a placeholder. The original game's
motion lives in **sibling `.md2` keyframe files** (see below), which the engine never loaded.

## Reverse-engineering done (see docs/08)

- `.sgn` files are **signs** (GDI billboard text on `sign1.tga`/`sign2.tga` surfaces, `FUN_00467a80`),
  **not** animation — the original ticket assumption was wrong and is retired.
- Animation is **vertex-keyframe**, split across files (`FUN_00461f10`): a base `<name>.md2` plus
  per-channel `<name><c>.md2` / `<name><c><n>.md2` keyframe files, `<c>` = first letter of the
  animation name. 12 channels = `ScriptDefs.Animations` slots 0–11; **Main (`m`) is the looping
  motion**. Verified against `monkey`/`totem`/`wateride` WAD contents.
- Frame files are thin and **don't parse as standalone models** — they carry per-frame vertex data
  keyed to the base topology, folded in by `FUN_00470b30`.

## Done (this change)

- `RideEngine` discovers a ride's real channels at load time (probe the WAD for `<base><c>[<n>].md2`),
  maps each `ScriptDefs.Animations` value → channel letter + frame count, and drives
  `TriggerAnim`/`LoopAnim` against that real data (animate only channels the ride ships; log true
  channel + frame count). Placeholder motion retained until the payload decode lands.

## Frame-file binary layout — decoded (see docs/08)

The frame format is now reversed (loader `FUN_0046d6d0`, cross-checked against `monkeym1`/`monkeyc`):

- Header `0x00`–`0x4f` is byte-identical to the base (same magic/version/counts) → inherits base
  topology. The base's offset-table fields `0x50`–`0x7c` are **zero** in a frame (no own tables).
- Dword at **`0x98`** = frame animation pointer (0 in the base). It points to a small (~72 B)
  **per-surface relink trailer** near EOF: a count (`+0x12`) + an offset array (`+0x2c`) into the
  vertex region. The bulk **`0xb8` .. `[0x98]`** is the frame's **vertex-position payload** (verified
  real model-space floats). A frame only carries the surfaces that move, so sizes vary.

## Validation finding (loader validated first, before engine code)

Decoding `monkeym1`'s payload **refutes the vertex-morph hypothesis**: a record's offsets point to a
**sparse indexed list** — `0xFFFF0000 | index` markers (indices step `0,10,20,30…`) each followed by
four **unit-magnitude, quaternion-like** floats (`(1,0,0,0)`, `(0.7071,0,0,0.7071)`, `(0,0,0,1)`), not
model-space positions. Both `monkeym1` records target base surface 5 (`m_arm`) — consistent with
**per-surface rotational animation**, not a replacement vertex set.

→ Ride animation should be modelled as **per-surface transform/rotation animation** (maps onto the
per-mesh `TransformMatrix` we already apply), *not* a vertex-morph path with dynamic buffers. This
de-risk pass means we will not build the wrong architecture.

## Remaining

1. Decompile the **runtime pose-apply function** (the consumer of the model's `0x98` animation
   pointer during rendering) to pin down the vec4 semantics: quaternion vs. axis form, per-vertex vs.
   per-bone, and how it composes with the base pose.
2. Implement the keyframe loader on that basis (flat-load + relocate per `FUN_0046d6d0`; read the
   `0x98` trailer; decode each surface's sparse quaternion list).
3. Apply per-frame transforms to the ride's `ModelEntity` parts over the channel frame sequence (loop
   Main); validate against `monkey` (arms swing) / `totem`.
4. Load the `7`-prefixed LOD set for distant rides; ignore `P`-prefixed preview models in-world.

## Acceptance

- A ride (e.g. `monkey`) visibly plays its **Main** keyframe loop matching the original, and plays
  one-shot transitions (Create/Start/End) when the script triggers them, with `dotnet test` green.
