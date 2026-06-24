# Tickets — OpenTPW backlog

Tickets derived from the 2026-06-15 analysis (build + tests run on Linux with
.NET 8.0.422). See [../README.md](../README.md) for context.

> Note: the `origin` remote points at the **upstream** repo `OpenTPW/OpenTPW`.
> These are local ticket files; convert them to GitHub issues on **your fork** if
> needed (do not open issues directly on upstream).

## Build / test state (observed)

- **Build**: ✅ `dotnet build OpenTPW.sln` → 6 projects, **0 errors** (T-009).
- **Tests**: ✅ `dotnet test` → **0 failed, 101 passed, 17 inconclusive/ignored** on a clean Linux
  machine. The inconclusive ones are integration tests that need a game install (`OPENTPW_GAMEPATH`)
  or a real asset sample (`TPW_VIDEO_SAMPLE`, `TPW_FONT_SAMPLE`, `TPW_MODEL_SAMPLE`, `TPW_MAP_SAMPLE`,
  `TPW_PLB_SAMPLE`, `TPW_MTR_SAMPLE`, `TPW_LIP_SAMPLE`). All pass when the samples are provided.

## Remaining work (open tickets)

All format/VM decoding and the core gameplay loop are done. Open work falls into:
- **Active feature tails** (actionable now): **T-046** advisor render · **T-047** EVENT 3D sound + particle
  pools · **T-048** ride node geometry/movement · **T-052** coaster `.hmp`/rotation.
- **Blocked / deferred** (need an external sample or have no consumer): **T-017** (`SAD_*` save stream —
  no sample), **T-022** (EA-ADPCM mono waveform — no sample), **T-019** (`.PLB` per-effect field labels —
  no particle system to consume them; needs a dynamic capture), **T-021** (`.TQI` exact dequant — decoder
  already renders correctly).

## Index

