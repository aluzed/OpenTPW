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

## Remaining

1. Implement a keyframe loader: flat-load + relocate (mirror `FUN_0046d6d0`), read the `0x98` trailer
   to map each frame surface → base surface, and produce per-surface `Vector3[]` overriding the base
   positions. Validate by morphing `monkey` Main (`m1..m7`) and eyeballing against the original.
2. Renderer morph: lerp base↔frame vertex positions over the channel's frame sequence (CPU lerp into
   the vertex buffer, or a two-pose vertex shader) at the channel frame rate; loop Main.
3. Load the `7`-prefixed LOD set for distant rides; ignore `P`-prefixed preview models in-world.

## Acceptance

- A ride (e.g. `monkey`) visibly plays its **Main** keyframe loop matching the original, and plays
  one-shot transitions (Create/Start/End) when the script triggers them, with `dotnet test` green.
