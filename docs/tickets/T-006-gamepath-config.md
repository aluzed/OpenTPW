# T-006 — Windows default `GamePath` + no portable override

- **Priority**: 🟡 Low
- **Type**: Portability / DX
- **Status**: ✅ **Done.** `GameDir.GamePath` resolves the install path with
  `OPENTPW_GAMEPATH` taking precedence over the persisted setting; `Client/Game.cs` now
  uses it for the data/save directories, and the test suite honors the same variable
  (T-002). Covered by `GameDirTests.EnvironmentVariableOverridesGamePath`.
  **Optional follow-up**: auto-detection (Wine prefix, GOG, `~/.local/share`).

## Findings

```csharp
// source/OpenTPW/Settings.Designer.cs:28
[DefaultSettingValue("C:\\Program Files (x86)\\Bullfrog\\Theme Park World")]
public string GamePath { get; }
```

Windows default value, and resolution goes through `System.Configuration`
(`app.config`), which is awkward for Linux/CI. There is no simple way to point at the
game via an environment variable.

## Fix applied

- Added `GameDir.GamePath`: returns `OPENTPW_GAMEPATH` when set, else
  `Settings.Default.GamePath`.
- `Client/Game.cs` resolves the path once via `GameDir.GamePath` and uses
  `Path.Join` for the `data`/`save` subfolders (no hardcoded `/`).
- Documented in [../Linux.md](../Linux.md) §5.
- Optional (not done): auto-detection (Wine prefix, `~/.local/share`, GOG…).

## Link

Unblocks hermetic tests — see [T-002](T-002-tests-absolute-paths.md).

## Affected files

`source/OpenTPW/Settings.Designer.cs` / `Settings.settings`, `source/OpenTPW/Client/Game.cs`
