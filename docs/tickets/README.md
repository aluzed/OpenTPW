# Tickets — OpenTPW backlog

Tickets derived from the 2026-06-15 analysis (build + tests run on Linux with
.NET 8.0.422). See [../README.md](../README.md) for context.

> Note: the `origin` remote points at the **upstream** repo `OpenTPW/OpenTPW`.
> These are local ticket files; convert them to GitHub issues on **your fork** if
> needed (do not open issues directly on upstream).

## Build / test state (observed)

- **Build**: ✅ `dotnet build OpenTPW.sln` → 6 projects, **0 errors**.
- **Tests**: ✅ `dotnet test` → **0 failed, 3 passed, 6 inconclusive** on a clean Linux
  machine (was 7/7 failing). The 6 inconclusive are integration tests that need a game
  install (`OPENTPW_GAMEPATH`). See T-002.

## Index

| # | Priority | Status | Title |
|---|----------|--------|-------|
| [T-001](T-001-backslash-paths-linux.md) | 🔴 High | ✅ Done | Hardcoded `\` paths break everything on Linux |
| [T-002](T-002-tests-absolute-paths.md) | 🔴 High | ✅ Done | Tests: hardcoded absolute paths + dependency on a game install |
| [T-003](T-003-naudio-not-portable.md) | 🟠 Medium | ✅ Done | NAudio (audio) not portable off Windows |
| [T-004](T-004-system-drawing-modkit.md) | 🟠 Medium | ✅ Mostly | `System.Drawing.Common` is Windows-only in the ModKit |
| [T-005](T-005-vulnerable-dependencies.md) | 🟠 Medium | ✅ Done | Vulnerable dependencies (direct + transitive) |
| [T-006](T-006-gamepath-config.md) | 🟡 Low | ✅ Done | Windows default `GamePath` + no portable override |
| [T-007](T-007-vm-opcodes-rse.md) | 🟡 Feature | ⚠️ Partial | Ride VM: `.RSE` loader restored; ~180 opcodes remain |
| [T-008](T-008-unimplemented-formats.md) | 🟡 Feature | ☐ To do | Unimplemented formats: `.BF4`, `.MTR`, `.LIPS`, `.TQI/.TGQ` |
| [T-009](T-009-build-warnings.md) | ⚪ Debt | ☐ To do | build warnings (nullable, Dispose, etc.) |

Priority legend: 🔴 blocking · 🟠 important · 🟡 desirable/feature · ⚪ technical debt.
Status legend: ✅ done · ⚠️ partial · ☐ to do.
