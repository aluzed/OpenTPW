# T-051 — Audio polish (ambient loops + settings-screen volume sliders)

- **Priority**: 🟡 Feature
- **Type**: Audio / UI
- **Status**: ☐ To do
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

## Affected files

`source/OpenTPW/Audio/Audio.cs`, a settings/options UI panel, the settings-file read/write.
