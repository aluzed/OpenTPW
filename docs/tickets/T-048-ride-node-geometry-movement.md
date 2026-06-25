# T-048 — Ride node geometry: authored car paths + walk-node & head-node placement

- **Priority**: 🟡 Feature
- **Type**: Engine / reverse engineering
- **Status**: ⚠️ Partial — the **node graph is decoded structurally** (type + id per node, exposed in
  `ModelFile.Nodes`, unit-tested + confirmed on real ride models). Node **world positions** bind to bone
  transforms at runtime (not stored in the node entry), so they're **simulation output, not a decode**.
  A **runtime node→world-position resolver** (`RideNodePositions`) now supplies them — car/seat nodes from
  the live vehicle path, the rest from a deterministic footprint layout — and **EVENT effects +
  REPAIREFFECT/SPARK now spawn at the addressed node** instead of the ride centre (T-047 #1). WALKON /
  ADDHEAD visual placement can use the same resolver but still needs the peep/head render path.
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

## Done (this pass — authored car/seat count drives the vehicle)

The node **count** is static file data even though the positions aren't, so the first visible consumer of
the decoded graph landed without needing the motion sim or a renderer: a car ride's `RideVehicle` now shows
**as many riders/cars as the model declares car/seat nodes** (object `0x80` + car `0x100`), instead of a
hardcoded four.

- `Ride.CarNodeCount` = count of the model's object/car nodes, captured at model load.
- `RideVehicle.SeatCountFor(authoredCarNodes)` clamps that to `[1, 12]` (default 4 when the model has no
  node graph); the seat array is sized from it and the riders **trail the lead car along the loop** (a
  train of cars) rather than four markers beside one box.
- Unit-tested (`RideVehicleTests`: count-from-graph via `ParseNodeTable`, clamping/fallback) — so e.g.
  Bird's nine seat nodes → nine riders, go-karts' three → three.

The loop is still the procedural ellipse (the real path needs node *positions*, below).

## Done (this pass — runtime node→world-position resolver + 3D effect placement)

Since node positions are simulation output, not file data, the missing layer was a **runtime resolver**
that supplies a world position per node id each frame — the seam every consumer (effects, sounds, the
vehicle, later WALKON/ADDHEAD) was missing. New `RideNodePositions` (`source/OpenTPW/World/Rides/`):

- **Two regimes.** *Moving* nodes (object `0x80` + car `0x100`) take a **live world position published by
  the `RideVehicle` each frame** — these are real (the car genuinely moves there). *Static* nodes
  (walk/head/particle) take a **deterministic footprint layout** (walk nodes ring the perimeter at ground
  level; other nodes sit on a raised inner ring), worldised by the ride's placement transform (origin +
  90°-step orientation + footprint size — the exact quarter-turn math `Ride.BuildMeshEntities` uses). The
  static layout is an honest engine-side **stand-in** (like the procedural path / the light/particle
  proxies), not decoded geometry — the authored positions don't exist statically.
- **Resolution order**: a published moving position wins; else the static layout (once the ride is placed
  via `Configure`); else unresolved → the caller falls back to the ride body. So a ride with no decoded
  node graph behaves exactly as before.
- **Wired into the effect path (closes T-047 #1).** `EVENT(type, node, code)` now passes its `node`
  operand through: particle effects spawn at `NodePosition(node)`, category sounds resolve + record the
  node position (for when the audio bus goes 3D). `REPAIREFFECT`/`SPARK` (op_93/op_105) pass their
  first operand (the node id — `FUN_00556b90`) to a new `SpawnParticleEffect(code, nodeId)` overload, so
  sparks fire at the addressed node (e.g. a moving coaster car) instead of dead-centre.
- **Vehicle publishes.** `RideVehicle` precomputes its car/seat node ids (`CarSeatNodeIds`) and publishes
  each seat's path position every frame, whether or not the seat is occupied (the node exists physically),
  while the visible marker still hides when empty.
- Unit-tested (`RideNodePositionsTests`: layout split walk-vs-inner, car/seat id selection, configured-vs-
  unconfigured resolution, moving-overrides-static, footprint scaling + placement rotation; new
  `RideEngineTests.ParticleOpcodesPassTheirTargetNode` for the REPAIREFFECT/SPARK passthrough).

## Done (this pass — WALKON / ADDHEAD visual placement)

The VM's WALKON/ADDHEAD families kept pure slot tables (`RideVM.Walk`/`RideVM.Heads`); the visual placement
was the gap. The **engine now mirrors those tables into the world each frame** from the node positions —
no VM/handler changes (the engine reads the read-only `WalkSlots`/`HeadSlots` views), so the pure-VM
behaviour + tests are untouched:

- **ADDHEAD** → `RideEngine.SyncHeads(VM.HeadSlots)`: each occupied slot shows a head marker at its
  **head node** (`RideNodePositions.ObjectNodeIds[slot]`, the type-`0x80` mounts), removed when the slot is
  vacated (DELHEAD). The head table is also **sized to the model's head-node count** (`VM.SetHeadCapacity`,
  matching the original probing type-`0x80` at spawn) instead of the fixed-8 stand-in.
- **WALKON** → `RideEngine.SyncWalk(VM.WalkSlots, VM.GameTime)`: each non-free slot shows a peep marker
  **gliding between its two walk nodes**, interpolated by the slot's own start/end clock and facing along
  the travel direction (the original's `atan2`); Arrived/Done peeps pin at the end node; a freed slot drops
  its marker. The interpolation is a pure, unit-tested helper (`RideEngine.WalkSample`).
- The markers are emissive **stand-ins** (no peep/head art — same pattern as the light/particle proxies),
  driven by **real node positions**; swapping in the peep sprite / head mesh is a renderer follow-up.
- Unit-tested (`RideEngineTests.WalkSample*` — midpoint/clamp/end-pin/zero-span + atan2 facing;
  `RideScriptTests.HeadCapacityFollowsHeadNodeCount`/`HeadCapacityIgnoresNonPositive`).

## Remaining

1. **Authored path shape.** The moving nodes follow the *procedural* ellipse, not the ride's authored
   track — the real shape still needs the ride **motion/skeleton simulation** (T-032 car-physics + T-033
   bone transforms). The resolver is shape-agnostic, so once the real path exists, the vehicle just
   publishes those positions instead.
2. **Art swap.** WALKON peeps + ADDHEAD heads are placed + animated as marker proxies; rendering the real
   peep sprite / head sub-mesh at those positions is a renderer follow-up.

## Acceptance criteria

- A tour ride's car follows the ride's authored path; a walking peep (WALKON) glides between the ride's
  real walk nodes; heads (ADDHEAD) appear at head nodes. *(Node-table decode + the runtime resolver +
  node-positioned effects + WALKON/ADDHEAD placement done — peeps glide between walk nodes, heads sit at
  head nodes, as marker proxies; the authored path shape + the real peep/head art await the motion sim +
  the render path.)*

## Affected files

`source/OpenTPW/World/Rides/RideNodePositions.cs` (new), `RideVehicle.cs`, `RideEngine.cs`,
`IRideEngine.cs`, `source/OpenTPW/World/Ride.cs`, `source/OpenTPW/World/Level.cs`,
`source/OpenTPW/VM/RideVM.Walk.cs`/`RideVM.Heads.cs` (head-capacity setter), `source/OpenTPW/VM/Handlers/Particles.cs`,
`source/OpenTPW.Files/Formats/Model/ModelFile.cs` (node-graph access).
