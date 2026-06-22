# T-022 — EA-ADPCM mono audio support

- **Priority**: 🟡 Feature
- **Type**: Codec
- **Status**: ⚠️ Implemented — the mono path is in and tested for plumbing + sample-count; **waveform
  verification awaits a real mono sample** (none ships in this install — `Movies/` is empty and no
  `.TGQ`/`.TQI` is in any WAD).
- **Split from**: [T-008](T-008-unimplemented-formats.md).

## Context

`VideoFile.DecodeAudio()` decodes the EA-ADPCM audio in `.TQI`/`.TGQ` containers, **verified for stereo**
(a real `BF.TGQ` decodes to exactly 194815 samples/channel matching the header, coherent audio).

## Done

- Added the **mono** EA-ADPCM path. `DecodeAudio()` now dispatches on the `0x82` channel count: 1 → mono,
  2 → stereo, else unsupported. `DecodeScdlMono` mirrors the stereo decoder for a single channel — an
  8-byte block header (sample count + prev/cur history), then sub-blocks of one `(coeff|shift)` byte and
  28 data bytes, each data byte packing **two consecutive samples** (high nibble then low) instead of the
  stereo L/R pair (per the FFmpeg `adpcm_ea` reference, the single-channel form of the same codec). Mono
  output is non-interleaved with `Channels = 1`.
- `VideoFileTests.DecodesMonoEaAdpcm` builds a synthesised mono `SCHl`/`SCDl` and checks the decode hits
  the header's sample count with the expected first samples (validates the channel dispatch + block
  accounting; the per-nibble math is the proven stereo math).

## Remaining

- **Waveform verification against a real mono EA-ADPCM clip.** None is present here; if a mono movie or
  EA-ADPCM speech clip turns up, decode it and confirm the count + audible coherence (the env-gated
  `ParsesRealMovieSample` covers the stereo case the same way).

## Affected files

`source/OpenTPW.Files/Formats/Video/VideoFile.cs`, `source/OpenTPW.Tests/VideoFileTests.cs`.
