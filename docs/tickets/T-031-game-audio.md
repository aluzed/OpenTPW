# T-031 — Game audio: play sound in-engine (lobby music)

- **Priority**: 🟡 Feature
- **Type**: Engine wiring / audio
- **Status**: ⚠️ Partial — looping background music wired; SFX + mixing/volume UI remain.
- **Related**: [T-003](T-003-naudio-not-portable.md) (portable audio stack), [01-project-status](../01-project-status.md).

## Problem

The game played **no sound**. The pieces existed but weren't connected: `OpenTPW.Files` decodes the
audio containers (`SoundFile`/`MP2File`/`SdtArchive` — `.sdt` archives of MPEG `.mp2` tracks), and
the **ModKit** has an OpenAL previewer (`AudioPlayer`, NLayer + Silk.NET.OpenAL — T-003), but the
**game** (`OpenTPW`) had no audio device init and no playback — the "MusicVolume/SoundEffectsVolume"
strings were unwired labels.

## Done

- ✅ **Game audio service** `source/OpenTPW/Audio/Audio.cs`: lazy OpenAL init (graceful when there's
  no device — headless/CI just disables playback), `PlayMusic(mp2Bytes, loop)` (NLayer-decode → one
  OpenAL buffer, `AL_LOOPING`), `StopMusic`, and a `MusicVolume` gain. Audio deps added to
  `OpenTPW.csproj` (NLayer, Silk.NET.OpenAL, .Soft.Native bundles libopenal).
- ✅ **Lobby music**: `Game.Run` opens `data/global/sound/MusicHD.sdt`, takes the calm track
  (`level4c.mp2`) and plays it looping. Verified: "OpenAL audio initialized.", track decoded, no
  errors — music plays in the lobby.

## To do

1. ☐ **SFX** — UI click sounds (`UIHD`/`sfUiHD.sdt`) on button interactions, ambience
   (`AmbientHD.sdt`). Needs a multi-source/voice path (the current service has one music source) and
   button click events wired.
2. ☐ **Volume / mixing** — connect the settings (`MusicVolume`, `SoundEffectsVolume`) UI to the
   service; per-category gain.
3. ☐ **Track selection** — pick music by context/level instead of hardcoding `level4c`
   (`MusicHD.sdt` ships `level4c`/`level4w` = calm/wild). The frontend may want a dedicated theme.
4. ☐ Consider sharing the NLayer/OpenAL decode with the ModKit `AudioPlayer` to avoid duplication.

## Affected files

`source/OpenTPW/Audio/Audio.cs` (new), `source/OpenTPW/Client/Game.cs` (lobby music),
`source/OpenTPW/OpenTPW.csproj` (audio packages).
