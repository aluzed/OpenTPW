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
| `.MD2` models | ⚠️ | `Model/ModelFile.cs` | Parses animated mesh models (verified: PAUSED.MD2 → readable 3D text). The static variant (frameCount 0, e.g. GARROW.MD2) is **detected and rejected with a clear error** (different header layout — full decode pending). Render integration to finish. See T-012. |
| `.MAP` | ⚠️ | `OpenTPW.Files/Public/MapFile.cs` | **Not terrain** — `.MAP` are audio category catalogs (CAT_*). GUID + **variant** (BANK/SFX) decoded; BANK **entry names** (e.g. `Sound\Kids`) and SFX **category header** (sound count + 3 float defaults 1.0/2.0/0.5) decoded and verified on real `cat_*`. Per-record mixing fields need Ghidra. See T-016. (Demo terrain is hardcoded in `World/Terrain`.) |
| `.TPWS` saves | ⚠️ | `Save/SaveReader.cs` | Partial read; no write. |
| `.RSE` ride scripts | ⚠️ | `source/OpenTPW/VM/` | Loader/disassembler restored & tested; **~13% of opcodes** implemented. See T-007. |
| `.BF4` fonts | ✅ | `OpenTPW.Files/Formats/Font/BF4File.cs` | Fully reverse-engineered: char code, width/height, 1bpp bitmap, **bearings + advance** (verified — renders correctly-spaced text). Engine/UI wiring is separate. See T-008. |
| `.LIP`/`.LIPS` lip-sync | ⚠️ | `OpenTPW.Files/Formats/Sound/LipSyncFile.cs` | Reverse-engineered: a list of uint32 mouth keyframe timestamps terminated by 0xFFFFFFFF. **Unit confirmed = microseconds** (last keyframe ≈ companion speech clip length across all 4 levels; `Duration`/`TimeOf` exposed). Mouth-shape semantics + renderer wiring remain. See T-008. |
| `.MTR` materials | ⚠️ | `OpenTPW.Files/Formats/Model/MTRFile.cs` | Reverse-engineered: magic 0x2E5915AF, version, name (companion to same-named `.MD2`). The mesh-coupled body is decoded as a `uint32[]` index array (`Indices`) — observed to ramp per-vertex up to ~the MD2 face count then small grouping values; exact semantics TBD. Constant trailing block kept raw. See T-008. |
| `.TQI` / `.TGQ` video | ✅ | `Video/VideoFile.cs`, `Video/TqiDecoder.cs` | Container + **EA-ADPCM audio** (`DecodeAudio()`) + **TQI video frames** (`DecodeFrame()`) fully decoded — verified pixel-accurate against ffmpeg (the Bullfrog logo). See T-008. |
| `.PLB` particle libs | ✅ | `Particle/ParticleLibraryFile.cs` | Reverse-engineered from `Tp2.plb` + the disc's `par_lib.h`: 16-byte header (count, record size), then fixed 320-byte records (raw param block + 48-byte name). The 105 effect names decode **exactly** to par_lib.h's `P_EFFECT_*` order (NULL, Sparks, … Test2D). Each effect's **16-stop RGBA colour ramp is decoded** (verified: Fire dark-red→bright, Smoke white w/ alpha fade); other params kept raw. See T-008. |

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
