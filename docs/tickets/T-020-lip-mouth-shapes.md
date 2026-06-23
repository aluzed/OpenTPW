# T-020 — `.LIP` mouth-shape semantics + lip-sync wiring

- **Priority**: 🟡 Feature
- **Type**: Reverse engineering
- **Status**: ⚠️ Mostly done — **mouth-shape semantics resolved** (item 1) and the **lip-sync is wired to
  real speech playback with visible output** (item 2): an in-engine advisor face plays a real `sp_001.mp2`
  clip and changes its mouth per viseme from the companion `.LIP`, in sync. The viseme→advisor-mesh-part
  names are RE'd (`mouth - normal/aah/eee/ooh/sss`). Only rendering the *real* advisor model's named
  sub-meshes (vs the demo's procedural mouth) remains.
- **Split from**: [T-008](T-008-unimplemented-formats.md).

## Context

`LipSyncFile` decodes the flat list of keyframe timestamps (terminated by `0xFFFFFFFF`). The **timestamp
unit is microseconds** (the last keyframe ≈ the companion `speechHD.SDT` clip's duration on all four
levels), exposed via `Duration` / `TimeOf` / `UnitsPerSecond`.

## Done (item 1 — mouth-shape semantics)

- **No mouth shape is encoded in the `.LIP`.** Empirically every keyframe is a bare `uint32` timestamp:
  the values use all their low bits for the time (no spare high bits — a 28 s clip needs 25 bits) and the
  file is exactly `N×u32 + terminator`, leaving no room for a parallel shape stream.
- **The engine owns the shapes.** Ghidra (`tp.exe FUN_0044b2e0`) maps a shape code to a named mouth mesh
  part — `0 = closed`, `1 = normal`, `2 = aah`, `3 = eee`, `4 = ooh`, `5 = sss` (five visemes). The `.LIP`
  only says *when* the mouth changes (phoneme boundaries), not *which* shape; the engine selects the
  viseme at runtime.
- Exposed a `MouthShape` enum + `VisemeCount`, `IntervalAt(time)` (which keyframe interval is active) and
  `ShapeAt(time)` (cycles the five visemes deterministically as an engine-side stand-in; `Closed` outside
  the clip). Headless timing test (`ResolvesIntervalsAndCyclesVisemesInSync`) + the real jungle sample
  pass.

## Done (item 2 — wiring to real playback)

- **Viseme → advisor mesh-part names RE'd** (`FUN_0044b2e0`): the engine resolves a viseme by string-
  matching the advisor model's named sub-meshes — `1 = "mouth - normal"`, `2 = "mouth - aah"`,
  `3 = "mouth - eee"`, `4 = "mouth - ooh"`, `5 = "mouth - sss"`, `0` = closed (no part), `-1` = "bug head"
  (whole head). Exposed as `MouthShape.MeshPartName()` (unit-tested).
- **Lip-sync wired to real speech + visible output** (`AdvisorPanel`, enabled by `OPENTPW_ADVISOR_DEMO=1`):
  loads `levels/jungle/Speech/speechHD.SDT`, plays `sp_001.mp2` via the audio system, loads the companion
  `lips/sp_001.LIP`, and each frame drives a visible advisor mouth from `LipSyncFile.ShapeAt(elapsed)` in
  sync with the audio clock. Verified in-game: the mouth + on-screen label step through the visemes as the
  clip plays (e.g. `Ooh [mouth - ooh]` @14.6s → `Sss [mouth - sss]` @23.8s of the 28.6 s clip), the elapsed
  time tracks the audio, and the clip loops.

## Remaining (rendering polish)

- Swap the **real advisor model's** named `mouth - *` sub-meshes (now unblocked — the part names are RE'd)
  instead of the demo's procedurally-drawn mouth, and place the advisor in its proper screen position with
  the message system (`Advisor.sam`). The exact runtime viseme selection (the `.LIP` only marks phoneme
  boundaries; `ShapeAt` currently cycles the five visemes) can be traced from the speech-update caller if
  the precise per-interval viseme matters; the file format + the engine wiring are settled.

## Affected files

`source/OpenTPW.Files/Formats/Sound/LipSyncFile.cs`, `source/OpenTPW.Tests/LipSyncFileTests.cs`,
plus engine wiring (TBD).
