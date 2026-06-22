# T-025 — BF4: decode the antialiased (multi-bit) font variant

- **Priority**: 🟢 Low (1bpp fonts work; AA fonts are a visual upgrade)
- **Type**: File format / reverse engineering
- **Status**: ⚠️ Partial — the **raw-4bpp antialiased** variant is decoded (all the `*AA` faces + several
  others); the **compressed-4bpp** variant (menu/title big faces) remains.
- **Blocks**: nicer UI typography (menu/title fonts).

## Solved: the encoding is in the glyph block (offset 12)

The glyph block's **offset-12 field is the encoding tag** (not a constant): `2` = 1bpp, `0` = **raw 4bpp**
(antialiased), `1` = **compressed 4bpp**. (Offset 8 is `width*height/2`, the 4bpp *uncompressed* size, not
the stored byte count — which is why the byte-ratio heuristic below was noisy.)

- **Raw 4bpp (tag 0) — done.** Continuous 4-bit coverage nibbles, high nibble first, scaled 0..15→0..255.
  Verified by rendering: `GAME6AA`/`GAME9AA`/`TITLESMALL`/… 'A','B','H' come out as clean antialiased
  glyphs. `BF4File` now exposes `GlyphEncoding` + per-pixel `Coverage`, and `FontAtlas` blits coverage as
  the alpha channel (1bpp glyphs stay 0/255). Covers the `*AA` faces, the `DATE*`/`SESHSMALL`/`TITLESMALL`
  /`GAMEBOLD9/10` faces. Unit-tested (`DecodesRaw4BppAntialiasedGlyph`).
- **Compressed 4bpp (tag 1) — remaining.** The big menu/title faces (`MENU*`, `TITLE*BIG/MED`, `CASH*`,
  `SESH*`, `MATISSE*`, `GAME12AA`) store fewer bytes than `width*height/2`, so they're a compressed 4bpp
  stream. The decompression scheme isn't cracked yet; these still fall back to a rough 1bpp read. Needs the
  BF4 blit/decompressor traced in Ghidra (no `.bf4`/`F4FB` string xref — search the `F4FB` magic constant
  `0x42464634`), or more sample analysis of the byte stream.

## Problem

`BF4File`/`FontAtlas` decode the **1bpp** glyph variant only: a continuous MSB-first bitmap,
`width` bits per row. Several shipped fonts are **antialiased / multi-bit** and the 1bpp reader
turns their glyph data into noise (garbled text).

Detection rule observed on `Language/English/*.bf4` (compare the glyph's bitmap byte count to the
1bpp expectation `ceil(width*height/8)` for `'A'`):

| Font | 'A' w×h | bitmap bytes | 1bpp expects | ratio | kind |
|------|---------|--------------|--------------|-------|------|
| GAME12 | 10×12 | 16 | 15 | 1.1 | **1bpp ✓** |
| GAME10 | 7×10 | 16 | 9 | 1.8 | multi-bit |
| MENUMED | 18×29 | 152 | 66 | 2.3 | multi-bit |
| MENUSMALL | 15×23 | 128 | 44 | 2.9 | multi-bit |
| GAMEBOLD12 | 11×12 | 56 | 17 | 3.3 | multi-bit |

So `GAME12` (and the other ratio-~1 `GAME*`/`CONSOLE*` fonts) decode cleanly; `MENU*`, `TITLE*`,
the `*AA` variants and the bold faces do not. The current UI therefore uses `GAME12` everywhere
text is drawn (loading screen, `PurpleButton` labels).

## To do

1. Determine the multi-bit encoding: bits-per-pixel (the ratios suggest ~2–4 bpp, possibly
   variable), row alignment, and whether a palette/gamma is involved. Cross-check in Ghidra on the
   font blit path.
2. Decode coverage into the alpha channel of `FontAtlas` (it already stores RGBA with alpha = the
   mask; an AA glyph would write a gradient instead of 0/255).
3. Switch the UI to the intended menu/title faces once they decode.

## Affected files

`source/OpenTPW.Files/Formats/Font/BF4File.cs` (`DecodeBitmap`),
`source/OpenTPW.Files/Formats/Font/FontAtlas.cs` (alpha blit),
`source/OpenTPW/UI/Widgets/PurpleButton.cs` (font choice).
