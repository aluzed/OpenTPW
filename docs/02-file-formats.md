# 02 — File Formats: Detailed Progress

Source: `source/OpenTPW.Files/Formats/`. Legend: ✅ done · ⚠️ partial · ❌ to do.

## Archives & compression (foundation) — ✅

| Item | File | Notes |
|------|------|-------|
| `.WAD` archive | `Archive/WadArchive.cs` | Main asset container (ESPRITES, FONTS, LOBBY, UI…). Present on the disc. |
| `.SDT` archive | `Archive/SDTArchive.cs` | Sound bank. |
| Refpack | `Compression/Refpack.cs` + `RefpackCommands.cs` | EA/Bullfrog compression. |
| LZSS | `Compression/LZSS.cs` | Compression. |

## Formats read by the engine

| Format | Status | Code | Remaining work |
|--------|:------:|------|----------------|
| `.WCT` textures | ✅ | `Image/TextureFile*.cs` | A few pixel formats throw `NotImplementedException` (`TextureFile.Decode.cs`). |
| `.SAM` settings | ✅ | `String/SAMParser.cs` | — |
| `.SDT`/`.MP2` sounds | ✅ | `Sound/MP2File.cs` | Playback via NAudio (Windows-only, see [04](04-linux-compatibility.md)). |
| `.BFMU` strings | ✅ | `String/BFMUReader.cs` | — |
| `.BFST` strings | ✅ | `String/BFSTReader.cs` | — |
| `.BFUM` strings | ✅ | (BFMU variant) | — |
| `.MD2` models | ⚠️ | `Model/ModelFile.cs` | Parses mesh models (verified: PAUSED.MD2 → readable 3D text); not robust to all variants yet (GARROW.MD2 crashes); render integration to finish. See T-012. |
| `.MAP` maps | ⚠️ | (tied to World/Terrain) | Demo terrain hardcoded; parsing to generalize. |
| `.TPWS` saves | ⚠️ | `Save/SaveReader.cs` | Partial read; no write. |
| `.RSE` ride scripts | ⚠️ | `source/OpenTPW/VM/` | Loader/disassembler restored & tested; **~13% of opcodes** implemented. See T-007. |
| `.BF4` fonts | ⚠️ | `OpenTPW.Files/Formats/Font/BF4File.cs` | Reverse-engineered: container + per-glyph char code, **width/height and 1bpp bitmap decoded** (verified by rendering an atlas of a real font). A few header fields + renderer integration remain. See T-008. |
| `.LIPS` lip-sync | ❌ | — | Not implemented. |
| `.MTR` materials | ❌ | — | Not implemented. |
| `.TQI` / `.TGQ` video | ⚠️ | `OpenTPW.Files/Formats/Video/VideoFile.cs` | EA FourCC container parsed (chunk index, frame count, audio); TQI/EA-ADPCM codec decode remains. See T-008. |

## Focus: the ride-script VM (`.RSE`)

The most "reverse engineering" component of the project.

- **`VM/Opcode.cs`**: enumerates the opcodes (NOP, CRIT_LOCK, COPY, SETLV, branches,
  animations, RAND, JSR/RETURN…). Docs: <https://opentpw.gu3.me/formats/rsse-vm-instructions.html>.
- **`VM/RideVM.cs`**: the executor. Discovers handlers via reflection using the
  `OpcodeHandlerAttribute`. Reports a total of **210 documented opcodes**.
- **`VM/Handlers/`**: `Bounce.cs` (7), `Logic.cs` (8), `Math.cs` (5), `Misc.cs` (11)
  → **27 handlers** implemented. Several are no-ops (`TODO`).
- **Known limitation** (comment in `RideVM.cs`): `BranchTo` is "hacky" (offsets
  converted by hand), and the `.RSE` file loader (`rsseqFile`) is **disabled**.

**Remaining VM work**: re-enable the `.RSE` loader, harden the disassembler, implement
the ~180 missing opcodes, wire the VM to real rides.

## Assets confirmed present on the provided disc

Excerpts from the CD's `DATA/` (see [03](03-disc-compatibility.md)):
`ESPRITES.WAD`, `FONTS.WAD`, `LOBBY.WAD`, `UI.WAD`, many `*.SAM`,
`LEVELS/{JUNGLE,FANTASY,HALLOW,SPACE}`, `MOVIES/*.TGQ`, `PARTICLE/*.PLB`,
`GLOBAL/SOUND` & `SPEECH`. → enough to exercise every existing ✅/⚠️ parser.
