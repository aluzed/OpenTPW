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
| [T-008](T-008-unimplemented-formats.md) | 🟡 Feature | 🗂️ Split | Umbrella (closed): `.BF4` ✅, `.TQI/.TGQ` ✅; remainders → T-018/019/020/021/022 |
| [T-009](T-009-build-warnings.md) | ⚪ Debt | ✅ Done | build warnings (105 → 0: nullable, Dispose, dead code) |
| [T-010](T-010-add-sub-flags.md) | 🟠 Medium | ✅ Done | ADD/SUB don't set arithmetic flags (branch correctness) |
| [T-011](T-011-branchto-hardening.md) | 🟡 Feature | ✅ Done | Harden `RideVM.BranchTo` (O(1) map; verified by a compiled loop) |
| [T-012](T-012-partial-formats.md) | 🟡 Feature | 🗂️ Split | Umbrella (closed): `.MD2` animated ✅, `.MAP` BANK names ✅; remainders → T-015/016/017 |
| [T-013](T-013-ci-pipeline.md) | 🟠 Medium | ✅ Done | Add CI (build + test on Linux) |
| [T-014](T-014-case-insensitive-assets.md) | 🟠 Medium | ✅ Done | Case-insensitive asset path resolution (Linux) |
| [T-015](T-015-md2-static-variant.md) | 🟡 Feature | ⚠️ Partial | `.MD2` version gate (0xDD/0xCB) Ghidra-confirmed; legacy decode remains |
| [T-016](T-016-map-entry-records.md) | 🟡 Feature | ⚠️ Partial | `.MAP`: variant detection + SFX category header done; record fields need Ghidra |
| [T-017](T-017-tpws-saves.md) | 🟡 Feature | ☐ To do | `.TPWS` save files: complete read + implement write |
| [T-018](T-018-mtr-material-semantics.md) | 🟡 Feature | ☐ To do | `.MTR` material semantics + `.MD2` texture binding |
| [T-019](T-019-plb-parameter-fields.md) | 🟡 Feature | ☐ To do | `.PLB` particle parameter fields (beyond the colour ramp) |
| [T-020](T-020-lip-mouth-shapes.md) | 🟡 Feature | ☐ To do | `.LIP` mouth-shape semantics + lip-sync wiring |
| [T-021](T-021-tqi-exact-dequant.md) | ⚪ Polish | ☐ To do | `.TQI` video: exact (AAN) dequantization |
| [T-022](T-022-ea-adpcm-mono.md) | 🟡 Feature | ☐ To do | EA-ADPCM mono audio support |

Priority legend: 🔴 blocking · 🟠 important · 🟡 desirable/feature · ⚪ technical debt/polish.
Status legend: ✅ done · ⚠️ partial · ☐ to do · 🗂️ split into focused tickets.
