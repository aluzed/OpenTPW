# 04 — Linux Compatibility

**Short answer: potentially yes, but not as-is.** The technical foundation is
cross-platform and the solution **builds on Linux** (verified), but the code contains
several Windows locks that break at runtime.

## What favors Linux ✅

| Item | Why it's OK |
|------|-------------|
| **.NET 8** (`net8.0`) | Officially cross-platform runtime (Linux/macOS/Windows). |
| **Veldrid 4.9** | Graphics abstraction: **Vulkan** or **OpenGL** backend on Linux. |
| **Veldrid.SDL2** | Windowing/input via SDL2 (native on Linux). |
| **Veldrid.SPIRV** | Cross-platform shader compilation. |
| **ImGui.NET, ImageSharp, StbImageSharp, SharpZipLib, Zio** | Fully managed, portable. |

Nothing in the rendering architecture prevents Linux. **The full solution compiles on
Linux** (`dotnet build` → 6 projects, 0 errors).

## The Windows locks to fix ⚠️

> The build succeeds because the packages exist for all platforms — the failures are
> at **runtime**. The failing tests already demonstrate them.

### 1. Hardcoded path separators (blocking)
```
source/OpenTPW/Client/GameDir.cs:15
    return Path.Join( Settings.Default.GamePath, path ).Replace( "/", "\\" );

source/OpenTPW.Common/Files/BaseFileSystem.cs:200
    return Path.Combine( basePath, relativePath.TrimStart('/') ).Replace( "/", "\\" );
```
The `.Replace("/", "\\")` forces Windows backslashes → **paths do not resolve on
Linux**. Replace with `Path.DirectorySeparatorChar` / let `Path.Join`/`Path.Combine`
do the work. → ticket [T-001](tickets/T-001-backslash-paths-linux.md).

### 2. `NAudio` (Windows-only audio)
`PackageReference NAudio 2.2.1` in **OpenTPW** and **OpenTPW.ModKit**.
NAudio relies on WinMM/WASAPI → **no sound on Linux**.
Usage: `ModKit/Editor/Viewers/SoundViewer.cs` (`WaveOutEvent`).
→ ticket [T-003](tickets/T-003-naudio-not-portable.md).

### 3. `System.Drawing.Common` (Windows-only on .NET 8)
`PackageReference System.Drawing.Common 8.0.0` in **OpenTPW.ModKit**.
Since .NET 7+, this package **throws `PlatformNotSupportedException` off Windows**.
→ Migrate to `SixLabors.ImageSharp` (already in the solution).
→ ticket [T-004](tickets/T-004-system-drawing-modkit.md).

### 4. Windows default game path (minor)
`Settings.Designer.cs`: `C:\Program Files (x86)\Bullfrog\Theme Park World`.
Just a setting (`GamePath`) to point elsewhere — non-blocking but worth documenting.
→ ticket [T-006](tickets/T-006-gamepath-config.md).

## Summary table

| Component | Linux? | Action |
|-----------|:------:|--------|
| .NET 8 runtime | ✅ | Install the .NET 8 SDK (see [Linux.md](Linux.md)). |
| Rendering (Veldrid/SDL2) | ✅ | Pick Vulkan/OpenGL backend; install SDL2/Vulkan libs. |
| Hardcoded `\` paths | ❌ | Patch `GameDir.cs` + `BaseFileSystem.cs`. |
| Audio (NAudio) | ❌ | Replace with a portable library. |
| ModKit (System.Drawing) | ❌ | Migrate to ImageSharp. |
| Asset filename case | ⚠️ | Normalize filenames (see [03](03-disc-compatibility.md)). |

## Verdict

- **OpenTPW can target Linux** without a heavy rewrite: the engine is already
  cross-platform. Three locks need fixing (paths, NAudio, System.Drawing) — these are
  targeted fixes, not a redesign.
- **The original game (`TP.EXE`)** does not need to run on Linux: OpenTPW replaces the
  engine. The CD's **SafeDisc** protection is therefore irrelevant to OpenTPW.
- **Verified on this machine**: `.NET 8.0.422` installed locally; `dotnet build`
  succeeds; `dotnet test` fails (7/7) due to the locks above + missing game install.

> Note: **Java 21 is present** on this machine (useful for Ghidra), but that is
> unrelated to running OpenTPW (which is .NET, not Java).
