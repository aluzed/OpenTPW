# T-002 — Tests: hardcoded absolute paths + dependency on a game install

- **Priority**: 🔴 High
- **Type**: Bug / test quality
- **Confirmed by**: `dotnet test` → was **7/7 failures** on Linux
- **Status**: ✅ **Done.** `dotnet test` now → **0 failed, 1 passed, 6 inconclusive**
  on a clean Linux machine.
  - `ShaderTests`: the shader assets are copied next to the test assembly
    (`OpenTPW.Tests.csproj`) and referenced via `AppContext.BaseDirectory` → passes.
  - `FileSystemTests`: these are integration tests needing a real game install. They now
    read the base path from `OPENTPW_GAMEPATH` and call `RequireGameData()`, which marks
    them `Assert.Inconclusive` (not failed) when the data is absent.

## Symptoms

1. `ShaderTests.PreprocessTest` references an **absolute path from the original dev's
   machine**:
   ```
   E:\OpenTPW\content\shaders\test.shader   (ShaderTests.cs:13)
   ```
2. `FileSystemTests.*` read directly from the Windows default `GamePath`
   (`C:\Program Files (x86)\Bullfrog\...`) → not found, and compound the
   [T-001](T-001-backslash-paths-linux.md) bug.

## Cause

Non-hermetic tests: they depend on a specific environment (drive letter, installed
game) instead of bundling their fixtures.

## Proposed fix

- `ShaderTests`: reference the shader via a path relative to the test project
  (`AppContext.BaseDirectory` + an asset copied with `CopyToOutputDirectory`), not `E:\...`.
- `FileSystemTests`: either
  - ship a small committed `.WAD`/`.SDT` test fixture, or
  - make the tests `[Ignore]`/conditional when `GamePath` is not configured, via an
    environment variable (see [T-006](T-006-gamepath-config.md)).
- Goal: `dotnet test` **green without a game install**.

## Acceptance criteria

- `dotnet test` passes on a clean machine (Linux/CI) without a copy of the game.

## Affected files

`source/OpenTPW.Tests/ShaderTests.cs`, `source/OpenTPW.Tests/FileSystemTests.cs`
