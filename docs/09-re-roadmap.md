# 09 — RE roadmap: the unimplemented meta-game subsystems

A reconnaissance pass (2026-06) mapped what's authored in the install + named in the **no-CD binary**
(`/var/tmp/nocd/tp.exe`, which retains debug strings + C++ RTTI symbols + `.sam` balance-field labels) but
**not yet implemented** in OpenTPW. This is the entry-point map for tickets **T-053–T-060**. Docs 05–08 cover
the VM, renderer, animation/node rig and file formats; everything here is *uncovered* by those.

> **Leverage note.** The binary names a generic **`BalanceLoader`** that reads the `.sam` key→field tables.
> Reversing that one loader yields the exact struct field maps for the challenge / decision / quality / score /
> advisor balance blocks at once — do it first if implementing several of these.

## Progression cluster (highest value) — T-053/054/055

The objectives layer is data-driven and the tracking infra (money, visitors, ride usage, shop sales) already
exists. Persisted together in the save module **`SAD_ADV_SCORING`**.

- **Challenge engine** (T-054). Data `data/Challenges.sam` (35 challenges: `Type`/`FollowupType`/`TargetTime`/
  `TargetVal`/`TargetObj`/`Prize`/`CheckAtEndOnly`/`Independent`; globals `ChallengesInThisLevel`,
  `DaysUntilFirstChallenge`, `DaysAfter{Completed,Declined}Challenge`). Binary: loader log `"Loading
  challenges.sam went horribly wrong! Shout at Bjarne!"`; state machine `GetCurrentChallenge()` /
  `EndCurrentChallenge()`, fields `mCurrentChallenge`/`mChallengeOn`/`mNextChallengeEventTime`/
  `mChallenge{Declined,Lost,Offered}`; events `"Challenge %d is being offered!"`, `"Won/Lost the challenge!!!"`.
- **Golden ticket** (T-055). Data `levels/<lvl>/Standard.sam` `GoldenTicketLocal.{Visitors 100, PeopleInPark
  200, Happiness 75, AtLeastThisManyHappyPeople 150, ProfitYear 15000, RecentVisitors 350,
  RecentVisitorMonths 6}` (+ Global/Secret tiers). Binary: `CGoldenTicketControl::TellTheWorld`
  (Local/Global/Secret; sub-states ticket→key→park); hints `GoldTicketNearTo{XPeeps,XHappiness,XProfit}`,
  `"You have %d total golden tickets"`. Effect `P_EFFECT_GoldenTicket`.
- **In-game clock** (T-053, foundation). All the above are in **days**; the binary tracks time in
  `SAD_VANILLA_TIME` / `SAD_CLOCK`. `ParkFinances` already runs an 8 s "month" — generalise into one clock.
- **Advisor** (already built, T-046) delivers these — `CAdvisor::ReceiveMessage`, shared `Score*` fields
  (`MinScoreForConsideration`, `ScorePer{WornRide,BrokenRide,LitterPoint}`, `ScorePerUnpatrolledPct`).

## Save / load — T-059

`.TPWS` container fully RE'd + writable (T-017). Payload = **17 self-describing `SAD_*` modules**, each with a
saved-vs-loaded byte-count check: `SAD_ADV_SCORING, SAD_CHEAT, SAD_SOUND, SAD_ADV, SAD_COASTERS, SAD_CAMERA,
SAD_RSSE, SAD_FLYERS, SAD_TRACK, SAD_RIDESYS, SAD_GAMESYS, SAD_VANILLA_TIME, SAD_CLOCK, SAD_MESSAGE,
SAD_PARTICLES, SAD_SPRITE_SCRIPTS, SAD_AI`. Orchestrator `"Loaded savegame: %s"`; per-module `"UI: loaded %d
bytes"`. **Route A** (native save) needs no RE; **Route B** (original-save compat) reverses the 17 modules and
needs a real `.TPWS` sample (none in this install).

## Peep AI decision scorer — T-060

Ride-choice scorer logs `"Calculating option score for object with no BOQ"`; weight vector `DecisionVariable1..8`,
`DecisionVar{Dist,Queue,Excitement,Thirst}Weight` (`.sam`, via `BalanceLoader`). Needs weights
`ScorePer{Thirsty,Hungry,Waiting}Person`; happiness model `HappinessRecuperationRate`/`HappinessEffectOnCell`/
Small-Medium-Big `HappinessChange`. Nav: `"A peep became stuck and renavigated"`, `Peep %d collided with Peep %d`.

## Weather / seasons / lighting — T-056

`levels/<lvl>/Standard.sam` `Seasons[0..3].{AvgWeatherQuality,NormalTolerance,ChanceForExceptionalWeather}`,
`Weather.{DaysBetweenChanges 7, DaysOfWarning 4}`. Binary: `QualityFor{Rain,Snow}{Low,High}`, `MaxRaindrops`,
`mWeather`, `mRainSoundHandle`; assets `Data/Generic/Weather/{lightning,raindrop,snow}.tga`. Day/night is **not**
a named cycle — it's `FogColour`/`"Fog algorithm %x"` + `AmbientLightLevel`/`DirectionalLightLevel`.

## Content / UX — T-057/058

- **Minimap** (T-057): `data/2dmap/` 6 category sprites (`{e,g,l,r,s,v}sprite.tga`) + `qickload.txt` index.
- **Sideshows** (T-058): per-level `sideshow/*.wad` (jungle: arc2x3/hyenas/junspray/puzzle/squark) — normal
  ride-style WADs; `SideshowTakings` / `EventSideshowWin` VM stubs already exist.

## Documented-but-low-value (preservation only)

- **Online** (`City`/`Chat`/`Upload`/`Vote`/`News`/`Mail`/`Auth` C++ classes, EA `wea*.dll`, servers
  `daphne.eagames.co.uk` — dead service) and **postcard** (`CTagSystem`, `data/postcard/*.tga`). Easy to map
  (named symbols), low gameplay value. **Init splash screens** (`data/Init/` 80 TGA), **hoardings**
  (`Hoardings.sam` click zones), **`sound.sam` audio detail tiers** — cosmetic/secondary.
