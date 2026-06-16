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
  no device — headless/CI just disables playback), `PlayMusic(mp2Bytes, loop)` (decode → one OpenAL
  buffer, `AL_LOOPING`), `StopMusic`, and a `MusicVolume` gain. OpenAL via Silk.NET (+.Soft.Native).
- ✅ **Lobby music**: `Game.Run` opens `data/global/sound/MusicHD.sdt`, takes the calm track
  (`level4c.mp2`) and plays it looping.
- ✅ **Decoder fix — NLayer → minimp3.** NLayer mis-decoded the MPEG-2 Layer II music (22 kHz):
  it dropped ~42 of 347 frames (~12%), producing a 15.9 s, time-desynced, glitchy stream
  (correlation 0.078 vs a correct ffmpeg decode) — audible as "missing instruments / weird". Switched
  to the public-domain **minimp3** decoder, bundled as a tiny native lib (`Audio/native/tpwmp3.c` +
  `minimp3*.h`, built by `build.sh` → `linux-x64/libtpwmp3.so`, P/Invoked as `tpwmp3`). minimp3's
  decode matches ffmpeg bit-for-bit (correlation **1.000**, full 18.1 s). The SDT payload offset was
  also corrected (`headerSize`, not the field sum — see commit) so the stream starts on an MP2 frame.
  Note: the source remains 22 kHz / 112 kb/s MP2 (the original asset); minimp3 just decodes it
  correctly. The ModKit previewer still uses NLayer and has the same MPEG-2 bug (item 4).

## To do

1. ☐ **SFX** — UI click sounds (`UIHD`/`sfUiHD.sdt`) on button interactions, ambience
   (`AmbientHD.sdt`). Needs a multi-source/voice path (the current service has one music source) and
   button click events wired.
2. ☐ **Volume / mixing** — connect the settings (`MusicVolume`, `SoundEffectsVolume`) UI to the
   service; per-category gain.
3. ☐ **Track selection** — pick music by context/level instead of hardcoding `level4c`
   (`MusicHD.sdt` ships `level4c`/`level4w` = calm/wild). The frontend may want a dedicated theme.
4. ☐ Point the ModKit `AudioPlayer` at the same minimp3 decoder (it still uses NLayer → same
   MPEG-2 Layer II bug when previewing 22 kHz sounds), and share one decode path.
5. ☐ Build `libtpwmp3` for Windows (`.dll`) and macOS (`.dylib`) — currently only `linux-x64` is
   committed; other platforms fall back to no audio (graceful) until built (`Audio/native/build.sh`).

## Affected files

`source/OpenTPW/Audio/Audio.cs` (new), `source/OpenTPW/Client/Game.cs` (lobby music),
`source/OpenTPW/OpenTPW.csproj` (audio packages).
