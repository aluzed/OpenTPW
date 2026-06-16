# T-019 — `.PLB` particle parameter fields

- **Priority**: 🟡 Feature
- **Type**: Reverse engineering
- **Status**: ☐ To do
- **Split from**: [T-008](T-008-unimplemented-formats.md).

## Context

`ParticleLibraryFile` (`OpenTPW.Files/Formats/Particle/ParticleLibraryFile.cs`) decodes the
header (effect count, record size), the per-effect names (which match the disc's `par_lib.h`
`P_EFFECT_*` list exactly), and the **16-stop RGBA colour ramp** at the tail of each record.
The rest of the 272-byte parameter block, and the trailing shared block after the records,
are kept raw.

## Remaining work

1. Decode the non-colour parameter fields: lifetime, spawn rate / count, velocity / spread,
   gravity, size, sprite/texture reference, blend mode. Cross-reference effect names for
   plausibility (e.g. Fire vs Smoke vs Sparks).
2. Decode the trailing shared block after the effect records.

## Acceptance criteria

- Decoded parameters are exposed as typed fields on `ParticleEffect`, validated for internal
  consistency across several effects; extend `ParticleLibraryFileTests`.

## Reverse-engineering aid

The on-disc `par_lib.h` gives only effect **names** (not the struct). The field layout needs
**Ghidra** on the particle loader in `TP.EXE` — see [05-ghidra-reverse.md](../05-ghidra-reverse.md).

## Affected files

`source/OpenTPW.Files/Formats/Particle/ParticleLibraryFile.cs`,
`source/OpenTPW.Tests/ParticleLibraryFileTests.cs`.
