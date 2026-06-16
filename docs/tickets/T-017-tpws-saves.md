# T-017 — `.TPWS` save files (read + write)

- **Priority**: 🟡 Feature
- **Type**: Reverse engineering
- **Status**: ☐ To do
- **Split from**: [T-012](T-012-partial-formats.md).

## Context

`SaveReader` (`OpenTPW.Files/Formats/Save/SaveReader.cs`) does a partial read of `.TPWS`
saves and cannot write them. There is **no save sample on the install disc** (saves are
user-generated), so this needs a save produced by a real install (`.TPWS` / `.INTS` initial
save / `.LAYS` online save).

## Remaining work

1. Obtain a `.TPWS` sample (run the game, or the `.INTS` initial-save template if present).
2. Complete the read path (fields beyond the current partial parse).
3. Implement writing (round-trip a loaded save).

## Acceptance criteria

- A real `.TPWS` round-trips under test (read → write → read produces equal state),
  gated on a sample env var like the other format tests.

## Affected files

`source/OpenTPW.Files/Formats/Save/SaveReader.cs`.
