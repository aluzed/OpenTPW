# Tickets — OpenTPW backlog

Tickets derived from the 2026-06-15 analysis (build + tests run on Linux with
.NET 8.0.422). See [../README.md](../README.md) for context.

> Note: the `origin` remote points at the **upstream** repo `OpenTPW/OpenTPW`.
> These are local ticket files; convert them to GitHub issues on **your fork** if
> needed (do not open issues directly on upstream).

## Build / test state (observed)

- **Build**: ✅ `dotnet build OpenTPW.sln` → 6 projects, **0 errors, 0 warnings** (T-009).
- **Tests**: ✅ `dotnet test` → **0 failed, 21 passed, 13 inconclusive** on a clean Linux
  machine (was 7/7 failing). The inconclusive ones are integration tests that need a game
  install (`OPENTPW_GAMEPATH`) or a real asset sample (`TPW_VIDEO_SAMPLE`,
  `TPW_FONT_SAMPLE`, `TPW_MODEL_SAMPLE`). See T-002 / T-008 / T-012.

## Index

| # | Priority | Status | Title |
|---|----------|--------|-------|
| [T-001](T-001-backslash-paths-linux.md) | 🔴 High | ✅ Done | Hardcoded `\` paths break everything on Linux |
| [T-002](T-002-tests-absolute-paths.md) | 🔴 High | ✅ Done | Tests: hardcoded absolute paths + dependency on a game install |
| [T-003](T-003-naudio-not-portable.md) | 🟠 Medium | ✅ Done | NAudio (audio) not portable off Windows |
| [T-004](T-004-system-drawing-modkit.md) | 🟠 Medium | ✅ Mostly | `System.Drawing.Common` is Windows-only in the ModKit |
| [T-005](T-005-vulnerable-dependencies.md) | 🟠 Medium | ✅ Done | Vulnerable dependencies (direct + transitive) |
| [T-006](T-006-gamepath-config.md) | 🟡 Low | ✅ Done | Windows default `GamePath` + no portable override |
| [T-007](T-007-vm-opcodes-rse.md) | 🟡 Feature | ⚠️ Partial | Ride VM: `.RSE` loader restored; 34/210 opcodes (LIFO stack + END/PUSH/POP) |
| [T-008](T-008-unimplemented-formats.md) | 🟡 Feature | ⚠️ Partial | Formats: `.BF4` ✅, `.TQI/.TGQ` ✅, `.PLB` ✅, `.LIP` ⚠️, `.MTR` ⚠️ |
| [T-009](T-009-build-warnings.md) | ⚪ Debt | ✅ Done | build warnings (105 → 0: nullable, Dispose, dead code) |
| [T-010](T-010-add-sub-flags.md) | 🟠 Medium | ✅ Done | ADD/SUB don't set arithmetic flags (branch correctness) |
| [T-011](T-011-branchto-hardening.md) | 🟡 Feature | ✅ Done | Harden `RideVM.BranchTo` (O(1) map; verified by a compiled loop) |
| [T-012](T-012-partial-formats.md) | 🟡 Feature | ⚠️ Partial | `.MD2` verified (renders); robustness + `.MAP`/`.TPWS` remain |
| [T-013](T-013-ci-pipeline.md) | 🟠 Medium | ✅ Done | Add CI (build + test on Linux) |
| [T-014](T-014-case-insensitive-assets.md) | 🟠 Medium | ✅ Done | Case-insensitive asset path resolution (Linux) |

Priority legend: 🔴 blocking · 🟠 important · 🟡 desirable/feature · ⚪ technical debt.
Status legend: ✅ done · ⚠️ partial · ☐ to do.
