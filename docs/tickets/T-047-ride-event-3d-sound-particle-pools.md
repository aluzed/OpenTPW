# T-047 — Ride EVENT 3D sound positioning + particle effect-pool → `.PLB` mapping

- **Priority**: 🟡 Feature
- **Type**: Engine / reverse engineering
- **Status**: ⚠️ RE advanced (premise corrected) — the "7 particle pools / per-pool library" model is
  **wrong**: there's a single particle library and the pools are **sound categories** in a unified
  effect manager. EVENT effects are node-positioned (the T-048 path). The one actionable code change
  (EVENT type routing) needs in-game verification, currently **blocked by the broken display/renderer**.
- **Parent**: [T-037](T-037-ride-cycle-sound.md) (EVENT type switch decoded — this is the residue).
- **Related**: [T-019](T-019-plb-parameter-fields.md), [T-032](T-032-ride-engine.md), [T-048](T-048-ride-node-geometry-movement.md).

## Context

T-037 decoded the whole `EVENT` type switch (`FUN_005573d0`): types 1-2 = positioned sounds (wired to
the global sound registry), types 3-10 = particle effects each selecting one of **7 effect pools**
(`DAT_00803a20..3c`), rendered through the decoded `.PLB` proxy. Two pieces remain.

## Done (Ghidra — the model corrected)

- **There is exactly one particle library.** `data/Particle/Tp2.plb` (header: 105 records, stride 320) +
  `par_lib.h` define the particle effects **`P_EFFECT_*` = 0..100** (plus a tiny `E_EFFECT_*` force set
  0..5). So a code like 76/77/78 = `Destroy2/3/4`, 62 = `LaserFWexplode`, 8 = `FirePuff`, 11 = `Fire`;
  **219/220 are far out of range** → sentinels/invalid, NOT alternate-library effects. T-037's "skip
  unresolved" is therefore correct, and the "per-pool library mapping" premise is dropped.
- **The 7 EVENT "pools" are SOUND CATEGORIES, not particle sets.** RE'd from the registrars `FUN_0051eae0`
  / `FUN_0051ec50` (both call `FUN_0051b530("cat_*", …)`): `DAT_00803a20`=`cat_ambient`, `a24`=`cat_kids`,
  `a28`=`cat_rides`, `a2c`=`cat_ui`, `a30`=`cat_staff`, `a34`=`cat_speech`, and a second group `a38`=ambient
  / `a3c`=rides. Mapping these onto the EVENT dispatch (`FUN_005573d0`, T-037): **type 5→rides, 6→kids,
  7→staff, 8→ambient, 9→ui** — i.e. EVENT **types 5-9 are positioned sounds by category**, dispatched
  through the **unified effect manager `DAT_00802bcc`** (the same manager registers categories at vtable+4
  and spawns at +8; it handles *both* positional sound and particles — it was mislabeled "particle
  manager" in T-019).
- **Effects are node-positioned.** `op_93`/`op_105` (REPAIREFFECT/SPARK) resolve a position from a ride
  **node** via `FUN_00556a80`/`FUN_00556b90` (error "Bad particle node in %s") — exactly the node graph
  decoded in [T-048](T-048-ride-node-geometry-movement.md). EVENT's `target` operand is the node id.

## Open question (needs in-game verification — blocked by the display)

- **Are EVENT types 3-4 particles and 5-9 sounds?** The pools are unambiguously sound categories (so 5-9
  look like sounds), yet `op_93` (a particle) also routes through `FUN_0051bfc0` — so the unified manager
  dispatches sound-vs-particle by the handle type. My T-037 code routes **all** of EVENT 3-10 to
  `SpawnParticleEffect`, which is likely **wrong for 5-9** (should play a category sound). I have **not**
  changed it: the renderer is down this session so I can't confirm which types are which, and mis-routing
  working audio is worse than the current state. Verify in-game, then split the EVENT handler:
  types 3-4 → particle (`Tp2.plb`), types 5-9 → category sound, type 10 → custom.

## Remaining

1. Verify + split EVENT type routing (sound categories vs particle) per the open question above.
2. Real 3D positioning: feed the node world position (needs T-048's bone-transform decode) into the
   effect/sound spawn instead of the ride origin.
3. **`COAST`/`BUMP`/`ADDOBJ` sound residue** (their direct sound triggers, e.g. `FUN_00473270` in BUMP).

## Acceptance criteria

- Out-of-range EVENT particle codes resolve to a real effect (no "unresolved"); effects/sounds play at
  the correct node position rather than the ride centre.

## Affected files

`source/OpenTPW/World/Rides/RideEngine.cs`, `source/OpenTPW.Files/Formats/Particle/ParticleLibraryFile.cs`.
