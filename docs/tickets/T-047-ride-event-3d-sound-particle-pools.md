# T-047 — Ride EVENT 3D sound positioning + particle effect-pool → `.PLB` mapping

- **Priority**: 🟡 Feature
- **Type**: Engine / reverse engineering
- **Status**: ☐ To do
- **Parent**: [T-037](T-037-ride-cycle-sound.md) (EVENT type switch decoded — this is the residue).
- **Related**: [T-019](T-019-plb-parameter-fields.md), [T-032](T-032-ride-engine.md).

## Context

T-037 decoded the whole `EVENT` type switch (`FUN_005573d0`): types 1-2 = positioned sounds (wired to
the global sound registry), types 3-10 = particle effects each selecting one of **7 effect pools**
(`DAT_00803a20..3c`), rendered through the decoded `.PLB` proxy. Two pieces remain.

## Scope

1. **Per-type effect-pool → exact `.PLB` library mapping.** The 7 pools are distinct effect sets; we
   currently index them all into `Tp2.plb` (correct for common type-3 codes like Destroy2-4/Fire, but
   out-of-range codes — e.g. 219/220 — belong to another pool/library not yet decoded). Trace what each
   `DAT_00803a2x` pool points at and load the right library per type.
2. **3D positioning.** EVENT sounds + particles currently fire at the ride origin; `FUN_00556b90`
   computes a position from the `target` node in the ride's model/node graph. Decode it and place effects
   at the real node (the same node-geometry gap as walk/head — see [T-048](T-048-ride-node-geometry-movement.md)).
3. **`COAST`/`BUMP`/`ADDOBJ` sound residue.** The car-object opcodes mostly drive audio via `EVENT`, but
   any direct sound triggers in their subcommands (e.g. the `FUN_00473270`/`004732a0` calls in BUMP) can
   be wired once their semantics are confirmed.

## Acceptance criteria

- Out-of-range EVENT particle codes resolve to a real effect (no "unresolved"); effects/sounds play at
  the correct node position rather than the ride centre.

## Affected files

`source/OpenTPW/World/Rides/RideEngine.cs`, `source/OpenTPW.Files/Formats/Particle/ParticleLibraryFile.cs`.
