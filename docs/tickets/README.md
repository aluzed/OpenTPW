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
| [T-026](T-026-render-resource-churn.md) | 🔴 High | ✅ Done | Renderer: killed per-frame GPU resource churn (sync submit + ephemeral sets) → lobby at 60fps |
| [T-027](T-027-ui-draw-batching.md) | 🟠 Medium | ✅ Done | Renderer: UI draws batched (merge same-texture), per-quad allocs + set-churn removed |
| [T-028](T-028-frame-cpu-hygiene.md) | ⚪ Debt | ✅ Done | Renderer: per-frame CPU hygiene (dirty-shader registry, `Stopwatch`) |
| [T-029](T-029-native-render-loop-re.md) | 🟢 Low | ✅ Done | Native render loop RE'd (DDraw + D3D execute buffers + MMX software); see docs/07 |
| [T-030](T-030-async-level-load.md) | 🟠 Medium | ⚠️ Mostly | Level load: freeze resolved (per-step + per-mesh progress); only optional 60fps async remains |
| [T-031](T-031-game-audio.md) | 🟡 Feature | ⚠️ Mostly | Game audio: lobby music (minimp3), UI click SFX, music volume keys, cross-platform build; ambience/settings-UI remain |
| [T-032](T-032-ride-engine.md) | 🟡 Feature | ⚠️ In progress | Ride engine: VM→engine seam + sound + a ride rendered/running in-scene (slice 1); anim/lights/peeps/park remain |
| [T-033](T-033-ride-animation-keyframes.md) | 🟡 Feature | ✅ Core done | Ride animation: rotation + translation/scale + vertex-morph keyframes all RE'd and driven from real ride data, verified in-game; polish remains — see docs/08 |
| [T-034](T-034-peeps.md) | 🟡 Feature | ⚠️ Mostly | Peeps: full crowd loop — real animated `esprites.wad` sprites (TPC codec RE'd), directional walk cycles, queueing, riding, needs, economy, staff (entertainers/handymen/guards); polish split → T-035–T-039 |
| [T-035](T-035-peep-sprite-polish.md) | ⚪ Polish | ☐ To do | Peep sprite polish: camera-relative facing, idle/sit poses, `.FPC` shadow, hotspot anchoring |
| [T-036](T-036-peep-pathfinding.md) | 🟡 Feature | ☐ To do | Peep pathfinding: walkable path graph + A* (route over paths, not straight lines) |
| [T-037](T-037-ride-cycle-sound.md) | 🟡 Feature | ☐ To do | Ride boarding/unloading SFX (blocked on T-016 `.MAP` catalog) + duration from script |
| [T-038](T-038-park-management-ui.md) | 🟡 Feature | 🗂️ Umbrella | Park management & build mode — split into T-040–T-045 (mode/ACTION state machine RE'd from `tp.exe`) |
| [T-039](T-039-peep-needs-staff-depth.md) | 🟡 Feature | ☐ To do | Peep needs & staff depth: thirst/drink stalls, vandalism↔guards, ride ratings, balance |
| [T-040](T-040-build-mode-foundation.md) | 🟡 Feature | ✅ Core done | Build/manage **foundation**: controllable in-park camera + mouse→tile picking + highlight + click dispatch (verified in-game) — blocks T-041–T-045 |
| [T-041](T-041-ride-shop-placement.md) | 🟡 Feature | ⚠️ Core done | Ride & shop placement: catalog + footprint preview + place-on-click with cost charging + queue registration (empty park, player builds); rotation/sell remain |
| [T-042](T-042-economy-controls-loans.md) | 🟡 Feature | ⚠️ Core done | Economy controls: settable ride prices + admission fee, loans (take/repay/monthly/bankruptcy), HUD readout (verified); clickable panel+graph remain |
| [T-043](T-043-staff-management.md) | 🟡 Feature | ⚠️ Core done | Staff hire+place via catalog (entertainer/handyman/guard/researcher), charged + wages (verified); fire/patrol-zones remain |
| [T-044](T-044-research-upgrades.md) | 🟡 Feature | ⚠️ Core done | Research + ride capacity upgrades: full `Upgrades[*]` parsed, researchers advance research, apply bumps live capacity (verified); per-ride UI remains |
| [T-045](T-045-coaster-track-editor.md) | 🟡 Feature | ⚠️ Slices 1–3a | Coaster: station placeable/queues + elevated track-laying tool with pylons + a shuttle train running the laid segments (done, verified); 3D curved generation + real CrocCar + peep boarding remain (slice 3b) |

Priority legend: 🔴 blocking · 🟠 important · 🟡 desirable/feature · ⚪ technical debt/polish.
Status legend: ✅ done · ⚠️ partial · ☐ to do · 🗂️ split into focused tickets · ⏸️ deferred.
