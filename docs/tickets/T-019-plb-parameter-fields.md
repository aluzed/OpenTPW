# T-019 — `.PLB` particle parameter fields

- **Priority**: 🟡 Feature
- **Type**: Reverse engineering
- **Status**: ✅ Done — full file layout decoded (item 2) **and** the gameplay-meaningful per-effect
  parameter fields are now **labelled** (item 1). The earlier "needs a dynamic capture" call was wrong: while
  the *emit* goes through a message framework, the particle **spawn** path reads the record with **static**
  offsets. Decoded + exposed typed on `ParticleEffect`, verified against the real `Tp2.plb`.
- **Split from**: [T-008](T-008-unimplemented-formats.md).

## Field labels (Ghidra: `FUN_00521930` SpawnEffectInstance + `FUN_00520560` SpawnParticle)

The spawn copies the effect record (the `short[160]`) into an active emitter, then reads these offsets (byte
offsets into the record / `Parameters` block; the engine reads them as `short`/`int`). Verified against
`Tp2.plb` — the values match each effect's name:

| Offset | Type | Field | Evidence on Tp2.plb |
|--------|------|-------|---------------------|
| `0x0a` | short | **Emission mode** (0 = burst, 1 = continuous) | all sampled effects = 0 |
| `0x40/44/48` | int×3 | **Emission velocity** x/y/z (0,0,0 → inherit emitter velocity) | Smoke y=64 (rises) |
| `0x54` | short | **Colour mode** (0 = fixed, else cycle the ramp) | — |
| `0x5c` | int | **Burst count** (particles released at once) | Sparks=37, ExplodeFirey=62 |
| `0x74` | int | **Particle lifetime** (ticks, jittered ±¼) | Sparks=100, Smoke=800, Repair=2024 |
| `0x80/84/88` | int×3 | **Acceleration / gravity** x/y/z | Smoke x=-1 (drift) |
| `0xa8` | int | **Emission cone / spin angle** (drives a sin/cos lookup) | — |
| `0xac` | int | **Velocity scale** (16.10 fixed-point; `>>10`) | ExplodeFirey=2048 (=2.0) |
| `0xb0` | int | **Child effect** index spawned alongside (≤0 = none) | Smoke→12, Repair→31 |
| `0xc1` | byte | child/disabled flag (skips the spawn when set) | — |

Exposed as typed properties on `ParticleEffect` (`EmissionMode`, `EmissionVelocity`, `BurstCount`,
`LifetimeTicks`, `Acceleration`, `EmissionConeAngle`, `VelocityScale`, `ChildEffect`/`HasChildEffect`,
`CyclesColorRamp`). Other `short[160]` slots are emitter runtime-state (position/age/links) overwritten at
spawn, not authored params, so they stay raw in `Parameters`.

## Context

`ParticleLibraryFile` (`OpenTPW.Files/Formats/Particle/ParticleLibraryFile.cs`) decodes the header,
per-effect names (matching the disc's `par_lib.h` `P_EFFECT_*` list exactly) and the **16-stop RGBA
colour ramp** at the tail of each record.

## Done (Ghidra: loader `FUN_0051f370` in the no-CD `tp.exe`)

- **Corrected the file layout.** The header is **8 bytes** (`count`, `recordSize`), not 16 — the two
  "reserved" words were really effect-0's first parameter words. The parser now matches the engine: records
  start at offset 8, stride `recordSize` (320). Names + ramps land at the same absolute offsets, so they
  are unchanged; but the parameter block and the post-record data are now correctly aligned.
- **Decoded the trailing "shared block" (item 2).** After the effect records the loader reads a **second
  table** (`count2`, `recordSize2`, then `count2 × recordSize2` records — observed **20 × 104** on
  `Tp2.plb`) followed by **two globals**: a particle **density** (the engine clamps it to 10..500;
  observed 33) and a **total-particle** budget (observed 1024). All exposed typed (`SharedRecords`,
  `SharedRecordSize`, `ParticleDensity`, `TotalParticles`); the whole 35 704-byte file is now accounted
  for (`TrailingData` is empty). Verified by `ParticleLibraryFileTests` against the real `Tp2.plb`.
- One per-effect field noted from the loader's post-read fix-up: byte 162 (short 81) defaults to 1000 when
  zero. The engine treats each record as a `short[160]`.

## Consumed by the ride engine (T-007)

The decode is now used at runtime: `RideEngine`'s particle subsystem loads `Tp2.plb` and resolves effects
by their `par_lib.h` code (e.g. `P_EFFECT_Repair`/`P_EFFECT_Sparks`) — using the effect **name** + a
representative **colour-ramp** stop — to render the `REPAIREFFECT`/`SPARK` opcodes' effects. So the names
+ ramp this ticket decoded are wired into live gameplay; the unlabelled per-effect params below would add
real spawn-rate/velocity/size to those effects once decoded.

## Consumer chain (Ghidra — mapped, but the emit is message-driven)

Traced where the effect records (`DAT_00804370`, stride 320 = `short[160]`) are read:

- The **only static xrefs** to the effect array are the **serializers**, not a param reader:
  `FUN_0051fa20` ("add effect" — finds the first empty 320-byte slot, returns its index),
  `FUN_0051f680` / `FUN_0051f7a0` (save / load the whole particle state: magic `'LCTP'` = `0x5054434c`,
  the effect block `0x8b60` B, the shared block `0x9e68` B, plus a live particle pool). So the array is
  copied verbatim; no static reader exposes field meanings. *(Corrects the earlier "no xref" note — there
  are xrefs, they just aren't the consumer.)*
- The **particle subsystem** is an event/message framework: `FUN_0051b660` initialises it (registers the
  per-frame update/render listeners `PTR_LAB_00700b34`/`b30`, zeroes the EVENT effect pools
  `DAT_00803a20` (13) / `DAT_00803aa0` (21)), and stores the **manager** singleton at `DAT_00802bcc`
  (a 0x34-byte object, vtable `PTR_FUN_0070a2a0`). `FUN_0051fb70` builds the particle free-lists
  (pool `DAT_0080cef8`, stride 52 B/particle). `FUN_0051b920` is the staged per-frame tick;
  `FUN_0051c700` (global intensity ramp) and `FUN_0051bd70` (global config) just broadcast settings.
- The **emit** path: `EVENT` types 3-10 / `REPAIREFFECT` / `SPARK` call `FUN_0051bfc0`, which builds a
  message descriptor `{vtable PTR_FUN_00700b90, pool, code, x, y, z}` and invokes the manager's
  **vtable slot 2** (`FUN_006b8720`) — a **generic message dispatcher** (`0x6b8xxx` framework). It routes
  to the registered particle handler, which reads `effectArray[code]`'s fields via a **runtime-computed
  base** (so still no static xref).

## Remaining (low value)

1. The **shared table's** 104-byte records stay raw (`SharedRecords`) — they aren't read by the spawn path,
   so labelling them needs a separate consumer trace; no gameplay consumer in OpenTPW wants them.
2. A handful of minor `short[160]` slots (per-particle size, sprite/texture ref, blend mode) weren't isolated
   — the spawn reads colour from the ramp region and most remaining slots are emitter runtime-state. The
   gameplay-meaningful authored fields are labelled above.
   **Note:** OpenTPW's renderer still uses a colour-proxy for effects (T-007/T-037), so the new fields are
   consumed only once a real particle simulation exists — but they're now decoded and ready.

## Affected files

`source/OpenTPW.Files/Formats/Particle/ParticleLibraryFile.cs`,
`source/OpenTPW.Tests/ParticleLibraryFileTests.cs`.
