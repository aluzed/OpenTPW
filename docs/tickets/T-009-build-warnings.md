# T-009 — build warnings

- **Priority**: ⚪ Technical debt
- **Type**: Code quality
- **Status**: ✅ **Done.** `dotnet build OpenTPW.sln` → **0 errors, 0 warnings**
  (was ~105). Tests unchanged: 0 failed, 21 passed, 13 inconclusive.

## Findings (as observed)

- **Nullable init** (`CS8618`, ×75): non-nullable fields/properties not set in the
  constructor (`Log`, `FileSystem`, archive `buffer`/`memoryStream`, render `Pipeline`,
  `Texture`, VM `FileData`, ModKit tabs…). All of these are assigned later in
  `ReadFromStream` / `SetupResources` / at startup.
- **Null flow** (`CS8602`/`8603`/`8604`/`8601`/`8600`/`8625`, ×25): dereference / return /
  argument / assignment of values the flow analysis can't prove non-null
  (archive lookups, `Path.GetDirectoryName`, `Activator.CreateInstance`, reflection
  attributes, `Dictionary.TryGetValue` out params).
- **Member hiding / dead code** (×5): `IArchive.Dispose()` hid `IDisposable.Dispose()`
  (`CS0108`); `MP2File.Name` hid `ArchiveItem.Name` (`CS0108`); `SoundFile.ReadFromStream`
  hid `BaseFormat.ReadFromStream` (`CS0114`); `Editor.shouldRender` was assigned but never
  used (`CS0414`); `Material.ClearBoundResources` had unreachable code after a `return;`
  (`CS0162`).

## What was done

- **CS8618**: applied the established `= null!` idiom to the 75 fields/properties that are
  populated post-construction (consistent with the existing code, e.g. `RideVM.FileData`).
- **Member hiding / dead code** (fixed properly, not suppressed):
  - removed the redundant `IArchive.Dispose()` declaration (the interface already extends
    `IDisposable`);
  - `MP2File.Name` → `public new string Name` (intentionally non-null, set in the ctor);
  - `SoundFile.ReadFromStream` → `protected override` (it overrides `BaseFormat`);
  - removed the unused `Editor.shouldRender` field;
  - `Material.ClearBoundResources` reduced to an explained no-op (the clear was deliberately
    disabled; the dead body was removed).
- **Null flow**: each site reviewed individually — honest fixes where null is real
  (nullable locals/params: `WadArchive.newSubDir`, `FileSystemDirectory.parent`;
  `ShaderPreprocessor` now reads block values via `TryGetValue` temporaries; `EnumerateItems`
  returns an empty list instead of `null`), and `!` / `null!` only where the value is
  provably non-null or where the existing (non-null) public contract is intentionally kept
  (`BaseFileSystem.GetArchive`/`OpenRead`, `WadArchive.GetItem`/`OpenFile` — behavior
  preserved exactly).

## Follow-up (optional)

- Consider `TreatWarningsAsErrors` now that the build is clean, to prevent regressions.
  Left off for now since the renderer/ModKit are still under active reverse-engineering.

## Acceptance criteria

- ✅ Warning count reduced to **0** on a clean build; tests still green.

## Affected files

`OpenTPW.Common` (`BaseFileSystem`, `IArchive`, `GlobalNamespace`, `Window`),
`OpenTPW.Files` (archive/string/save/sound/model readers, `Refpack`),
`OpenTPW` (`Renderer`, render assets, `ShaderPreprocessor`, `RootPanel`, `RideVM`,
`Level`, `Layout`), `OpenTPW.ModKit` (`Editor`, `FileBrowser`, `TextureViewer`,
`BaseTab`, `GlobalNamespace`).
