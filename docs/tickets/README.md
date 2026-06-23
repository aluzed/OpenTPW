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
| [T-007](T-007-vm-opcodes-rse.md) | 🟡 Feature | ⚠️ Partial | Ride VM: **104/106** — Batch A (43 pure) complete + nearly all Batch B (objects incl. ADDOBJ_EXT/anim/`WAIT*`/sound/scream, limbo/cross-VM/walk/head pure-VM families, TURBO, + the **light** & **particle** (.PLB-driven) subsystems); only TOUR + BUMP remain (need their own engine subsystems) |
| [T-008](T-008-unimplemented-formats.md) | 🟡 Feature | 🗂️ Split | Umbrella (closed): `.BF4` ✅, `.TQI/.TGQ` ✅; remainders → T-018/019/020/021/022 |
| [T-009](T-009-build-warnings.md) | ⚪ Debt | ✅ Done | build warnings (105 → 0: nullable, Dispose, dead code) |
| [T-010](T-010-add-sub-flags.md) | 🟠 Medium | ✅ Done | ADD/SUB don't set arithmetic flags (branch correctness) |
| [T-011](T-011-branchto-hardening.md) | 🟡 Feature | ✅ Done | Harden `RideVM.BranchTo` (O(1) map; verified by a compiled loop) |
| [T-012](T-012-partial-formats.md) | 🟡 Feature | 🗂️ Split | Umbrella (closed): `.MD2` animated ✅, `.MAP` BANK names ✅; remainders → T-015/016/017 |
| [T-013](T-013-ci-pipeline.md) | 🟠 Medium | ✅ Done | Add CI (build + test on Linux) |
| [T-014](T-014-case-insensitive-assets.md) | 🟠 Medium | ✅ Done | Case-insensitive asset path resolution (Linux) |
| [T-015](T-015-md2-static-variant.md) | 🟡 Feature | ⚠️ Partial | `.MD2` version gate (0xDD/0xCB) Ghidra-confirmed; legacy decode remains |
| [T-016](T-016-map-entry-records.md) | 🟡 Feature | ✅ Decoded | `.MAP`: variant + BANK names + SFX category header + **SFX per-sound 20-byte records** decoded; BANK records RE'd as serialized pointers (not data); only the SFX mixing-curve blob stays raw |
| [T-017](T-017-tpws-saves.md) | 🟡 Feature | ⚠️ Partial | `.TPWS`: container Ghidra-corrected (leading bytes = **version 500**, not magic; full header layout) + read + **write/round-trip**; inner `SAD_*` module stream stays opaque, real sample still unavailable |
| [T-018](T-018-mtr-material-semantics.md) | 🟡 Feature | ✅ Done | `.MTR` not runtime-used (Ghidra); `.MD2` carries texture binding (decoded + tested) |
| [T-019](T-019-plb-parameter-fields.md) | 🟡 Feature | ⚠️ Partial | `.PLB`: **layout Ghidra-confirmed & fully decoded** (8-byte header fix + the trailing block is a 2nd 20×104 table + density/total globals, all typed; whole file accounted for). Per-effect param **field labels** still need the consumer traced |
| [T-020](T-020-lip-mouth-shapes.md) | 🟡 Feature | ⚠️ Partial | `.LIP` semantics resolved (Ghidra): shapes **not in the file** — engine has 5 visemes (`FUN_0044b2e0`), picked per keyframe interval at runtime; `MouthShape`/`ShapeAt` + timing test added. Live advisor-mouth wiring remains |
| [T-021](T-021-tqi-exact-dequant.md) | ⚪ Polish | ⏸️ Deferred | `.TQI`: float AAN IDCT confirmed (Ghidra); exact port deferred (decoder already renders correctly) |
| [T-022](T-022-ea-adpcm-mono.md) | 🟡 Feature | ⚠️ Implemented | EA-ADPCM **mono** path added (channel dispatch + `DecodeScdlMono`, two samples/byte per FFmpeg adpcm_ea) + synthesised test; waveform verification awaits a real mono sample (none in install) |
| [T-023](T-023-linux-vulkan-libdl.md) | 🔴 High | ✅ Done | Linux runtime: Vulkan `libdl` load fix — game now boots on Linux |
| [T-024](T-024-linux-black-screen.md) | 🟠 Medium | ✅ Done | Linux "black screen" was the synchronous load; lobby renders (loading screen + text) |
| [T-025](T-025-bf4-antialiased-fonts.md) | 🟢 Low | ⚠️ Partial | `.BF4`: encoding tag found at glyph offset 12 (1bpp/raw-4bpp/compressed-4bpp); **raw-4bpp AA decoded** (the `*AA` faces, coverage→alpha in `FontAtlas`, verified+tested); the compressed-4bpp menu/title faces remain |
| [T-026](T-026-render-resource-churn.md) | 🔴 High | ✅ Done | Renderer: killed per-frame GPU resource churn (sync submit + ephemeral sets) → lobby at 60fps |
| [T-027](T-027-ui-draw-batching.md) | 🟠 Medium | ✅ Done | Renderer: UI draws batched (merge same-texture), per-quad allocs + set-churn removed |
| [T-028](T-028-frame-cpu-hygiene.md) | ⚪ Debt | ✅ Done | Renderer: per-frame CPU hygiene (dirty-shader registry, `Stopwatch`) |
| [T-029](T-029-native-render-loop-re.md) | 🟢 Low | ✅ Done | Native render loop RE'd (DDraw + D3D execute buffers + MMX software); see docs/07 |
| [T-030](T-030-async-level-load.md) | 🟠 Medium | ⚠️ Mostly | Level load: freeze resolved (per-step + per-mesh progress); only optional 60fps async remains |
| [T-031](T-031-game-audio.md) | 🟡 Feature | ⚠️ Mostly | Game audio: lobby music (minimp3), UI click SFX, music volume keys, cross-platform build; ambience/settings-UI remain |
| [T-032](T-032-ride-engine.md) | 🟡 Feature | ⚠️ In progress | Ride engine: VM→engine seam + sound + a ride rendered/running in-scene (slice 1); anim/lights/peeps/park remain |
| [T-033](T-033-ride-animation-keyframes.md) | 🟡 Feature | ✅ Core done | Ride animation: rotation + translation/scale + vertex-morph keyframes all RE'd and driven from real ride data, verified in-game; polish remains — see docs/08 |
| [T-034](T-034-peeps.md) | 🟡 Feature | ⚠️ Mostly | Peeps: full crowd loop — real animated `esprites.wad` sprites (TPC codec RE'd), directional walk cycles, queueing, riding, needs, economy, staff (entertainers/handymen/guards); polish split → T-035–T-039 |
| [T-035](T-035-peep-sprite-polish.md) | ⚪ Polish | ✅ Done | Peep/staff sprite polish: camera-relative facing (`SpriteFacing`, unit-tested), idle standing pose, hotspot-anchored pixel-unit quads (no scale jitter; fixed a `TpcFile` hotspot-offset bug), procedural ground shadow; `.FPC` identified as an alternate full sprite set (not a shadow) |
| [T-036](T-036-peep-pathfinding.md) | 🟡 Feature | ⚠️ Core done | Peep pathfinding: `PathGraph` A* over the `PlacementGrid` (ride/shop footprints block, queue paths walkable) — peeps route around rides instead of straight lines (unit-tested + verified in-game); water-avoidance + real gate node wait on the real level terrain |
| [T-037](T-037-ride-cycle-sound.md) | 🟡 Feature | ⚠️ Mostly done | Ride SFX: wrong-sound bugs fixed; **global sound registry decoded + implemented** (`RideSoundBank`: id = concatenated-bank index, verified); EVENT types 1&2 play the ride's real sounds (creaks) via it. EVENT 3-9 / EventMap-COAST / ADDOBJ triggering + 3D positioning remain |
| [T-038](T-038-park-management-ui.md) | 🟡 Feature | 🗂️ Umbrella | Park management & build mode — split into T-040–T-045 (mode/ACTION state machine RE'd from `tp.exe`) |
| [T-039](T-039-peep-needs-staff-depth.md) | 🟡 Feature | ⚠️ Core done | Peep needs & staff depth: thirst + drink stalls, vandalism by unhappy peeps that **guards (now patrolling toward trouble) measurably deter** (verified 21/62 stopped), balanced long-run economy; toilets, ride ratings/thoughts, ride breakdown+mechanics remain |
| [T-040](T-040-build-mode-foundation.md) | 🟡 Feature | ✅ Core done | Build/manage **foundation**: controllable in-park camera + mouse→tile picking + highlight + click dispatch (verified in-game) — blocks T-041–T-045 |
| [T-041](T-041-ride-shop-placement.md) | 🟡 Feature | ⚠️ Core done | Ride & shop placement + a **clickable build/manage UI** (`HudPanel` base; `BuildPanel` catalog — all items mouse-selectable, fixes the >9 cap; `ManagePanel` — fee/loan/price/research buttons; blocks world-clicks over panels) + **lobby↔in-park HUD split** (`Level.InPark` — the front-end menu no longer draws over the park); footprint preview + cost charging + queues; rotation/sell remain |
| [T-042](T-042-economy-controls-loans.md) | 🟡 Feature | ⚠️ Core done | Economy controls: settable ride prices + admission fee, loans (take/repay/monthly/bankruptcy), HUD readout (verified); clickable panel+graph remain |
| [T-043](T-043-staff-management.md) | 🟡 Feature | ⚠️ Core done | Staff hire+place via catalog (entertainer/handyman/guard/researcher), charged + wages (verified); fire/patrol-zones remain |
| [T-044](T-044-research-upgrades.md) | 🟡 Feature | ⚠️ Core done | Research + ride capacity upgrades: full `Upgrades[*]` parsed, researchers advance research, apply bumps live capacity (verified); per-ride UI remains |
| [T-045](T-045-coaster-track-editor.md) | 🟡 Feature | ⚠️ Slices 1–3b | Coaster: station + track-laying tool closing into a loop at the `<` entry, rendered with a **rail+sleeper profile** (bed + two raised rails + cross-ties, real Trak_sec texture) on height-aware pylons, with a train of real CrocCar.MD2 cars (animated) gliding it; **real peeps board the train and ride it in view, the rider scream plays while occupied, and `STACKUP/DOWN` (`PageUp`/`PageDown`) builds hills** (verified) — only `.hmp`-exact spacing + segment rotation remain (nice-to-have) |

Priority legend: 🔴 blocking · 🟠 important · 🟡 desirable/feature · ⚪ technical debt/polish.
Status legend: ✅ done · ⚠️ partial · ☐ to do · 🗂️ split into focused tickets · ⏸️ deferred.
