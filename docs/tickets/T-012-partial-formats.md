# T-012 — Complete the partially-implemented formats (.MD2, .MAP, .TPWS)

- **Priority**: 🟡 Feature
- **Type**: Feature / reverse engineering
- **Status**: ☐ To do
- **Note**: distinct from [T-008](T-008-unimplemented-formats.md) (which tracks the
  ❌ *not-started* formats). This ticket tracks the ⚠️ *partial* ones, which had no ticket.

## Scope

| Format | Code | Current state | Remaining |
|--------|------|---------------|-----------|
| `.MD2` models | `OpenTPW.Files/Formats/Model/ModelFile.cs` | partial load | finish parsing + render integration (note: `Formats\Model\**` is excluded from the main `OpenTPW.csproj` compile). |
| `.MAP` maps | `World/Terrain` | demo terrain hardcoded | generalize parsing; load real level geometry. |
| `.TPWS` saves | `OpenTPW.Files/Formats/Save/SaveReader.cs` | partial read | complete read; implement write. |

## Approach

1. Cross-check each parser against a real asset (extract from the disc's `DATA/`, see
   [../03-disc-compatibility.md](../03-disc-compatibility.md)) and the format docs
   (`OpenTPW/OpenTPW.FileFormats` repo).
2. Add fixtures + tests (follow the `RideScriptTests` pattern; gate on game data with
   `OPENTPW_GAMEPATH` like [T-002](T-002-tests-absolute-paths.md) where a full asset is needed).

## Acceptance criteria

- Each format round-trips a real sample (read; write where applicable) under test.

## Affected files

`source/OpenTPW.Files/Formats/Model/ModelFile.cs`,
`source/OpenTPW.Files/Formats/Save/SaveReader.cs`, `source/OpenTPW/World/Terrain/*`.
