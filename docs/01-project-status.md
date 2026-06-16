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

- **Stage: boots & runs on Linux** (window + Vulkan render loop), not yet playable. Verified by
  running on an AMD Radeon (Mesa/Vulkan) after fixing the Vulkan `libdl` load ([T-023](tickets/T-023-linux-vulkan-libdl.md));
  the scene currently renders black ([T-024](tickets/T-024-linux-black-screen.md)).
- **Upstream** (`OpenTPW/OpenTPW`) is dormant (last real activity early 2025). **This fork**
  is under active development: full Linux portability, CI, and a large reverse-engineering
  push this session (see the Update below).
- **Tests**: ~38 passing, 0 failing (was 7/7 failing) + ~9 inconclusive integration tests that
  need a real asset (gated on `TPW_*_SAMPLE`/`OPENTPW_GAMEPATH`). Build: **0 warnings**.

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

> **Update (2026-06-16):** major progress this session.
> - Full Linux portability + CI; every file format decodes at least partially (no more ❌).
> - **Reverse engineering unblocked via Ghidra.** The disc binaries are SafeDisc-encrypted, so
>   an unprotected no-CD `tp.exe` (abandonware) was imported into Ghidra 12. From it: the `.MD2`
>   loader (version gate), the confirmation that `.MTR` is **not** a runtime format, the TQI
>   codec (float AAN IDCT), and — biggest — the **ride-VM opcode table** (106 opcodes + operand
>   counts) and executor, which let **Batch A (all 43 pure VM opcodes) be implemented & tested**.
>   See [05-ghidra-reverse.md](05-ghidra-reverse.md) and [06-rse-vm-opcodes.md](06-rse-vm-opcodes.md).
> - Catch-all tickets T-008/T-012 were split into focused per-concern tickets (T-015…T-022).

## What works (✅)

- **Linux/cross-platform**: builds + tests on Linux, CI, portable audio (NLayer + OpenAL),
  `OPENTPW_GAMEPATH`, case-insensitive asset resolution. See [04](04-linux-compatibility.md).
- **Virtual file system** over `data/`, mounting `.WAD`/`.SDT` archives; `WadTool` CLI.
- **Decompression**: Refpack (EA/Bullfrog) and LZSS.
- **Formats fully decoded**: `.WCT` textures, `.SAM`, strings (`.BFMU`/`.BFST`),
  `.SDT`/`.MP2` sounds, **`.BF4` fonts** (glyphs + metrics), **`.TQI`/`.TGQ` video**
  (EA container + EA-ADPCM audio + TQI frames), and **`.PLB` particles** (effect names + colour
  ramps). `.MTR` is a tool artifact the game never loads (texture binding lives in the `.MD2`).
- **`.RSE` ride VM**: loader/disassembler restored; **50 / 106 opcodes — Batch A (all 43 pure
  opcodes) complete** (arithmetic, flags, two stacks, branches/JSR/RETURN, date/time, timers,
  cross-VM variables, the WAIT scheduler), all Ghidra-verified.
- **Basic rendering**: window, demo terrain, ImGui UI.

## What is partial (⚠️)

- **`.RSE` opcodes**: 50 / 106. The remaining **63 are `engine`** side-effects (objects,
  animations, sound, lights, walk/limbo, scream) + `SPAWNCHILD`, blocked on the ride engine.
  See [T-007](tickets/T-007-vm-opcodes-rse.md).
- **`.MD2` models**: parses + verified-renders the current format; the version gate (0xDD/0xCB)
  is Ghidra-confirmed and rejects the legacy/static variant cleanly. Static-variant decode +
  render integration remain. [T-015](tickets/T-015-md2-static-variant.md).
- **`.LIP` lip-sync**: keyframe timestamps decoded; **unit confirmed = microseconds**. Mouth-shape
  semantics remain. [T-020](tickets/T-020-lip-mouth-shapes.md).
- **`.MAP`**: audio category catalog (not terrain). Variant (BANK/SFX) + BANK entry names + SFX
  category header decoded; per-record mixing fields remain. [T-016](tickets/T-016-map-entry-records.md).
- **`.PLB`**: names + colour ramp decoded; the other parameter fields remain. [T-019](tickets/T-019-plb-parameter-fields.md).
- **`.TPWS` saves**: partial reader; no sample on the install disc to verify. [T-017](tickets/T-017-tpws-saves.md).

## What is not started (❌)

- Real game logic (park management, visitor AI, economy, functional saving).
- Multiplayer / online aspect (the game's servers are long shut down).
- Engine wiring of the decoded assets (fonts/models/video into the renderer).

## Remaining work (see [tickets/](tickets/))

The project has reached the **engine frontier**: the formats and the pure VM are done, and most
of what's left needs the ride engine itself (which also lets further RE be verified).

1. **Ride engine** — the biggest unlock: instantiate ride objects/animations/sound/lights in the
   world. This backs **Batch B** of the VM (the 63 engine opcodes, incl. `SPAWNCHILD`) and lets
   `.PLB` particles / `.LIP` lip-sync / `.MAP` mixing actually run.
2. **Engine wiring**: ⏳ in progress. **Bitmap fonts are now wired** — `FontAtlas` (CPU,
   unit-tested) packs `.BF4` glyphs into an atlas + layout, `Render/Assets/Font.cs` uploads it,
   and `Graphics.DrawText` draws a quad per glyph; placing it in the live UI (e.g. button
   labels) is the next visible step. Models (`.MD2`) already render in the lobby. Drawing a real
   level from textures remains (terrain is hardcoded).
3. **Format tails** (each now its own ticket): `.MD2` static variant [T-015], `.MAP` records
   [T-016], `.TPWS` [T-017], `.PLB` params [T-019], `.LIP` shapes [T-020], exact TQI dequant
   [T-021], mono EA-ADPCM [T-022]. Several need the engine or Ghidra on the spawn/render paths.

Done this project so far: Linux portability + CI (T-001…T-006, T-013, T-014), build warnings
→ 0 (T-009), VM correctness (T-010, T-011), and the format/VM RE above.
