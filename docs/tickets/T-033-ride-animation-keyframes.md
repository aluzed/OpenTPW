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

## Done — loader + evaluator + rotation wired

- ✅ **Keyframe loader** (`RideKeyframeFile`, OpenTPW.Files): reads the `0x98` trailer's per-surface
  records and each surface's time-keyed tracks (rotation quaternion stride-20, translation/scale
  vec3 stride-16), bounded by the `0xFFFF` sentinel + monotonic-time rule. Validated against the real
  `monkeym1.MD2` (2 rotation tracks on surface 5 = `m_arm`, full 360° Z-turn, duration 40).
- ✅ **Evaluator**: `SampleRotation` (slerp) / `SampleVector` (lerp) bracket keys by time, clamped to
  the track range. Unit-tested (synthetic frame + the real asset via `TPW_KEYFRAME_SAMPLE`).
- ✅ **Wired into the engine**: `Ride` loads each channel's keyframe file and hands it to `RideEngine`
  (`SetChannelKeyframes`); when a channel with tracks is the active animation, `RideEngine.Update`
  evaluates each animated surface's rotation, swizzles it to world space and composes it onto the
  part's base rotation (replacing the bob for those surfaces). **Verified in-game**: the `monkey`
  ride's animated surface visibly rotates from the real decoded track.

## Done — translation + scale composition

- ✅ **Key-header marker decoded**: a key's header dword is `(marker << 16) | time`. The high u16 is
  the interpolation type — `0xFFFF` = rotation (quaternion, 4 floats, slerp), `0x0000` = linear vec3
  (translation/scale, 3 floats, lerp). The loader now reads all three track types with the right
  marker.
- ✅ **Robust track bounding**: tracks are contiguous, so each is bounded by the next track offset
  (collected across all records) plus the marker + strictly-increasing-time rule — fixes the
  identity-track over-read that otherwise pulled adjacent record data in as bogus keys.
- ✅ **TRS composition in the engine**: `RideEngine` composes scale (multiply, Y/Z-swizzled onto the
  part's base scale) and translation (additive, swizzled) alongside rotation, **per-track looped**
  (each track wraps over its own last-key time for looping anims, clamps for one-shots) so a short
  rotation track keeps spinning while a full-length identity scale stays a no-op.
- ✅ **Validated**: parse confirmed against real data — `space_bouncy` grow-in (`0.07→1.0`),
  `space_bumper` vertical squash (`1,0.71,1`/`1,0.53,1`). Verified in-game on `space_hoverbot` (Main,
  10 animated surfaces): visible breathing-scale + multi-part rotation. Unit test added for the
  `0x0000` scale-marker parse + lerp eval.

> Finding: **translation tracks are absent/identity across all sampled rides** (4 worlds) — rides
> animate by rotation and scale only. Translation is wired for completeness but is a no-op in practice.

## Remaining

1. **Vertex-morph** track (flag `0x1000`): decode the per-vertex `vec3` keyframe data (rec dword
   `[0xa]` pointer) and lerp it into a dynamic vertex buffer — the only part needing a morph vertex
   path. (Rotation/translation/scale ride on the existing per-mesh transform.)
2. Tune the playback rate vs. the original's time units (`KeyframeRate`); confirm pivot/compose order
   on more rides.
3. Handle multi-frame channels' extra files (`m2..m7`) and surfaces with two records targeting the
   same index (both arms).
4. Load the `7`-prefixed LOD set for distant rides; ignore `P`-prefixed preview models in-world.

## Acceptance

- A ride (e.g. `monkey`) visibly plays its **Main** keyframe loop matching the original, and plays
  one-shot transitions (Create/Start/End) when the script triggers them, with `dotnet test` green.
