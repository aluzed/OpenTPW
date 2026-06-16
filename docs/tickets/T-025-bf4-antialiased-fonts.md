# T-025 — BF4: decode the antialiased (multi-bit) font variant

- **Priority**: 🟢 Low (1bpp fonts work; AA fonts are a visual upgrade)
- **Type**: File format / reverse engineering
- **Blocks**: nicer UI typography (menu/title fonts).

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
