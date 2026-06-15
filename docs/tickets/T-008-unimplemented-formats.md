# T-008 — Unimplemented file formats

- **Priority**: 🟡 Feature
- **Type**: Feature / reverse engineering

## Formats to handle

| Format | Status | Samples (on the CD) | Notes |
|--------|:------:|---------------------|-------|
| `.BF4` fonts | ❌ | `DATA/FONTS.WAD` | — |
| `.MTR` materials | ❌ | inside the `.WAD`s | Needed for correct model rendering. |
| `.LIPS` lip-sync | ❌ | `DATA/GLOBAL/SPEECH` | — |
| `.TQI`/`.TGQ` video | ❌ | `DATA/MOVIES/*.TGQ` | Bullfrog video codec. |
| `.PLB` particles | (not listed) | `DATA/PARTICLE/TP2.PLB` | **`PAR_LIB.H` on the CD documents the format!** |
| `.MD2` models | ⚠️ | `.WAD`s | `ModelFile.cs` partial — to finish. |
| `.MAP` maps | ⚠️ | `DATA/LEVELS/...` | parsing to generalize (terrain currently hardcoded). |

## Recommended approach

1. For each format: analyze a real sample (hex editor) + reverse the read routine in
   `TP.EXE` via **Ghidra** ([../05-ghidra-reverse.md](../05-ghidra-reverse.md)).
2. Document the structure under `docs/`.
3. Implement the parser in `source/OpenTPW.Files/Formats/` (follow the existing
   pattern: `BaseStream`, `*Reader`).
4. Add a test with a fixture.

## Quick win

`DATA/PARTICLE/PAR_LIB.H` is an **original C header**: it likely describes the `.PLB`
particle format without any reversing — tackle it first.

## Affected files

`source/OpenTPW.Files/Formats/` (new parsers), `source/OpenTPW.Files/Formats/Model/ModelFile.cs`
