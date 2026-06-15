# T-009 — 109 build warnings

- **Priority**: ⚪ Technical debt
- **Type**: Code quality
- **Confirmed by**: `dotnet build OpenTPW.sln` → 0 errors, **109 warnings**

## Findings

Representative samples:

- **Nullable** (`CS8618`): non-nullable properties not initialized in constructors
  (`Log`, `FileSystem`, `SaveFileSystem`, `CacheFileSystem`, `Current`…).
- **`CS8604`**: possible null argument (`Directory.CreateDirectory(string path)`).
- **`CS0108`**: `IArchive.Dispose()` hides `IDisposable.Dispose()` — use `new` or
  rework the interface.
- Plus ~99 more (unused locals `CS0168`, etc.).

## Proposed fix

- Address nullable warnings (`required` modifier, sensible initialization, or
  `?` where genuinely nullable).
- Fix the `IArchive.Dispose()` hiding (implement `IDisposable` properly).
- Sweep the remaining minor warnings.
- Consider `TreatWarningsAsErrors` once cleaned, to prevent regressions.

## Acceptance criteria

- Warning count significantly reduced (target: 0 on a clean build).

## Affected files

Solution-wide; start with `OpenTPW.Common` (file system) and `OpenTPW.Files`.
