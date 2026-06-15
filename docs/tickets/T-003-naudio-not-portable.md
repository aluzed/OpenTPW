# T-003 — NAudio (audio) not portable off Windows

- **Priority**: 🟠 Medium
- **Type**: Portability
- **Status**: ⚠️ Partially addressed — the NAudio package reference is now Windows-only
  (`Condition="'$(IsWindows)' == 'true'"`) and `SoundViewer.cs` playback is guarded with
  `#if WINDOWS`, so the Linux build excludes it. **Remaining**: implement an actual
  cross-platform audio output so sound works on Linux (the fix below).

## Context

`NAudio 2.2.1` is referenced by **OpenTPW** and **OpenTPW.ModKit**. NAudio relies on
WinMM/WASAPI → **no audio playback on Linux/macOS** (throws at runtime).

Usage found: `source/OpenTPW.ModKit/Editor/Viewers/SoundViewer.cs`
(`new WaveOutEvent()`, `Init`, `Play`).

> The build still compiles (the package exists); the crash happens **at runtime**.

## Proposed fix

- Introduce an **audio-output abstraction** and implement it with a portable library:
  OpenAL (Silk.NET.OpenAL / OpenTK), SDL2 audio (SDL2 is already in the stack via
  Veldrid), or `ManagedBass`.
- Keep the existing `.SDT`/`.MP2` decoding; replace only the audio **output**.

## Acceptance criteria

- Playing a `.SDT`/`.MP2` sound works on Linux.
- No direct NAudio dependency for cross-platform audio output.

## Affected files

`source/OpenTPW/OpenTPW.csproj`, `source/OpenTPW.ModKit/*.csproj`,
`source/OpenTPW.ModKit/Editor/Viewers/SoundViewer.cs`
