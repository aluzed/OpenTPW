# T-039 — Peep needs & staff depth

- **Priority**: 🟡 Feature
- **Type**: Engine
- **Status**: ⚠️ Core done — the three acceptance criteria are met (thirst + drink stalls, vandalism that
  guards measurably reduce, a balanced long-run economy). Thoughts/ratings, toilets and ride
  breakdown/repair remain (below).
- **Related**: [T-034](T-034-peeps.md) (needs, economy, staff — core done).

## Context

Peeps already have happiness / energy / hunger, choose rides by excitement, queue, ride, eat at shops,
turn over, and the park has entertainers, handymen and guards. This ticket deepens the simulation so
each staff role and need has a fuller loop.

## Done

1. **Thirst + drink stalls** — peeps now build `thirst` (a bit faster than hunger) and detour to the
   nearest **drink** stall when parched, satisfying it (more park income). `Shop` gained a `ShopKind`
   (Food/Drink, blue vs green billboard) + `Shop.Nearest(kind,…)`; the build catalog/autoplace gained a
   "drink" stall; a peep picks whichever need (food vs drink) is more urgent relative to its threshold.
2. **Vandalism + guards** — an unhappy peep (happiness ≤ a vandalism threshold, above the give-up
   threshold so there's a window) periodically **vandalises** (scatters litter) *unless a guard is near*,
   which **deters** it. Both outcomes are tallied park-wide (`Peep.VandalismActs` / `VandalismDeterred`).
   Crucially, **guards now patrol toward trouble** (`Staff.DoGuard` heads for the nearest unhappy peep)
   so their deterrence actually lands where vandalism happens — a lone standing guard almost never
   coincides with a vandal (a point guard covers ~0.3% of the park). Staff patrol radius widened so they
   cover the park (guards reach queue backs, handymen reach far litter).
4. **Staff behaviour (partial)** — guards patrol toward unhappy peeps (above); other roles unchanged.
5. **Balance pass** — tuned thirst/hunger rates, the drink price, the vandalism window/rate and the
   patrol radius for a stable long run.

Verified via `OPENTPW_AUTOPLACE` + `OPENTPW_ECON_DEBUG` (≈150 s): money held steady and net-positive
(≈10.0k → 10.5k), drink+food revenue grew, and with one patrolling guard **vandalism=41 / deterred=21**
(≈34 % of would-be acts stopped, total acts also down vs ~70 without a patrolling guard), no exceptions.
Build clean, 74/0 tests.

## Remaining

- **Toilets / bathroom need** — a third need + stall type (symmetric with the thirst/drink loop).
- **Peep thoughts / ride ratings** (item 3) — surface *why* a peep is (un)happy and a per-ride
  popularity rating.
- **Ride breakdown + mechanics** (rest of item 4) — rides don't break down yet, so there's nothing for a
  mechanic to repair; needs a reliability/breakdown model first.
- **Build UI note**: the dev number-key catalog only addresses items 1–9; with the added "drink" stall
  the 10th entry (researcher) is no longer keyboard-selectable (autoplace still places it). A real build
  UI is the T-038 umbrella.

## Acceptance criteria

- Peeps express thirst (visit drink stalls) ✅; unhappy peeps vandalise and guards measurably reduce it
  ✅ (deterred 21 of 62 would-be acts in the verified run); the economy stays balanced over a long run ✅.

## Affected files

`source/OpenTPW/World/Peep.cs`, `Staff.cs`, `Shop.cs`, `Level.cs`.
