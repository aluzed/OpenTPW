# T-001 — Hardcoded `\` paths break file access on Linux

- **Priority**: 🔴 High (blocks Linux)
- **Type**: Bug / portability
- **Confirmed by**: `FileSystemTests` failures on Linux
- **Status**: ✅ **Done.** Both call sites now normalize to
  `Path.DirectorySeparatorChar` (correct on Windows *and* Linux). Validated on Linux:
  `FileSystemTests.TestRead` passes against a fixture set via `OPENTPW_GAMEPATH`
  (previously failed with a mangled `\`-separated path).

## Symptom

The tests produce absurd paths mixing `/` and `\`:

```
.../net8.0/\var\www\reverse\...\net8.0\C:\Program Files (x86)\Bullfrog\Theme Park World\Data\...
System.IO.FileNotFoundException / DirectoryNotFoundException
```

On Linux `\` is not a directory separator → all file access fails.

## Cause

Forced backslash conversion:

```csharp
// source/OpenTPW/Client/GameDir.cs:15
return Path.Join( Settings.Default.GamePath, path ).Replace( "/", "\\" );

// source/OpenTPW.Common/Files/BaseFileSystem.cs:200
return Path.Combine( basePath, relativePath.TrimStart('/') ).Replace( "/", "\\" );
```

## Fix applied

Replaced the `.Replace("/", "\\")` calls with separator normalization
(`.Replace('/', Path.DirectorySeparatorChar).Replace('\\', Path.DirectorySeparatorChar)`)
so the result is correct on every OS. `GetRelativePath` already normalizes to `/` for
the virtual-path representation and was left as-is.

## Acceptance criteria

- `FileSystemTests` pass on Linux **and** Windows with a valid `data/`.
- No hardcoded `\` in path resolution.

## Affected files

`source/OpenTPW/Client/GameDir.cs`, `source/OpenTPW.Common/Files/BaseFileSystem.cs`
