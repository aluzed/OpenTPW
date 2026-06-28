# T-046 — Render the real advisor character (3D bug-head + visemes + messages)

- **Priority**: 🟡 Feature
- **Type**: Engine / rendering
- **Status**: ⚠️ Implemented — functionally verified (model + 5 visemes + lip-sync + speech all confirmed
  in logs); the **message system (`Advisor.sam`) is now parsed + wired** (pacing/group rules drive which tip
  speaks — see below). The on-screen placement/scale/orientation still needs a visual pass (the screenshot
  tooling was unavailable). Per-event score formulas + per-message clips remain.
- **Parent**: [T-020](T-020-lip-mouth-shapes.md) (lip-sync format + wiring done — this is the render tail).
- **Related**: [T-031](T-031-game-audio.md).

## Context

T-020 decoded the `.LIP` lip-sync, RE'd the five visemes → advisor mesh-part names
(`mouth - normal/aah/eee/ooh/sss`, from `FUN_0044b2e0`), and wired a working lip-sync demo
(`AdvisorPanel`, `OPENTPW_ADVISOR_DEMO=1`) that plays a real speech clip and drives a **procedurally
drawn** mouth in sync. What remains is rendering the *real* advisor.

## Scope

1. Load the advisor model from `global/advisor.wad` (`Advisor.MD2` "Bug Head" + the `Advisorm1..m18.MD2`
   face/morph parts) and render it in its proper on-screen position.
2. Show the matching named sub-mesh per viseme (`MouthShape.MeshPartName()`) — hide the others — driving
   it from `LipSyncFile.ShapeAt` (already wired), replacing the demo's procedural mouth.
3. Hook the advisor **message system** (`Advisor/Advisor.sam`: `MessageGroups`, min-time/say-once/
   discard-after-slaps rules) so tips fire on real events (tutorial, golden-ticket, research, congrats)
   with the right speech clip + its `.LIP`.

## Done (so far)

- **Resolved the model structure.** `Advisor.MD2` ("Bug Head") loads cleanly through our `ModelFile`
  (25 named sub-meshes) and **contains the five viseme mouths as named meshes** exactly matching the
  RE'd selector — `Mouth - Normal/Aah/Eee/Ooh/Sss` (textures `Mouth1a..e.wct`) — plus `Bug Head`, eyes,
  `ShutEye` (blink), `Body`, antennae, hands, spatula and the context **hats** (Ticket/Kiosk/Bowler/
  Security/Handyman/Research/Fast Food/Hardhat). So it's **mesh-part swapping**, not the
  `Advisorm*.MD2` files (which are a separate sub-format we don't need).
- **`Advisor` world entity** (`source/OpenTPW/World/Advisor.cs`): builds a `ModelEntity` per relevant
  mesh (the bug face + the five mouths), anchors the whole assembly upright in front of / facing the
  camera each frame, shows only the active viseme's `Mouth - *` mesh (hides the other four), and plays a
  real `sp_001.mp2` clip driving the viseme from its `.LIP` via `ShapeAt`. `AdvisorPanel` is now a thin
  HUD label. Enabled by `OPENTPW_ADVISOR_DEMO=1`.
- **Verified (functional, in-game logs):** `Advisor.MD2 loaded: 25 mesh(es)` → `built 11 part(s)` (6 base
  + 5 visemes) → `speaking 'sp_001.mp2', 35 keyframes, 28.6s`, no exceptions.

## Done (this pass — the `Advisor.sam` message system)

The advisor's tip pacing/selection is now decoded + wired, so the **real `Advisor.sam` rules** decide when and
which message the advisor speaks (not an unconditional loop):

- **`AdvisorConfig`** (`source/OpenTPW/World/`): parses `Advisor/Advisor.sam` — the EA text key/value format,
  reusing the existing `SettingsFile`/`SAMParser` (no new parser). Exposes the global pacing
  (`MinTimeAnyMessage`/`MinTimeSameMessage`/`MinScoreForConsideration`), the per-group rules
  (`MessageGroups[N]` → `{MinTimeSameMessage, SayOnlyOnce, DiscardAfterSlaps}`, sparse 0-5+9), and a generic
  `Param(advice, key)` accessor over the ~200 per-advice scoring params for the future score formulas.
  Fault-tolerant: every key has a documented default, so a missing/stripped file still yields a usable config.
- **`AdvisorMessages`** scheduler (pure, time-injected): each tick the game `Submit`s candidate tips
  (id + group + score); `Consider(now)` picks the single best one enforcing the named rules — global min-gap,
  per-group same-message gap, say-once, min-score, and `Slap`/`DiscardAfterSlaps` retirement. Fully unit-tested.
- **Wired into `Advisor`**: it loads the config + builds the scheduler, and the demo now feeds two built-in
  candidates (a say-once `WelcomeTutorial` in tutorial group 1, a repeating `GeneralTip` in group 0) and speaks
  whatever the scheduler returns — so the visible behaviour is governed by the real rules. The id→clip mapping
  is by convention with a fallback to the shipped `sp_001` clip (we don't ship per-message advisor clips).
- **Unit-tested** (`AdvisorMessagesTests`, 10 tests: config globals/groups/advice parse incl. trailing-comment
  stripping + float params + missing-file defaults; highest-score-wins, min-score, global gap, same-message
  gap, say-once, discard-after-slaps, zero-discard). 180 tests pass, 0 new warnings.
- **Verified in-game** (`OPENTPW_ADVISOR_DEMO=1`, real assets): `message config: 7 group(s), minGap 5s,
  minScore 25` (parsed the real file), then `message 'WelcomeTutorial' → speaking 'sp_001.mp2'` — the say-once
  welcome fired first by score and drove real speech + lip-sync; 0 exceptions.

## Remaining

- **Visual pass**: confirm/tune the on-screen position, scale and facing (anchoring constants in
  `Advisor.cs`) — not done this session (screenshot tooling unavailable).
- **Per-event triggers + score formulas**: feed real game state (research available, in-the-red, congrats,
  thirsty/hungry visitors, …) into `AdvisorMessages.Submit` using the `AdvisorConfig.Param` formulas, and
  map each message to its own speech clip + `.LIP` (the clips aren't shipped per-message yet).
- Textures: the `.wct` are loaded from `global/advisor/textures/` via the VFS; verify they resolve (else
  the advisor renders untextured — still geometrically correct).
- Hook the **message system** (`Advisor.sam`: `MessageGroups`, min-time/say-once/discard-after-slaps) so
  tips fire on real events with the right clip, and pick the matching **hat** per advisor role.
- Exact runtime viseme selection (the `.LIP` only marks phoneme boundaries; `ShapeAt` cycles the five) —
  traceable from the speech-update caller if precise per-interval visemes matter.

## Acceptance criteria

- The advisor character renders on-screen and visibly mouths a speech clip with the correct viseme parts.

## Affected files

`source/OpenTPW/World/AdvisorConfig.cs` (new — `Advisor.sam` parser), `source/OpenTPW/World/AdvisorMessages.cs`
(new — message scheduler/rules), `source/OpenTPW/World/Advisor.cs` (loads the config + drives speech through
the scheduler), `source/OpenTPW.Tests/AdvisorMessagesTests.cs` (new). Earlier passes:
`source/OpenTPW/UI/Widgets/AdvisorPanel.cs`, the model/render pipeline, `ModelFile.cs` (named-part access).
