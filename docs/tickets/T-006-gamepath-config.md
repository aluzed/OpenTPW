# T-006 — Windows default `GamePath` + no portable override

- **Priority**: 🟡 Low
- **Type**: Portability / DX
- **Status**: ⚠️ Partially addressed — the **test suite** now honors `OPENTPW_GAMEPATH`
  (see T-002). **Remaining**: make the **game itself** read the same env var at startup,
  overriding the Windows default in `Settings.Designer.cs` / `Client/Game.cs`.

## Findings

```csharp
// source/OpenTPW/Settings.Designer.cs:28
[DefaultSettingValue("C:\\Program Files (x86)\\Bullfrog\\Theme Park World")]
public string GamePath { get; }
```

Windows default value, and resolution goes through `System.Configuration`
(`app.config`), which is awkward for Linux/CI. There is no simple way to point at the
game via an environment variable.

## Proposed fix

- Allow an override via an **environment variable** (e.g. `OPENTPW_GAMEPATH`) read at
  startup, taking precedence over the `app.config` setting.
- Document the setting in [../Linux.md](../Linux.md).
- Optional: auto-detection (Wine prefix, `~/.local/share`, GOG…).

## Link

Unblocks hermetic tests — see [T-002](T-002-tests-absolute-paths.md).

## Affected files

`source/OpenTPW/Settings.Designer.cs` / `Settings.settings`, `source/OpenTPW/Client/Game.cs`
