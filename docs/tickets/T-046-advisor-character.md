# T-046 — Render the real advisor character (3D bug-head + visemes + messages)

- **Priority**: 🟡 Feature
- **Type**: Engine / rendering
- **Status**: ⚠️ Implemented — functionally verified (model + 5 visemes + lip-sync + speech all confirmed
  in logs); the on-screen placement/scale/orientation still needs a visual pass (the screenshot tooling
  was unavailable this session). The message system (`Advisor.sam`) is not wired yet.
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

## Remaining

- **Visual pass**: confirm/tune the on-screen position, scale and facing (anchoring constants in
  `Advisor.cs`) — not done this session (screenshot tooling unavailable).
- Textures: the `.wct` are loaded from `global/advisor/textures/` via the VFS; verify they resolve (else
  the advisor renders untextured — still geometrically correct).
- Hook the **message system** (`Advisor.sam`: `MessageGroups`, min-time/say-once/discard-after-slaps) so
  tips fire on real events with the right clip, and pick the matching **hat** per advisor role.
- Exact runtime viseme selection (the `.LIP` only marks phoneme boundaries; `ShapeAt` cycles the five) —
  traceable from the speech-update caller if precise per-interval visemes matter.

## Acceptance criteria

- The advisor character renders on-screen and visibly mouths a speech clip with the correct viseme parts.

## Affected files

`source/OpenTPW/UI/Widgets/AdvisorPanel.cs` (or a new `World/Advisor.cs`), the model/render pipeline,
`source/OpenTPW.Files/Formats/Model/ModelFile.cs` (named-part access if needed).
