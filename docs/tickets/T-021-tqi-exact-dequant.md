# T-021 — `.TQI` video: exact (AAN) dequantization

- **Priority**: ⚪ Polish
- **Type**: Reverse engineering / codec
- **Status**: ☐ To do
- **Split from**: [T-008](T-008-unimplemented-formats.md).

## Context

The TQI video decoder (`OpenTPW.Files/Formats/Video/TqiDecoder.cs`, `VideoFile.DecodeFrame()`)
fully decodes frames and is **verified pixel-accurate against ffmpeg** using the standard
MPEG-1 intra dequant with a qscale derived from the header quant byte. The decode is in
perfect bitstream sync (the 32-bit-word byteswap was the key).

## Remaining work

Port EA's exact AAN dequant for a bit-exact match (the current plain dequant makes AC slightly
small vs DC → marginally blocky, chroma slightly off):

```
qscale            = (215 - 2*quant) * 5
intra_matrix[i]   = (ff_inv_aanscales[i] * mpeg1_intra[i] * qscale + 32) >> 14
intra_matrix[0]   = (ff_inv_aanscales[0] * mpeg1_intra[0]) >> 11   // DC
```

feeding a matching **AAN IDCT** (instead of the current straight IDCT).

## Acceptance criteria

- A decoded frame matches the ffmpeg reference within a tighter tolerance than today
  (extend `TqiDecoderTests`; ffmpeg used only as an external reference, never linked).

## Affected files

`source/OpenTPW.Files/Formats/Video/TqiDecoder.cs`, `source/OpenTPW.Tests/TqiDecoderTests.cs`.
