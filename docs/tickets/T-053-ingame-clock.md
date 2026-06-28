# T-053 — In-game clock / time progression

- **Priority**: 🟡 Feature (foundation)
- **Type**: Engine
- **Status**: ✅ Done — `GameClock` (day/month/year at `SecondsPerMonth` 8 s ÷ `DaysPerMonth` 30) is the
  single time source; `ParkFinances.SettleMonth` runs on `OnNewMonth`, the HUD shows the in-game date, and
  `OnNewDay`/`OnNewMonth` events are ready for challenges/seasons. Unit-tested + verified in-game.
- **Blocks**: [T-054](T-054-challenge-system.md) challenges, [T-055](T-055-golden-ticket-goals.md)
  golden-ticket goals, [T-056](T-056-weather-seasons.md) seasons. **Related**: [T-042](T-042-economy-controls-loans.md)
  (already simulates 8-second "months" for loans).

## Context

OpenTPW has **no in-game calendar**. `VM/Handlers/Time.cs` reads the **wall clock** (YEAR/MONTH/DAY/HOUR/…),
and `ParkFinances` advances loans on a private 8-second "month" timer. Almost every progression feature
(challenges with `TargetTime` in days, golden-ticket `RecentVisitorMonths`, weather `DaysBetweenChanges`)
needs a single authoritative **in-game day/month clock** to key off. This ticket adds it and routes the
existing month logic through it.

## What we know (RE recon)

- The binary tracks game time in modules `SAD_VANILLA_TIME` / `SAD_CLOCK` (the save system, T-059).
- `ParkFinances.MonthSeconds = 8f` is the de-facto month length already in use — generalise it.
- Level `.sam` balance is expressed in **days** (challenge `TargetTime`, `Weather.DaysBetweenChanges 7`,
  `Weather.DaysOfWarning 4`, `GoldenTicketLocal.RecentVisitorMonths 6`).

## Scope

1. A `GameClock` (static, like `ParkFinances.Current`): real-seconds → in-game **days / months / years** at
   a configurable scale (start from the existing 8 s/month; pick a days-per-month so a day is a few seconds).
   Expose `Day`, `Month`, `Year`, `TotalDays`, and a per-day / per-month tick event.
2. Route `ParkFinances`'s monthly settle + the finance-history sample through `GameClock` (single source of
   truth) instead of its own timer.
3. Expose it to the VM time opcodes (optional: the in-game clock instead of wall time) and the HUD (show the
   in-game date).
4. Pure + unit-tested (seconds→date conversion, month/day boundary events).

## Acceptance criteria

- A single in-game clock advances days/months/years; loans + finance history key off it; the HUD shows the
  in-game date; challenges/seasons can subscribe to day/month ticks.

## Affected files (anticipated)

`source/OpenTPW/World/GameClock.cs` (new), `ParkFinances.cs` (use the clock), `VM/Handlers/Time.cs`,
`UI/Widgets/HudPanel.cs`, `source/OpenTPW.Tests/GameClockTests.cs` (new).
