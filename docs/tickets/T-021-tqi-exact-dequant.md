# T-021 — `.TQI` video: exact (AAN) dequantization

- **Priority**: ⚪ Polish
- **Type**: Reverse engineering / codec
- **Status**: ☐ To do
- **Split from**: [T-008](T-008-unimplemented-formats.md).

## Context

The TQI video decoder (`OpenTPW.Files/Formats/Video/TqiDecoder.cs`, `VideoFile.DecodeFrame()`)
fully decodes frames and renders the **visually correct** image (manually confirmed: BF.TGQ
reconstructs the Bullfrog logo), in perfect bitstream sync (the 32-bit-word byteswap was the
key). It uses the standard MPEG-1 intra dequant with a hand-calibrated qscale; the per-pixel
values are *not* bit-exact to the game (the enforced test only checks a loose sanity bar).

## Ghidra findings (no-CD `tp.exe`, 2026-06-16)

The original codec was located and analyzed (the data sections `idct_dat`/`uva_data` are
runtime-built BSS; only the IDCT's float constants are in the image):

- **The IDCT is a float AAN IDCT** (`FUN_00677140`), not an integer one. Its constants live at
  `idct_dat` and decode to the textbook AAN values:
  - `0x00fbc024` → `0.38268343` = sin(π/8)
  - `0x00fbc01c` → `0.54119611` = √2·cos(3π/8)
  - `0x00fbc020` → `1.30656300` = √2·cos(π/8)
  - `0x00fbc018` → Q32 `0.35355339` = 1/(2√2)  (used via a 64-bit fixed-point multiply)
  - `0x00fbc028` → Q32 `0.35058594` (scale/round variant)
- **Dequant** is `coeff * matrix[pos]` against a runtime-built **Q16 fixed-point** matrix at
  `DAT_00fb7820` (DC: `coeff * matrix[0] >> 16`; AC similar). The matrix is folded with the
  AAN pre-scale (that's *why* an AAN IDCT is required and why a plain IDCT needs different
  scaling). The matrix **builder** (quant → 64-entry Q16 table) wasn't pinned down yet — the
  standard MPEG-1 intra matrix is not stored as plaintext bytes, so it's computed/encoded.

### Implication for our decoder

`TqiDecoder` currently pairs a **plain integer IDCT** (jsmpeg) with a plain MPEG-1 dequant and
a hand-calibrated `qscale = (215-2*quant)*5/10`. That is *self-consistent* and renders the
frame correctly (the `/10` is the fudge that compensates). A bit-exact match to the game means
replacing **both** halves together: the float AAN IDCT above **and** the AAN-prescaled Q16
dequant matrix — you can't swap one without the other.

## Remaining work

1. Pin down the dequant-matrix builder (`quant → Q16 matrix[64]`) in the binary.
2. Port the float AAN IDCT (`FUN_00677140`) and feed it the prescaled coefficients.
3. Verify against an ffmpeg reference frame.

## Decision / priority

This is ⚪ polish: the current output is already visually correct, and **pixel-exactness can't
be enforced in CI** (no ffmpeg/sample there). It's a sizeable paired port (IDCT + dequant) of
hand-optimized float asm for marginal gain, so it's **deferred** unless exact playback fidelity
becomes a goal. The findings above de-risk it when picked up.

## Acceptance criteria

- A decoded frame matches the ffmpeg reference within a tighter tolerance than today
  (extend `TqiDecoderTests`; ffmpeg used only as an external reference, never linked).

## Affected files

`source/OpenTPW.Files/Formats/Video/TqiDecoder.cs`, `source/OpenTPW.Tests/TqiDecoderTests.cs`.
