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
| **OpenTPW.ModKit** | Asset editor/viewer (ImGui) | NAudio, System.Drawing.Common |
| **OpenTPW.Tests** | MSTest tests | — |

All targets are **`net8.0`**.

### Rendering stack
- **Veldrid 4.9**: cross-platform graphics abstraction (Vulkan / D3D11 / Metal / OpenGL).
- **Veldrid.SDL2**: cross-platform windowing.
- **Veldrid.SPIRV**: shader compilation.
- **ImGui.NET**: debug UI and ModKit.
- `ShaderCompiler` cross-compiles shaders to the backend target.

## What works (✅)

- **Virtual file system** over the game's `data/` folder, mounting `.WAD`
  (`WadArchive`) and `.SDT` (`SDTArchive`) archives.
- **Decompression**: Refpack (EA/Bullfrog) and LZSS.
- **`.WCT` textures**: full decode (including D4 wavelets) → ✅.
- **`.SAM` settings**: full parser.
- **Strings**: `.BFMU`, `.BFST`.
- **Sounds**: `.SDT`, `.MP2` (via NAudio — Windows-only at runtime).
- **Basic rendering**: window, demo terrain (`levels/jungle/...`), ImGui UI.

## What is partial (⚠️)

- **`.MD2` models** (`ModelFile.cs`): partial.
- **`.MAP` maps**, **`.TPWS` saves**: partial.
- **`.RSE` ride scripts**: VM present but **~27 of ~210 documented opcodes
  implemented (~13%)**. See `source/OpenTPW/VM/`. Several handlers are TODO/no-op
  (`Misc.cs`: `GETTIME`, `SETLV`, `ENDSLICE`…). The `.RSE` disassembler/loader is even
  commented out (`RideVM.cs`: `rsseqFile` disabled).

## What is not started (❌)

- **`.BF4` fonts**, **`.LIPS` lip-sync**, **`.MTR` materials**, **`.TQI`/`.TGQ` video**.
- Real game logic (park management, visitor AI, economy, functional saving).
- Multiplayer / online aspect (the game had servers, long since shut down).

## Technical markers found (`TODO`/`NotImplemented`)

- `VM/Handlers/Misc.cs`: `Crit lock`, `Set level`, `End slice`, `Get time` → TODO/no-op.
- `Render/Assets/Material.cs`, `Render/ShaderCompiler.cs`: `NotImplementedException` branches.
- `Files/Image/TextureFile.Decode.cs`: unhandled pixel formats → `NotImplementedException`.
- `ModKit/Editor/Viewers/{Settings,Sound}Viewer.cs`: `NotImplementedException`.

## Suggested priorities ("what's left to do")

1. **Make the project portable/testable** (Windows locks → see [04](04-linux-compatibility.md); fix failing tests).
2. **Install/extract assets** from the provided disc → see [03](03-disc-compatibility.md).
3. **Finish the ride VM**: re-enable the `.RSE` loader and complete the opcodes.
4. **Complete `.MD2` models + `.MAP` maps** to render a real level.
5. **Tackle the ❌ formats** (`.BF4`, `.MTR`, `.TQI`/`.TGQ`) — this is where **Ghidra**
   helps most: reverse `TP.EXE` to understand the undocumented formats
   (see [05](05-ghidra-reverse.md)).
