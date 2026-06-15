# T-003 — NAudio (audio) not portable off Windows

- **Priority**: 🟠 Medium
- **Type**: Portability
- **Status**: ✅ **Done.** NAudio is removed entirely; audio is now decoded and played
  cross-platform. ⚠️ Note: actual sound output could not be verified in this headless
  environment (no audio device) — the code path builds, ships the native lib for every
  RID, and degrades gracefully when no device is present.

## Context

`NAudio 2.2.1` is referenced by **OpenTPW** and **OpenTPW.ModKit**. NAudio relies on
WinMM/WASAPI → **no audio playback on Linux/macOS** (throws at runtime).

Usage found: `source/OpenTPW.ModKit/Editor/Viewers/SoundViewer.cs`
(`new WaveOutEvent()`, `Init`, `Play`).

> The build still compiles (the package exists); the crash happens **at runtime**.

## Fix applied

- New `OpenTPW.ModKit/Editor/AudioPlayer.cs`: decodes MPEG (.mp2) to 16-bit PCM with
  **NLayer** and plays it through **OpenAL** (`Silk.NET.OpenAL`). Lazy device/context
  init; logs and disables playback when no device is available (headless / CI).
- `SoundViewer.cs`: dropped the `#if WINDOWS` guard; calls `AudioPlayer.Play(buffer)`
  on every platform.
- Removed the `NAudio` package reference from `OpenTPW.ModKit.csproj` and the unused one
  from `OpenTPW.csproj`. Added `NLayer`, `Silk.NET.OpenAL`,
  `Silk.NET.OpenAL.Soft.Native` (bundles `libopenal` for all RIDs — no system package).

## Acceptance criteria

- ✅ No NAudio dependency anywhere; build green on Linux.
- ✅ Native `libopenal` shipped to `runtimes/<rid>/native/` in the executable output.
- ⚠️ End-to-end sound output not verifiable here (headless, no audio device); decode +
  output paths compile and fail safe.

## Follow-up

- Promote `AudioPlayer` to a shared service when the game itself needs audio.
- Track/dispose OpenAL sources for stop/seek and to avoid buffer churn on repeat plays.
