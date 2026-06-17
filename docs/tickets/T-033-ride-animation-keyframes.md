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

## Loader validated + runtime apply decompiled

The runtime apply path is now reversed (see docs/08), so the format is fully known:

- **Keyframes are time-indexed tracks.** `FUN_00470b60` is the interpolator: it reads each entry's
  leading `u16` keyframe time (the `0xFFFF0000|time` dwords), finds the keys bracketing the current
  animation time, and returns a lerp factor. Entry stride by track type: `1→4`, `2→20`, `4→16` B.
- **`FUN_00471860` applies a surface's tracks**, selected by a flags word (`desc[1]`): vertex morph
  (`0x1000`, per-vertex `vec3` lerp into the surface vertex buffer), rotation (`0x8`, quaternion →
  matrix via `FUN_00474490`), scale (`0x80`), translation (`0x200`/`0x1`). `FUN_004679d0` runs it for
  every surface.
- It's a **hybrid**: a surface can morph vertices *and/or* carry T/R/S tracks. `monkeym1` surf 5
  (`m_arm`) carries the rotation-quaternion track (the arms swing). Animating-textures
  (`FUN_00467310`, UV scroll) is a separate system.

Earlier note corrected: the payload is **not** "transform-only" — vertex morph *and* TRS tracks
coexist; only morph surfaces need a dynamic vertex path.

## Remaining

1. Keyframe loader: flat-load + relocate (mirror `FUN_0046d6d0`), read the `0x98` trailer's surface
   records, and decode each surface's tracks (time-keyed: morph `vec3` / rotation quaternion / scale /
   translation), mapping each to the base surface.
2. Per-frame evaluator (mirror `FUN_00470b60`/`FUN_00471860`): bracket keys by animation time,
   interpolate, compose T·R·S onto the surface transform; lerp vertex positions for morph surfaces.
3. Apply to the ride's `ModelEntity` parts over the channel frame sequence (loop Main); validate
   against `monkey` (arms swing) / `totem`.
4. Load the `7`-prefixed LOD set for distant rides; ignore `P`-prefixed preview models in-world.

## Acceptance

- A ride (e.g. `monkey`) visibly plays its **Main** keyframe loop matching the original, and plays
  one-shot transitions (Create/Start/End) when the script triggers them, with `dotnet test` green.
