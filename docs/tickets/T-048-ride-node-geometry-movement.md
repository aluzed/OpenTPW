# T-048 — Ride node geometry: authored car paths + walk-node & head-node placement

- **Priority**: 🟡 Feature
- **Type**: Engine / reverse engineering
- **Status**: ⚠️ Partial — the **node graph is decoded structurally** (type + id per node, exposed in
  `ModelFile.Nodes`, unit-tested + confirmed on real ride models). Node **world positions** bind to bone
  transforms at runtime (not stored in the node entry), which is the remaining decode; the visual
  consumer work (car paths / WALKON / ADDHEAD placement) then follows and needs the renderer.
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

## Done (node table decoded — Ghidra + ModelFile)

RE'd the node graph from the MD2 loader + the runtime resolver `FUN_0044b220` / the EVENT positioner
`FUN_00556b90`:

- **File layout**: node **count** = `u16` at header **0x48**, node **table** = `u32` file offset at
  header **0x7c**, **0x14 bytes/node** = `{u32 typeMask, u32 nodeId, u32 extra, u32 ptrA(reloc @+0xc),
  u32 ptrB(reloc @+0x10)}`. Decoded in `ModelFile.ParseNodeTable` → `ModelFile.Nodes` (unit-tested,
  `ModelFileTests.ParsesNodeTable`).
- **Resolution**: the VM finds a node by `(TypeMask & selector) != 0 && NodeId == requested`
  (`FUN_0044b220`; `0x3da1f83` is the all-types mask). So `TypeMask` is a **bitfield of which subsystems
  may address the node** and `selector` is the requesting opcode's node-type. Confirmed on real models:
  `Bird.MD2` (tour ride) = 11 nodes — one `0x131`, nine `0xB1` (ids 1-9, the car/seat ring), one `0x1031`;
  `gokarts` = 3; `coaster1` = 9 incl. four `0x811` + several `0x*400031` (the high car/track bits).
- **Positions** are NOT in the node entry — `ptrA/ptrB` are **null in every shipped model**; at runtime the
  node binds to a **bone transform** and the position is that matrix's translation row (`FUN_00556b90`
  reads `transform+0x30/0x34/0x38`, direction at `+0x20/0x24/0x28`).

## Done (node types labelled) + key finding on positions

- **Node-type selectors RE'd** (each subsystem calls `FUN_0044b220(model, selector, id)`; a node matches
  when `TypeMask & selector != 0`): **0x80 = object/head attach** (`FUN_005587f0` head-mount + the
  TOUR/BUMP car helpers), **0x800 = walk node** (`FUN_00557ab0`; the coaster's `0x811` queue nodes),
  **0x100 = bumper/kart car** (`FUN_0054a040`); other bits (0x400/0x1000/0x20000/0x400000) belong to
  peep/scenery/other subsystems. Exposed as `Node.IsObject/IsWalk/IsCar`, `Node.Matches(selector)`,
  `ModelFile.NodesMatching(selector)` + `ModelFile.NodeSelector` constants (unit-tested). Confirmed: Bird's
  nine `0xB1` nodes (ids 1-9) are object/head attach points (its riders), the coaster's `0x811` are walk.
- **Key finding — node positions are NOT static file data.** In every shipped model the node entries'
  transform pointers (`+0xc/+0x10`) are **null**, there is **no bone table** (`ptr@0x98 = ptr@0xac = 0`,
  `count@0x40 = 0`), and Bird has **1 mesh but 11 nodes** — so positions can't come from the file or the
  meshes. `FUN_00556b90` reads a node's position from a **bone transform bound at runtime** (translation at
  matrix +0x30); that binding is produced by the **skeletal animation + the ride's motion VM**
  (TOUR/BUMP/COAST move the car/seat nodes each frame). So node world positions are **simulation output**,
  not a decode — they fold into T-032's "authored car-physics subsystem" + the T-033 skeleton, not this
  file format.

## Remaining

1. **Node world positions** require the ride **motion/skeleton simulation** (T-032 car-physics + T-033
   bone transforms), since they're not stored statically — re-scoped out of the "file decode" framing.
2. Apply (once positions exist + a working renderer): drive `RideVehicle`/the coaster train along the car
   nodes; place WALKON peeps & ADDHEAD heads at their nodes; feed positions to T-047.

## Acceptance criteria

- A tour ride's car follows the ride's authored path; a walking peep (WALKON) glides between the ride's
  real walk nodes; heads (ADDHEAD) appear at head nodes. *(Foundational node-table decode done; the above
  await the bone-transform decode + a working renderer to verify.)*

## Affected files

`source/OpenTPW/World/Rides/RideVehicle.cs`, `RideEngine.cs`, `RideVM.Walk.cs`/`RideVM.Heads.cs`,
`source/OpenTPW.Files/Formats/Model/ModelFile.cs` (node-graph access).
