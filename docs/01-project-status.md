# 01 — Project Status

## Nature of the project

OpenTPW is a **clean-room re-implementation** of *Theme Park World*. It is **not** a
crack or a patch of the original game: it is a new C# engine that:

1. Reads the original game's asset files (textures, models, sounds, scripts…);
2. Renders them through a modern rendering backend (Veldrid);
3. Re-implements the game logic (ride-script VM, terrain, UI…).

You therefore **need a legal copy of the game** to provide the assets — which the
`.7z` at the repo root supplies (see [03](03-disc-compatibility.md)).

## Maturity

- **Stage: very early, not playable** (as stated in the README).
- Last commit: **2025-02-20**. Recent activity is mostly cosmetic (README, buttons).
  The project appears **paused / dormant**.
- No CI (`.github` was removed), few tests (2 files: shaders + filesystem), and they
  currently **fail** (see tickets).

## Architecture (`source/OpenTPW.sln`)

| Project | Role | Notable dependencies |
|---------|------|----------------------|
| **OpenTPW.Common** | Virtual file system, client abstractions | Zio |
| **OpenTPW.Files** | **Format parsers** (the reverse-engineering core) | SharpZipLib, ImageSharp, StbImage |
| **OpenTPW** | The game: rendering, world, UI, ride VM | Veldrid, SDL2, ImGui.NET, NAudio |
| **OpenTPW.ModKit** | Asset editor/viewer (ImGui) | NLayer, OpenAL, System.Drawing.Common |
| **OpenTPW.WadTool** | CLI to list/extract `.wad` (DWFB) archives | — |
| **OpenTPW.Tests** | MSTest tests | — |

All targets are **`net8.0`**.

### Rendering stack
- **Veldrid 4.9**: cross-platform graphics abstraction (Vulkan / D3D11 / Metal / OpenGL).
- **Veldrid.SDL2**: cross-platform windowing.
- **Veldrid.SPIRV**: shader compilation.
- **ImGui.NET**: debug UI and ModKit.
- `ShaderCompiler` cross-compiles shaders to the backend target.

> **Update (2026-06-16):** large progress this session — full Linux portability + CI, the
> ride-script VM loader restored, and **every file format now decodes at least partially**
> (no more ❌). See the [file-format table](02-file-formats.md) and [tickets](tickets/).

## What works (✅)

- **Linux/cross-platform**: builds + tests on Linux, CI, portable audio (NLayer + OpenAL),
  `OPENTPW_GAMEPATH`, case-insensitive asset resolution. See [04](04-linux-compatibility.md).
- **Virtual file system** over `data/`, mounting `.WAD`/`.SDT` archives; `WadTool` CLI.
- **Decompression**: Refpack (EA/Bullfrog) and LZSS.
- **Formats fully decoded**: `.WCT` textures, `.SAM`, strings (`.BFMU`/`.BFST`),
  `.SDT`/`.MP2` sounds, **`.BF4` fonts** (glyphs + metrics), and **`.TQI`/`.TGQ` video**
  (EA container + EA-ADPCM audio + TQI frames — verified pixel-accurate).
- **`.RSE` ride VM**: loader/disassembler restored; branching verified.
- **Basic rendering**: window, demo terrain, ImGui UI.

## What is partial (⚠️)

- **`.RSE` opcodes**: 30 / ~210 implemented (arithmetic, logic, branches). The rest are
  ride-engine side-effects needing engine hooks. See [T-007](tickets/T-007-vm-opcodes-rse.md).
- **`.MD2` models**: parses + verified-renders the localized mesh variant; not robust to
  every variant (the static GARROW variant differs). [T-012](tickets/T-012-partial-formats.md).
- **`.LIP` lip-sync**: keyframe timestamps decoded (unit not pinned). **`.MTR` materials**:
  header + name decoded; the mesh-coupled index array is raw. [T-008](tickets/T-008-unimplemented-formats.md).
- **`.MAP`**: identified as an audio category catalog (not terrain); GUID decoded, entries raw.
- **`.TPWS` saves**: spec known, partial reader; no sample on the install disc to verify.

## What is not started (❌)

- Real game logic (park management, visitor AI, economy, functional saving).
- Multiplayer / online aspect (the game's servers are long shut down).
- Engine wiring of the decoded assets (fonts/models/video into the renderer).

## Remaining work (see [tickets/](tickets/))

1. **Engine wiring**: use the decoded fonts (`.BF4`), models (`.MD2`) and textures in the
   renderer to draw a real UI/level (currently terrain is hardcoded).
2. **Finish the ride VM** opcodes ([T-007](tickets/T-007-vm-opcodes-rse.md)) — needs the
   ride-entity/animation engine to back the side-effecting opcodes.
3. **Format polish**: `.MD2` variant robustness + `.MAP`/`.TPWS` ([T-012](tickets/T-012-partial-formats.md)),
   `.MTR` index decode, mono-audio, exact-vs-ffmpeg TQI dequant.
4. **Tech debt**: build warnings ([T-009](tickets/T-009-build-warnings.md)).
