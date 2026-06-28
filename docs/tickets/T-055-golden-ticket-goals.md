# T-055 — Golden-ticket goals / level objectives

- **Priority**: 🟡 Feature
- **Type**: Engine / UI
- **Status**: ⚠️ Core done — `GoldenTicketLocal.*` targets parse from `Standard.sam`; a pure `GoldenTicket`
  evaluator + `GoldenTicketGoals` manager track the live park (visitors / in-park / happiness / happy-people /
  profit-per-year, on `GameClock.OnNewDay`) and award the ticket once all set targets are met; the HUD shows
  `TICKET GOALS n/5` → `GOLDEN TICKET WON!`. Unit-tested, verified in-game. **Remaining (polish)**: the
  `RecentVisitors`-over-months goal (needs per-month visitor history), the exact "happy" threshold (approx 50),
  and the award **particle effect + advisor congratulation** (currently a log + HUD banner); Global/Secret
  tiers aren't in the jungle `.sam`.
- **Needs**: [T-053](T-053-ingame-clock.md) (months for the "recent visitors" window). **Related**:
  [T-054](T-054-challenge-system.md) (shares the `SAD_ADV_SCORING` progression cluster), [T-047](T-047-ride-event-3d-sound-particle-pools.md)
  (particle/award effect).

## Context

The golden ticket is TPW's per-level **win condition**: hit a set of park targets (visitors, happiness,
profit, …) to earn the Local → Global → Secret tickets. The level `.sam` defines all the targets; nothing in
OpenTPW reads them.

## What we know (RE recon — strong)

- **Data: `levels/<lvl>/Standard.sam`** — `GoldenTicketLocal.Visitors 100`, `.PeopleInPark 200`,
  `.Happiness 75`, `.AtLeastThisManyHappyPeople 150`, `.ProfitYear 15000`, `.RecentVisitors 350`,
  `.RecentVisitorMonths 6` (and Global/Secret tiers per the binary).
- **Binary (Ghidra, named C++):** `CGoldenTicketControl::TellTheWorld` with Local/Global/Secret variants and
  sub-states `ticket only → ticket and key → ticket, key and park`; award reasons (`big park`, `water length`,
  `gokart excitement`, `coaster height`); hint strings `GoldTicketNearToXPeeps/XHappiness/XProfit`,
  `"You have %d total golden tickets"`. Persisted in `SAD_ADV_SCORING`. Particle `P_EFFECT_GoldenTicket`.
- The advisor already surfaces near-miss hints (`AdvisorAdvice`, T-046) — wire the `GoldTicketNearTo*` ones.

## Scope

1. Parse the `GoldenTicket{Local,Global,Secret}.*` targets from the level `.sam`.
2. A `GoldenTicketGoals` evaluator: track real park state (`Peep.AverageHappiness`, visitor counts,
   `ParkFinances` profit-per-year over the T-053 clock, recent-visitors window) against each tier's targets;
   fire the award when all are met.
3. On award: the `P_EFFECT_GoldenTicket` particle + an advisor congratulation; feed the `GoldTicketNearTo*`
   hints into `AdvisorAdvice`.
4. UI: a goals panel (each target + met/▢) showing progress toward the ticket.
5. Unit-test the pure target-evaluation logic.

## Acceptance criteria

- The level's golden-ticket targets load, the player sees progress, and meeting all of a tier's targets awards
  the ticket (effect + advisor line).

## Affected files (anticipated)

`source/OpenTPW/World/GoldenTicketGoals.cs` (new), `UI/Widgets/GoalsPanel.cs` (new), hooks in `Level`/
`AdvisorAdvice`, `source/OpenTPW.Tests/GoldenTicketTests.cs` (new).
