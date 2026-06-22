# T-020 — `.LIP` mouth-shape semantics + lip-sync wiring

- **Priority**: 🟡 Feature
- **Type**: Reverse engineering
- **Status**: ⚠️ Partial — **mouth-shape semantics resolved** (item 1): shapes are *not* in the file; the
  engine has five visemes and picks one per keyframe interval at runtime. Exposed + headless timing test.
  Real engine wiring to an on-screen advisor mouth (item 2) remains.
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

## Remaining (item 2 — wiring)

- Drive a real advisor character's mouth mesh from `ShapeAt` in sync with the speech clip (engine
  integration — needs the advisor model + speech playback hooked together; the advisor head's per-viseme
  mesh parts are what `FUN_0044b2e0` resolves). If the exact runtime viseme-selection (random vs cycling)
  matters, it can be traced from the speech-update caller; the file format itself is fully settled.

## Affected files

`source/OpenTPW.Files/Formats/Sound/LipSyncFile.cs`, `source/OpenTPW.Tests/LipSyncFileTests.cs`,
plus engine wiring (TBD).
