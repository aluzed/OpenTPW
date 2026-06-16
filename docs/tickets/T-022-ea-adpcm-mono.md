# T-022 — EA-ADPCM mono audio support

- **Priority**: 🟡 Feature
- **Type**: Codec
- **Status**: ☐ To do
- **Split from**: [T-008](T-008-unimplemented-formats.md).

## Context

`VideoFile.DecodeAudio()` decodes the EA-ADPCM audio in `.TQI`/`.TGQ` containers, **verified
for stereo** (a real `BF.TGQ` decodes to exactly 194815 samples/channel matching the header,
coherent audio). Only the stereo path (interleaved L/R SCDl blocks) is implemented.

## Remaining work

1. Add the **mono** EA-ADPCM path (single-channel SCDl blocks; the "PT" header at 0x82 gives
   the channel count).
2. Verify against a mono sample — the disc's speech is mono (`speechHD.SDT` is mono 22050 Hz
   MP2, and mono EA-ADPCM movies, if any, can be checked the same way).

## Acceptance criteria

- A mono source decodes to the header's sample count with coherent output; extend
  `VideoFileTests`.

## Affected files

`source/OpenTPW.Files/Formats/Video/VideoFile.cs`, `source/OpenTPW.Tests/VideoFileTests.cs`.
