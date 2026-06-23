# T-019 — `.PLB` particle parameter fields

- **Priority**: 🟡 Feature
- **Type**: Reverse engineering
- **Status**: ⚠️ Partial (advanced) — the **full file layout is now Ghidra-confirmed and decoded** (item 2
  done): the header is 8 bytes (not 16), the trailing "shared block" is a real second table + two globals,
  all exposed typed. Per-effect parameter *field labels* (item 1) remain — they need the particle
  *consumer* code traced (the loader has no static xref to read meanings from).
- **Split from**: [T-008](T-008-unimplemented-formats.md).

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

## Remaining

1. **Per-effect parameter field labels** (item 1): lifetime, spawn rate/count, velocity/spread, gravity,
   size, sprite/texture ref, blend mode. The loader copies the record verbatim, so meanings live in the
   particle *spawn/update* code — which references the effect array by a runtime-computed
   `base + index*320` (no static xref to `DAT_00804370`), so it needs a manual trace / dynamic capture,
   not a one-shot xref. The block is exposed raw (`ParticleEffect.Parameters`) until then.
2. Label the shared table's 104-byte records (same consumer-trace requirement).

## Affected files

`source/OpenTPW.Files/Formats/Particle/ParticleLibraryFile.cs`,
`source/OpenTPW.Tests/ParticleLibraryFileTests.cs`.
