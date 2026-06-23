# T-046 — Render the real advisor character (3D bug-head + visemes + messages)

- **Priority**: 🟡 Feature
- **Type**: Engine / rendering
- **Status**: ☐ To do
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

## Open questions

- Whether the `Advisorm*.MD2` parts are separate meshes assembled at runtime or named sub-meshes of
  `Advisor.MD2` — the `FUN_0044b2e0` string-match suggests named parts; confirm against the loaded model.
- Exact runtime viseme selection (the `.LIP` only marks phoneme boundaries; `ShapeAt` currently cycles
  the five) — can be traced from the speech-update caller if precise per-interval visemes matter.

## Acceptance criteria

- The advisor character renders on-screen and visibly mouths a speech clip with the correct viseme parts.

## Affected files

`source/OpenTPW/UI/Widgets/AdvisorPanel.cs` (or a new `World/Advisor.cs`), the model/render pipeline,
`source/OpenTPW.Files/Formats/Model/ModelFile.cs` (named-part access if needed).
