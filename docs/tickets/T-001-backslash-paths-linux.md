# T-001 — Hardcoded `\` paths break file access on Linux

- **Priority**: 🔴 High (blocks Linux)
- **Type**: Bug / portability
- **Confirmed by**: `FileSystemTests` failures on Linux

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

## Proposed fix

- Remove the `.Replace("/", "\\")` calls.
- Let `Path.Join` / `Path.Combine` handle the native separator, and normalize incoming
  paths with `Path.DirectorySeparatorChar` when needed:
  `path.Replace('\\', Path.DirectorySeparatorChar).Replace('/', Path.DirectorySeparatorChar)`.
- Audit other usages: `grep -rn 'Replace( "/"' source`.

## Acceptance criteria

- `FileSystemTests` pass on Linux **and** Windows with a valid `data/`.
- No hardcoded `\` in path resolution.

## Affected files

`source/OpenTPW/Client/GameDir.cs`, `source/OpenTPW.Common/Files/BaseFileSystem.cs`
