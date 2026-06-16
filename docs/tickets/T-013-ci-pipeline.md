# T-013 — Add a CI pipeline (build + test on Linux)

- **Priority**: 🟠 Medium (process)
- **Type**: Infrastructure
- **Status**: ✅ **Done.** Added `.github/workflows/ci.yml` (restore → build → test →
  vulnerable-package report on `ubuntu-latest`, .NET pinned via `global.json`). Validated
  locally in Release: build 0 errors, tests 0 failed / 5 passed / 6 inconclusive.
  *(Will run once pushed to a fork with Actions enabled.)*
- **Context**: the repo had **no CI** (the `.github` directory was deleted in history).

## Proposed fix

- Add `.github/workflows/ci.yml` (GitHub Actions):
  - `actions/setup-dotnet` with .NET 8 (the repo pins it via `global.json`).
  - `dotnet build source/OpenTPW.sln -c Release`.
  - `dotnet test source/OpenTPW.Tests/OpenTPW.Tests.csproj` (the 6 integration tests are
    `Inconclusive` without game data — they won't fail the run; see [T-002](T-002-tests-absolute-paths.md)).
  - `dotnet list package --vulnerable --include-transitive` as a non-blocking report
    (guards [T-005](T-005-vulnerable-dependencies.md) from regressing).
- Run on `ubuntu-latest`; optionally add `windows-latest` to catch OS-specific regressions
  (the `$(IsWindows)` package conditions in `Directory.Build.props`).

## Acceptance criteria

- PRs run build + test automatically on Linux.
- Green on the current `main`.

## Affected files

`.github/workflows/ci.yml` (new).
