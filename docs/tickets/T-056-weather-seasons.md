# T-056 — Weather + seasons (+ day/night lighting)

- **Priority**: 🟢 Low (atmosphere)
- **Type**: Engine / rendering
- **Status**: ⚠️ Core done (sim + overlay + HUD; peep-behaviour nudge + real fog/lighting ramp remain)
- **Needs**: [T-053](T-053-ingame-clock.md) (day/season clock).

## Done

- **Parse** (`WeatherConfig.ParseLocal`): `Seasons[0..3].{AvgWeatherQuality,NormalTolerance,ChanceForExceptionalWeather}`,
  `Weather.{DaysBetweenChanges,DaysOfWarning,SpeedOfChange}`, `WeatherEffects.QualityFor{Rain,Snow,Lightning}{Low,High}`
  from the level `Standard.sam`. A `-1` bound disables that effect (jungle never snows); a missing block falls
  back to always-clear.
- **Pure maths** (`Weather`, unit-tested): `SeasonForMonth` (12 months → 4 seasons), `Classify` (quality 0..100,
  lower = worse → clear/rain/snow, snow wins overlaps, lightning rides on top), `RollQuality` (avg ± tolerance,
  doubled on an exceptional roll, clamped).
- **Sim** (`WeatherSim`): rolls a fresh quality for the current season every `DaysBetweenChanges` on
  `GameClock.OnNewDay`; `Current` exposes `State`/`Quality`/`Season` for the overlay + HUD.
- **Visuals** (`WeatherOverlay : HudPanel`): full-screen colour wash + animated falling precipitation
  (rain streaks / drifting snow, a deterministic per-index stream) + a brief lightning flash. `ParkStatsPanel`
  shows a `SPRING STORM (q5)`-style season/sky line.
- **Wiring**: `Level` builds the sim from the level settings + ticks it on the daily clock. `OPENTPW_WEATHER`
  (`rain`/`storm`/`snow`/`blizzard`) pins a state so the overlay can be demoed on a level that rarely produces it.
- 9 MSTest unit tests (`WeatherSimTests`); verified in-game (storm = blue wash + streaks + HUD line).

## Remaining (polish)

- Feed weather into peep behaviour (shelter/leave in bad weather).
- A real fog/lighting ramp (`FogColour`/`AmbientLightLevel`/`DirectionalLightLevel`) — currently a flat tint,
  not the day-cycle lighting; the renderer is unlit.
- Ambient rain sound (`mRainSoundHandle`) + the snow path on a level that actually enables it.

## Context

TPW runs a weather sim (quality-gated rain/snow) over four seasons, plus fog/lighting that stand in for the
day cycle. OpenTPW has a static sky (`Sky.cs`, `Sun.cs`) and no weather. This is mostly atmosphere but the
data is fully authored.

## What we know (RE recon)

- **Data: `levels/<lvl>/Standard.sam`** — `Seasons[0..3].AvgWeatherQuality` (75/80/90/50), `.NormalTolerance`,
  `.ChanceForExceptionalWeather`; `Weather.DaysBetweenChanges 7`, `Weather.DaysOfWarning 4`.
- **Binary (Ghidra):** weather sim fields `AvgWeatherQuality`, `QualityForRainLow/High`, `QualityForSnowLow/High`,
  `MaxRaindrops`, `mWeather`, `mRainSoundHandle`; assets `Data/Generic/Weather/{lightning,raindrop,snow}.tga`.
  Day/night is **not** a named cycle — it's driven by `FogColour`, `"Fog algorithm %x"`, `AmbientLightLevel`,
  `DirectionalLightLevel` (a lighting/fog ramp, not a literal clock).
- The renderer is unlit; a first pass can do a screen-space rain/snow overlay + a tint, deferring true lighting.

## Scope

1. Parse `Seasons[*]` + `Weather.*`; a `WeatherSim` that rolls a quality each `DaysBetweenChanges` (T-053
   clock) within the season's tolerance, mapping quality→clear/rain/snow via the `QualityForRain/Snow`
   thresholds; `DaysOfWarning` lead-in.
2. Visuals: a camera-facing rain/snow particle overlay (reuse the billboard/particle proxy path) + a sky/fog
   tint; ambient sound (`mRainSoundHandle`).
3. (Optional) feed weather into peep behaviour (shelter/leave in bad weather — peeps already have moods).
4. Unit-test the pure quality→weather-state mapping + season rollover.

## Acceptance criteria

- Weather changes over seasons per the `.sam` data with a visible rain/snow + tint; optionally nudges peeps.

## Affected files (anticipated)

`source/OpenTPW/World/WeatherSim.cs` (new), `Sky.cs`/`Sun.cs`, a weather overlay entity,
`source/OpenTPW.Tests/WeatherSimTests.cs` (new).
