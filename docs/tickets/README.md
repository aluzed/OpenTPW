# Tickets — OpenTPW backlog

Tickets derived from the 2026-06-15 analysis (build + tests run on Linux with
.NET 8.0.422). See [../README.md](../README.md) for context.

> Note: the `origin` remote points at the **upstream** repo `OpenTPW/OpenTPW`.
> These are local ticket files; convert them to GitHub issues on **your fork** if
> needed (do not open issues directly on upstream).

## Build / test state (observed)

- **Build**: ✅ `dotnet build OpenTPW.sln` → 6 projects, **0 errors, 0 warnings** (T-009).
- **Tests**: ✅ `dotnet test` → **0 failed, 38 passed, 9 inconclusive** on a clean Linux
  machine (was 7/7 failing). The inconclusive ones are integration tests that need a game
  install (`OPENTPW_GAMEPATH`) or a real asset sample (`TPW_VIDEO_SAMPLE`, `TPW_FONT_SAMPLE`,
  `TPW_MODEL_SAMPLE`, `TPW_MAP_SAMPLE`, `TPW_PLB_SAMPLE`, `TPW_MTR_SAMPLE`, `TPW_LIP_SAMPLE`).
  All pass when the samples are provided.

## Index

| # | Priority | Status | Title |
|---|----------|--------|-------|
| [T-001](T-001-backslash-paths-linux.md) | 🔴 High | ✅ Done | Hardcoded `\` paths break everything on Linux |
| [T-002](T-002-tests-absolute-paths.md) | 🔴 High | ✅ Done | Tests: hardcoded absolute paths + dependency on a game install |
| [T-003](T-003-naudio-not-portable.md) | 🟠 Medium | ✅ Done | NAudio (audio) not portable off Windows |
| [T-004](T-004-system-drawing-modkit.md) | 🟠 Medium | ✅ Mostly | `System.Drawing.Common` is Windows-only in the ModKit |
| [T-005](T-005-vulnerable-dependencies.md) | 🟠 Medium | ✅ Done | Vulnerable dependencies (direct + transitive) |
| [T-006](T-006-gamepath-config.md) | 🟡 Low | ✅ Done | Windows default `GamePath` + no portable override |
| [T-007](T-007-vm-opcodes-rse.md) | 🟡 Feature | ⚠️ Partial | Ride VM: 51/**106** — Batch A (43 pure) complete + SPAWNCHILD (1st Batch B) |
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
| [T-018](T-018-mtr-material-semantics.md) | 🟡 Feature | ✅ Done | `.MTR` not runtime-used (Ghidra); `.MD2` carries texture binding (decoded + tested) |
| [T-019](T-019-plb-parameter-fields.md) | 🟡 Feature | ☐ To do | `.PLB` particle parameter fields (beyond the colour ramp) |
| [T-020](T-020-lip-mouth-shapes.md) | 🟡 Feature | ☐ To do | `.LIP` mouth-shape semantics + lip-sync wiring |
| [T-021](T-021-tqi-exact-dequant.md) | ⚪ Polish | ⏸️ Deferred | `.TQI`: float AAN IDCT confirmed (Ghidra); exact port deferred (decoder already renders correctly) |
| [T-022](T-022-ea-adpcm-mono.md) | 🟡 Feature | ☐ To do | EA-ADPCM mono audio support |
| [T-023](T-023-linux-vulkan-libdl.md) | 🔴 High | ✅ Done | Linux runtime: Vulkan `libdl` load fix — game now boots on Linux |
| [T-024](T-024-linux-black-screen.md) | 🟠 Medium | ✅ Done | Linux "black screen" was the synchronous load; lobby renders (loading screen + text) |
| [T-025](T-025-bf4-antialiased-fonts.md) | 🟢 Low | ☐ To do | `.BF4`: decode the antialiased (multi-bit) font variant (`MENU*`/`TITLE*`/`*AA`) |
| [T-026](T-026-render-resource-churn.md) | 🔴 High | ☐ To do | Renderer: per-frame GPU resource churn freezes the lobby (sync submit + ephemeral sets) |
| [T-027](T-027-ui-draw-batching.md) | 🟠 Medium | ☐ To do | Renderer: batch UI draws, remove per-quad allocations |
| [T-028](T-028-frame-cpu-hygiene.md) | ⚪ Debt | ☐ To do | Renderer: per-frame CPU hygiene (dirty-shader registry, `Stopwatch`) |
| [T-029](T-029-native-render-loop-re.md) | 🟢 Low | ☐ To do | RE the native TPW main loop & render dispatch (reference/validation) |
| [T-030](T-030-async-level-load.md) | 🟠 Medium | ⚠️ Partial | Level load: checkpoint progress (status per step) done; true background load remains |

Priority legend: 🔴 blocking · 🟠 important · 🟡 desirable/feature · ⚪ technical debt/polish.
Status legend: ✅ done · ⚠️ partial · ☐ to do · 🗂️ split into focused tickets · ⏸️ deferred.
