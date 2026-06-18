# T-039 — Peep needs & staff depth

- **Priority**: 🟡 Feature
- **Type**: Engine
- **Status**: ☐ To do
- **Related**: [T-034](T-034-peeps.md) (needs, economy, staff — core done).

## Context

Peeps already have happiness / energy / hunger, choose rides by excitement, queue, ride, eat at shops,
turn over, and the park has entertainers, handymen and guards. This ticket deepens the simulation so
each staff role and need has a fuller loop.

## Remaining work

1. **Thirst + drink stalls** (and toilets/bathroom need) — more shop types driving spending, mirroring
   the hunger/food loop.
2. **Vandalism** — very unhappy peeps vandalise (break a fence / scatter litter); **guards** catch or
   deter vandals (today guards only suppress littering via proximity — give them a real target).
3. **Peep thoughts / ride ratings** — track why peeps are (un)happy; feed a per-ride popularity rating.
4. **Staff behaviour** — patrol zones / assignments, mechanics repairing broken-down rides, balanced
   wages vs income.
5. **Balance pass** — tune drop rates, prices, wages and durations for a stable economy.

## Acceptance criteria

- Peeps express thirst (visit drink stalls); unhappy peeps vandalise and guards measurably reduce it;
  the economy stays balanced over a long run.

## Affected files

`source/OpenTPW/World/Peep.cs`, `Staff.cs`, `Shop.cs`, `ParkFinances.cs`, `Litter.cs`.
