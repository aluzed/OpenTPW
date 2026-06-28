# T-060 — Peep AI ride-choice scorer (authored decision weights)

- **Priority**: 🟡 Feature (simulation fidelity)
- **Type**: Engine / RE
- **Status**: ☐ To do (proposed — RE recon done; **weight vector + scorer identified in the binary**)
- **Related**: [T-034](T-034-peeps.md)/[T-050](T-050-peep-simulation-depth.md) (peep sim — currently uses a
  simpler excitement-weighted choice).

## Context

Peeps currently pick rides by a simple excitement weighting (T-050). The original runs a **weighted-utility
scorer** over several factors (distance, queue, excitement, thirst, …) with authored weights — replacing our
heuristic with it would make crowd flow read like the real game.

## What we know (RE recon)

- **Binary (Ghidra):** the ride-choice scorer logs `"Calculating option score for object with no BOQ"` and
  reads a tunable weight vector: `DecisionVariable1..8`, `DecisionVarDistWeight`, `DecisionVarQueueWeight`,
  `DecisionVarExcitementWeight`, `DecisionVarThirstWeight`. These are `.sam` balance keys (read via the
  generic `BalanceLoader`).
- Supporting peep AI strings: `"A peep became stuck and renavigated"`, `Peep %d collided with Peep %d`,
  needs weights `ScorePerThirsty/Hungry/WaitingPerson`, happiness model
  (`HappinessRecuperationRate`, `HappinessEffectOnCell`, Small/Medium/Big `HappinessChange`).
- Our peeps already expose the needed inputs (position/distance, ride excitement, queue length, thirst/hunger).

## Scope

1. Find + parse the `DecisionVar*` weights from the level/global `.sam` (confirm exact keys + which file).
2. A pure `RideChoiceScorer`: `score(peep, ride) = Σ weight_i · factor_i` (distance, queue wait, excitement,
   thirst, …) matching the RE'd factor set; peeps pick the max-scoring reachable ride.
3. Swap `Peep`'s current excitement-weighted `PickRoute` to use the scorer; keep the avoid-immediate-repeat
   rule.
4. Unit-test the scorer (a closer/shorter-queue/more-exciting ride wins; weights change the ranking).

## Acceptance criteria

- Peeps choose rides via the authored weighted-utility scorer; tuning the weights visibly shifts crowd flow;
  the scorer is unit-tested.

## Affected files (anticipated)

`source/OpenTPW/World/RideChoiceScorer.cs` (new), `Peep.cs` (`PickRoute`), balance-key parsing in `Level`,
`source/OpenTPW.Tests/RideChoiceScorerTests.cs` (new).
