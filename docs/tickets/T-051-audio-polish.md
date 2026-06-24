# T-051 — Audio polish (ambient loops + settings-screen volume sliders)

- **Priority**: 🟡 Feature
- **Type**: Audio / UI
- **Status**: ✅ Core done
- **Parent**: [T-031](T-031-game-audio.md) (game audio — this is the remaining tail).

## Context

Game audio is **mostly done**: lobby music (minimp3), UI click SFX, music-volume keys, cross-platform
playback. Two pieces remain from T-031.

## Scope

1. **Ambient loops**: per-area / weather ambience playing under the music (the `AMB` sound categories
   exist in the BANK catalogs; pick + loop the right ambience for the level).
2. **Settings-screen volume UI**: expose the existing `Audio.MusicVolume` / `Audio.SfxVolume` (and a
   speech volume — see `UIStrings.SpeechVolume`) as on-screen sliders, persisted to the settings file,
   rather than keys only.

## Acceptance criteria

- An ambient loop plays in-level; music/SFX/speech volumes are settable from a UI panel and persist.

## What was done

1. **Persistent settings** — new `GameSettings` (`source/OpenTPW/Client/GameSettings.cs`): a small JSON
   store (`./.opentpw/settings.json`) for the three volume buses, fault-tolerant (a missing / corrupt /
   partial file falls back to defaults, never throws) and clamped to `[0,1]`. Unit-tested
   (`GameSettingsTests`: round-trip, clamping, garbage/null/partial → defaults, file save/load).
2. **Audio buses** — `Audio` gains a **speech** bus (`SpeechVolume` + `PlaySpeech`, now used by the
   advisor lines so voice balances independently of SFX) and a second **looping ambient source**
   (`PlayAmbient`/`StopAmbient`) that rides the SFX bus under the music. The three volumes are adopted
   from `GameSettings` on init.
3. **Ambient bed** — `Game` loads `data/global/sound/AmbientHD.sdt` and loops a track under the music
   (optional: absent archive just leaves the music playing).
4. **Options UI** — new `OptionsPanel` (F10): a centred overlay with three **draggable volume sliders**
   (music/SFX/speech) that drive the live `Audio` buses and persist on release. The `-`/`+` music keys
   also persist now.

Verifying the ambient bed / sliders against real audio needs a mounted install (the bundled game is a
SafeDisc disc image, not an extracted tree); the logic is unit-tested and degrades gracefully without a
device or game data, consistent with the rest of the audio layer.

## Remaining (nice-to-have)

- Per-area / weather-driven ambience selection (currently one level ambience loop).
- A speech-volume key (only music has `-`/`+`; speech/SFX are slider-only).

## Affected files

`source/OpenTPW/Client/GameSettings.cs` (new), `source/OpenTPW/Audio/Audio.cs`,
`source/OpenTPW/UI/Widgets/OptionsPanel.cs` (new), `source/OpenTPW/UI/Widgets/HudPanel.cs`,
`source/OpenTPW/Client/Game.cs`, `source/OpenTPW/Client/Renderer.cs`, `source/OpenTPW/World/Level.cs`,
`source/OpenTPW/World/Advisor.cs`, `source/OpenTPW.Tests/GameSettingsTests.cs` (new).
