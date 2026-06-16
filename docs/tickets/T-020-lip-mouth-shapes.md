# T-020 — `.LIP` mouth-shape semantics + lip-sync wiring

- **Priority**: 🟡 Feature
- **Type**: Reverse engineering
- **Status**: ☐ To do
- **Split from**: [T-008](T-008-unimplemented-formats.md).

## Context

`LipSyncFile` (`OpenTPW.Files/Formats/Sound/LipSyncFile.cs`) decodes the flat list of
keyframe timestamps (terminated by `0xFFFFFFFF`). The **timestamp unit is confirmed to be
microseconds** (the last keyframe ≈ the companion `speechHD.SDT` clip's duration on all four
levels), exposed via `Duration` / `TimeOf` / `UnitsPerSecond`.

## Remaining work

1. **Mouth-shape semantics**: each keyframe is currently a single `uint32` timestamp with no
   explicit shape index. Determine whether a mouth shape is encoded (e.g. high bits of the
   timestamp, a parallel stream, or an implicit open/close toggle).
2. **Wiring**: drive a character's mouth from the keyframes in sync with the speech clip
   (engine integration; verify against the `sp_001` + `speechHD.SDT` pairing per level).

## Acceptance criteria

- Keyframes resolve to mouth shapes (or a documented toggle), and a sample plays in sync with
  its audio under a headless timing test.

## Affected files

`source/OpenTPW.Files/Formats/Sound/LipSyncFile.cs`, plus engine wiring (TBD).