| # | Priority | Status | Title |
|---|----------|--------|-------|
| [T-001](T-001-backslash-paths-linux.md) | 🔴 High | ✅ Done | Hardcoded `\` paths break everything on Linux |
| [T-002](T-002-tests-absolute-paths.md) | 🔴 High | ✅ Done | Tests: hardcoded absolute paths + dependency on a game install |
| [T-003](T-003-naudio-not-portable.md) | 🟠 Medium | ✅ Done | NAudio (audio) not portable off Windows |
| [T-004](T-004-system-drawing-modkit.md) | 🟠 Medium | ✅ Mostly | `System.Drawing.Common` is Windows-only in the ModKit |
| [T-005](T-005-vulnerable-dependencies.md) | 🟠 Medium | ✅ Done | Vulnerable dependencies (direct + transitive) |
| [T-006](T-006-gamepath-config.md) | 🟡 Low | ✅ Done | Windows default `GamePath` + no portable override |
| [T-007](T-007-vm-opcodes-rse.md) | 🟡 Feature | ✅ Done | Ride VM: **106/106 (100%)** — Batch A (43 pure) + all of Batch B (objects incl. ADDOBJ_EXT/anim/`WAIT*`/sound/scream, limbo/cross-VM/walk/head pure-VM families, TURBO, the **light** & **particle** (.PLB-driven) subsystems, and the three car-object multiplexers **COAST/TOUR/BUMP**, RE'd as fixed 2-operand `(sub,arg)` switches; queries return 0 / commands no-op without a car engine — the faithful `0x4a454647`-magic-absent path) |
| [T-008](T-008-unimplemented-formats.md) | 🟡 Feature | 🗂️ Split | Umbrella (closed): `.BF4` ✅, `.TQI/.TGQ` ✅; remainders → T-018/019/020/021/022 |
| [T-009](T-009-build-warnings.md) | ⚪ Debt | ✅ Done | build warnings (105 → 0: nullable, Dispose, dead code) |
| [T-010](T-010-add-sub-flags.md) | 🟠 Medium | ✅ Done | ADD/SUB don't set arithmetic flags (branch correctness) |
| [T-011](T-011-branchto-hardening.md) | 🟡 Feature | ✅ Done | Harden `RideVM.BranchTo` (O(1) map; verified by a compiled loop) |
| [T-012](T-012-partial-formats.md) | 🟡 Feature | 🗂️ Split | Umbrella (closed): `.MD2` animated ✅, `.MAP` BANK names ✅; remainders → T-015/016/017 |
| [T-013](T-013-ci-pipeline.md) | 🟠 Medium | ✅ Done | Add CI (build + test on Linux) |
| [T-014](T-014-case-insensitive-assets.md) | 🟠 Medium | ✅ Done | Case-insensitive asset path resolution (Linux) |
| [T-015](T-015-md2-static-variant.md) | 🟡 Feature | ✅ Done | `.MD2` version gate (0xDD/0xCB) Ghidra-confirmed; **legacy static variant (GARROW/RARROW = 0x18/0x17) decoded** — 2-byte-packed header, 32B verts (pos/normal/uv), 24B faces (indices @+2/+4/+6); verified against both real files + a synthetic sample |
| [T-016](T-016-map-entry-records.md) | 🟡 Feature | ✅ Decoded | `.MAP`: variant + BANK names + SFX category header + **SFX per-sound 20-byte records** decoded; BANK records RE'd as serialized pointers (not data); only the SFX mixing-curve blob stays raw |
| [T-017](T-017-tpws-saves.md) | 🟡 Feature | ⚠️ Partial | `.TPWS`: container Ghidra-corrected (leading bytes = **version 500**, not magic; full header layout) + read + **write/round-trip**; inner `SAD_*` module stream stays opaque, real sample still unavailable |
| [T-018](T-018-mtr-material-semantics.md) | 🟡 Feature | ✅ Done | `.MTR` not runtime-used (Ghidra); `.MD2` carries texture binding (decoded + tested) |
| [T-019](T-019-plb-parameter-fields.md) | 🟡 Feature | ⚠️ Partial | `.PLB`: **layout Ghidra-confirmed & fully decoded** (8-byte header fix + the trailing block is a 2nd 20×104 table + density/total globals, all typed; whole file accounted for). Per-effect param **field labels** still need the consumer traced |
| [T-020](T-020-lip-mouth-shapes.md) | 🟡 Feature | ⚠️ Mostly done | `.LIP` semantics resolved (Ghidra): shapes **not in the file** — engine has 5 visemes (`FUN_0044b2e0`), picked per keyframe interval; viseme→advisor mesh-part names RE'd (`mouth - normal/aah/eee/ooh/sss`). **Lip-sync wired to real speech**: `AdvisorPanel` (`OPENTPW_ADVISOR_DEMO=1`) plays `sp_001.mp2` + drives a viseme mouth from its `.LIP` in sync (verified in-game). Only rendering the real advisor model's named sub-meshes remains |
| [T-021](T-021-tqi-exact-dequant.md) | ⚪ Polish | ⏸️ Deferred | `.TQI`: float AAN IDCT confirmed (Ghidra); exact port deferred (decoder already renders correctly) |
| [T-022](T-022-ea-adpcm-mono.md) | 🟡 Feature | ⚠️ Implemented | EA-ADPCM **mono** path added (channel dispatch + `DecodeScdlMono`, two samples/byte per FFmpeg adpcm_ea) + synthesised test; waveform verification awaits a real mono sample (none in install) |
| [T-023](T-023-linux-vulkan-libdl.md) | 🔴 High | ✅ Done | Linux runtime: Vulkan `libdl` load fix — game now boots on Linux |
| [T-024](T-024-linux-black-screen.md) | 🟠 Medium | ✅ Done | Linux "black screen" was the synchronous load; lobby renders (loading screen + text) |
| [T-025](T-025-bf4-antialiased-fonts.md) | 🟢 Low | ✅ Done | `.BF4`: all three glyph encodings decoded (1bpp / raw-4bpp / **compressed-4bpp** = a nibble-RLE, RE'd from tp.exe's font decompressor `FUN_006b4aa0`); coverage→alpha in `FontAtlas`; the UI's menu buttons now use the intended antialiased `MENUMED` face (verified in-game), unit-tested |
| [T-026](T-026-render-resource-churn.md) | 🔴 High | ✅ Done | Renderer: killed per-frame GPU resource churn (sync submit + ephemeral sets) → lobby at 60fps |
| [T-027](T-027-ui-draw-batching.md) | 🟠 Medium | ✅ Done | Renderer: UI draws batched (merge same-texture), per-quad allocs + set-churn removed |
| [T-028](T-028-frame-cpu-hygiene.md) | ⚪ Debt | ✅ Done | Renderer: per-frame CPU hygiene (dirty-shader registry, `Stopwatch`) |
| [T-029](T-029-native-render-loop-re.md) | 🟢 Low | ✅ Done | Native render loop RE'd (DDraw + D3D execute buffers + MMX software); see docs/07 |
| [T-030](T-030-async-level-load.md) | 🟠 Medium | ⚠️ Mostly | Level load: freeze resolved (per-step + per-mesh progress); only optional 60fps async remains |
| [T-031](T-031-game-audio.md) | 🟡 Feature | ⚠️ Mostly | Game audio: lobby music (minimp3), UI click SFX, music volume keys, cross-platform build; ambience/settings-UI remain |
| [T-032](T-032-ride-engine.md) | 🟡 Feature | ⚠️ In progress | Ride engine: seam + lifecycle + keyframe anim + scream + boarding bridge, and now backs **all 106/106 VM opcodes** (lights, particles/.PLB, limbo, walk, heads, and the COAST/TOUR/BUMP car-object multiplexers) + **ride breakdown & a mechanic** that repairs them + **visible moving cars** for tour/go-kart/water/bumper rides (`RideVehicle`, occupancy-driven); a full authored car-physics subsystem + walk-node visuals remain |
| [T-033](T-033-ride-animation-keyframes.md) | 🟡 Feature | ✅ Core done | Ride animation: rotation + translation/scale + vertex-morph keyframes all RE'd and driven from real ride data, verified in-game; polish remains — see docs/08 |
| [T-034](T-034-peeps.md) | 🟡 Feature | ⚠️ Mostly | Peeps: full crowd loop — real animated `esprites.wad` sprites (TPC codec RE'd), directional walk cycles, queueing, riding, needs, economy, staff (entertainers/handymen/guards); polish split → T-035–T-039 |
| [T-035](T-035-peep-sprite-polish.md) | ⚪ Polish | ✅ Done | Peep/staff sprite polish: camera-relative facing (`SpriteFacing`, unit-tested), idle standing pose, hotspot-anchored pixel-unit quads (no scale jitter; fixed a `TpcFile` hotspot-offset bug), procedural ground shadow; `.FPC` identified as an alternate full sprite set (not a shadow) |
| [T-036](T-036-peep-pathfinding.md) | 🟡 Feature | ⚠️ Core done | Peep pathfinding: `PathGraph` A* over the `PlacementGrid` (ride/shop footprints block, queue paths walkable) — peeps route around rides instead of straight lines (unit-tested + verified in-game); water-avoidance + real gate node wait on the real level terrain |
| [T-037](T-037-ride-cycle-sound.md) | 🟡 Feature | ⚠️ Mostly done | Ride SFX: wrong-sound bugs fixed; **global sound registry decoded + implemented** (`RideSoundBank`: id = concatenated-bank index, verified); **whole EVENT type switch decoded** (`FUN_005573d0`) — types 1&2 play real positioned sounds (creaks), types 3-10 are **particle effects** rendered via the decoded `.PLB` proxy (Fire/Sparks/Destroy2-4/LaserFWexplode, verified in-game; the old "type-3 = wrong sounds" was a misread — `code` is a particle index). Per-pool `.PLB` mapping + `COAST`/`BUMP`/`ADDOBJ` residue + 3D positioning remain |
| [T-038](T-038-park-management-ui.md) | 🟡 Feature | 🗂️ Umbrella | Park management & build mode — split into T-040–T-045 (mode/ACTION state machine RE'd from `tp.exe`) |
| [T-039](T-039-peep-needs-staff-depth.md) | 🟡 Feature | ⚠️ Core done | Peep needs & staff depth: thirst+drink, **bladder+toilets** (free facility; verified peeps visit), vandalism that **patrolling guards measurably deter**, balanced economy; ride ratings/thoughts + ride breakdown+mechanics remain |
| [T-040](T-040-build-mode-foundation.md) | 🟡 Feature | ✅ Core done | Build/manage **foundation**: controllable in-park camera + mouse→tile picking + highlight + click dispatch (verified in-game) — blocks T-041–T-045 |
| [T-041](T-041-ride-shop-placement.md) | 🟡 Feature | ✅ Done | Ride & shop placement + a **clickable build/manage UI** (`HudPanel`/`BuildPanel`/`ManagePanel` — catalog, fee/loan/price/research buttons), **lobby↔in-park HUD split**, **sell/demolish** rides+shops (full teardown, 50% refund), and **rotation** (R, 90° steps — `RideShape.Rotated` + mesh yaw); footprint preview + cost charging + queues. Nice-to-haves only (drag-rotate, shop price) |
| [T-042](T-042-economy-controls-loans.md) | 🟡 Feature | ✅ Core done | Economy controls: settable ride prices + admission fee, loans (take/repay/monthly/bankruptcy), HUD readout (verified); clickable income/expense **graph done in T-049** (F11) |
| [T-043](T-043-staff-management.md) | 🟡 Feature | ✅ Core done | Staff hire+place via catalog (entertainer/handyman/guard/researcher), charged + wages (verified); **fire + patrol-zones done in T-049** |
| [T-044](T-044-research-upgrades.md) | 🟡 Feature | ⚠️ Core done | Research + ride capacity upgrades: full `Upgrades[*]` parsed, researchers advance research, apply bumps live capacity (verified); per-ride UI remains |
| [T-045](T-045-coaster-track-editor.md) | 🟡 Feature | ⚠️ Slices 1–3b | Coaster: station + track-laying tool closing into a loop at the `<` entry, rendered by **sweeping the authored cross-section profile decoded from `coaster.sam`** (the real channel/rail silhouette) along the spline, on height-aware pylons, with a train of real CrocCar.MD2 cars (animated) gliding it; **real peeps board the train and ride it in view, the rider scream plays while occupied, and `STACKUP/DOWN` (`PageUp`/`PageDown`) builds hills** (verified) — only `.hmp` curved-piece meshes + segment rotation remain (nice-to-have) |
| [T-046](T-046-advisor-character.md) | 🟡 Feature | ⚠️ Implemented | Real **advisor character**: `Advisor.MD2` ("Bug Head", 25 meshes) loads + the five viseme mouths are named sub-meshes (`Mouth - Normal/Aah/Eee/Ooh/Sss`); new `Advisor` entity assembles the bug face, anchors it in front of the camera, shows the active viseme mesh + plays `sp_001.mp2` driving it from the `.LIP` (functionally verified in logs — `built 11 parts`). Remaining: a **visual pass** (placement/scale/facing) + the `Advisor.sam` message system |
| [T-047](T-047-ride-event-3d-sound-particle-pools.md) | 🟡 Feature | ⚠️ RE advanced | Corrected the model: **one** particle library (`Tp2.plb` 0..104 per `par_lib.h`; OOR codes are sentinels), and the 7 EVENT "pools" are **sound categories** (`cat_ambient/rides/kids/ui/staff/speech`) via the unified manager `DAT_00802bcc` → **EVENT types 5-9 are category sounds, 3-4 particles** (T-037 routed all to particles). Effects are node-positioned (T-048). Splitting the routing + real positions need in-game verify (blocked by display) |
| [T-048](T-048-ride-node-geometry-movement.md) | 🟡 Feature | ⚠️ Partial | Ride **node graph decoded** (count@0x48, table@0x7c, 0x14B/node) + **node types labelled** (selectors RE'd: 0x80 object/head, 0x800 walk, 0x100 car) — `ModelFile.Nodes`/`NodesMatching`/`Node.IsObject/IsWalk/IsCar`, unit-tested, confirmed on real models. **Finding: node positions are NOT static file data** (null transform ptrs, no bone table) — they're runtime simulation output (bone skeleton + ride motion VM), so positions re-scope into T-032/T-033, not a file decode |
| [T-049](T-049-management-ui-depth.md) | 🟡 Feature | ✅ Core done | Management UI depth: **F11 finance graph** (`ParkFinances.History` — per-month income/expense bars, capped ring buffer) + **staff fire & patrol zones** (`Staff.Fire`, pure `PatrolZone` bounding wander + job-seeking; `BuildMode.SelectedStaff` + `ManagePanel` FIRE/ZONE±/SET-ZONE/FREE-ROAM row). Per-ride price + research/upgrade were already clickable (T-042/044). Unit-tested (`ManagementDepthTests`). Draggable zone + per-ride window remain (nice-to-have) |
| [T-050](T-050-peep-simulation-depth.md) | 🟡 Feature | ✅ Done | **Ride ratings & thoughts** (`Peep.RateRide` → satisfaction + `RideThought`, feeding mood / `Ride.Rating` reputation / rating-weighted ride choice), **water-aware pathfinding** (`PlacementGrid` water layer — impassable & unbuildable; A* routes around lakes), and the **real entrance gate** (`Level.ReadEntranceTile` from `FixedItemInfo.EntranceA/B` — peeps enter/leave through the gate) — all unit-tested |
| [T-051](T-051-audio-polish.md) | 🟡 Feature | ✅ Core done | Audio polish: **looping ambient bed** (`AmbientHD.sdt` under the music, on its own source) + **persistent `GameSettings`** (JSON, fault-tolerant, clamped) + a **dedicated speech bus** (advisor lines) + an **F10 options overlay with draggable music/SFX/speech sliders** (persist on release; `-`/`+` keys persist too). Unit-tested (`GameSettingsTests`). Per-area ambience selection remains (nice-to-have) |
| [T-052](T-052-coaster-track-polish.md) | 🟢 Low | ⚠️ Partial | **`.hmp` decoded** — general footprint/height template (cols×rows, per-tile 5×5 code sub-grids, code grid + footprint grid; magic `0x0005AB1E`), used game-wide (coaster/queue/fence/sideshow/upgrade). New `HmpFile` parser, unit-tested + verified on real files. **`.hmp` footprints now drive placement**: new `PlacementFootprint` mask (`Rectangle`/`FromHmp`) + masked `PlacementGrid.CanPlace/TryPlace/Clear` reserving only solid tiles (passable cells stay walkable/buildable), wired through `CommitPlacement` (optional `BuildCatalogItem.HmpPath`), unit-tested (`PlacementFootprintTests`). Remaining: curved-piece meshes + per-segment rotation (renderer-blocked) |

Priority legend: 🔴 blocking · 🟠 important · 🟡 desirable/feature · ⚪ technical debt/polish.
Status legend: ✅ done · ⚠️ partial · ☐ to do · 🗂️ split into focused tickets · ⏸️ deferred.
