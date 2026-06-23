# T-048 — Ride node geometry: authored car paths + walk-node & head-node placement

- **Priority**: 🟡 Feature
- **Type**: Engine / reverse engineering
- **Status**: ☐ To do
- **Parent**: [T-032](T-032-ride-engine.md) (ride engine — this is the "node geometry" tail).
- **Related**: [T-007](T-007-vm-opcodes-rse.md) (WALKON/ADDHEAD/TOUR/BUMP), [T-047](T-047-ride-event-3d-sound-particle-pools.md).

## Context

Several ride features are modelled as pure VM/engine bookkeeping but lack their **visual placement**
because the ride model's named **node graph** (walk nodes, head nodes, tour/car path nodes, particle
nodes) isn't decoded. Today:

- **Car rides** (TOUR/BUMP scripts) show a generic `RideVehicle` looping a *procedurally generated
  ellipse* around the footprint, not the ride's authored car path.
- **WALKON** peeps and **ADDHEAD** heads update slot tables but aren't placed at the real walk/head nodes.
- **EVENT** effects (T-047) and the car engine want the same node positions (`FUN_00556b90`).

## Scope

1. Decode the ride model's node graph (the type-`0x80` objects probed at spawn — `FUN_005587f0` for
   head-node count `+0x4c`; the analogous walk/path/particle node lists). Map node name/index → local
   transform.
2. Drive `RideVehicle` (and the coaster train where applicable) along the **authored** car/tour path
   instead of the ellipse; place WALKON peeps gliding between real walk nodes and ADDHEAD heads at head
   nodes.
3. Feed the node positions to T-047's 3D effect placement.

## Acceptance criteria

- A tour ride's car follows the ride's authored path; a walking peep (WALKON) glides between the ride's
  real walk nodes; heads (ADDHEAD) appear at head nodes.

## Affected files

`source/OpenTPW/World/Rides/RideVehicle.cs`, `RideEngine.cs`, `RideVM.Walk.cs`/`RideVM.Heads.cs`,
`source/OpenTPW.Files/Formats/Model/ModelFile.cs` (node-graph access).
