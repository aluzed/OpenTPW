# Ride animation & signs (Ghidra) — how TPW animates a ride

How the original *Theme Park World* (`tp.exe`, the unencrypted no-CD build — see
[05-ghidra-reverse.md](05-ghidra-reverse.md)) animates a ride's model, and what the `.sgn` files
actually are. This was opened as "decode `.sgn` and the `TRIGANIM`/`ANIMATINGTEXTURES` path"; the
RE below **corrects that starting assumption** (`.sgn` is *signs*, not animation) and recovers the
real animation mechanism.

> **Method.** Ghidra 12.1 headless decompilation of `tp.exe`, driven by a defined-string xref scan
> (`FindAnim.java`): find the functions that reference the animation/format strings, decompile them,
> then cross-check every claim against the **actual bytes of shipping ride WADs** (extracted with
> `OpenTPW.WadTool`) and the engine's own `ModelFile` parser. Where a claim is verified against real
> assets it says so; nothing here is guessed from names alone.

## TL;DR

- **`.sgn` files are SIGNS**, not animation: customisable billboard text (e.g. the ride's name on its
  entrance board) rendered with **GDI** onto two texture surfaces named `sign1.tga` / `sign2.tga`.
  The loader is `FUN_00467a80`. This is unrelated to motion.
- **Ride animation is vertex-keyframe animation split across sibling `.md2` files.** A ride has a
  **base model** `<name>.md2` (full topology — the bind pose), plus per-animation **keyframe files**
  named `<name><c>.md2` (single frame) and `<name><c><n>.md2` (a numbered sequence), where `<c>` is a
  one-letter **animation channel** code. The loader is `FUN_00461f10`.
- The channel letter is the **first letter of the animation name** (`ScriptDefs.Animations`): there
  are **12 channels** (slots 0–11). The **Main** channel (`m`) holds the looping ride motion.
- Frame files are **thin** (2–3 KB vs ~40 KB for the base) and **do not parse as standalone models** —
  they carry per-frame vertex data keyed to the base model's topology. Decoding their exact byte
  layout is the remaining work (see [T-033](tickets/T-033-ride-animation-keyframes.md)).

## `.sgn` is the ride sign, not animation — `FUN_00467a80`

The format strings are `%s\%s.sgn` (`0x0074d68c`) and `%s\%s_%s.sgn` (`0x0074d698`). The function
that builds those paths, `FUN_00467a80`, does the following:

- Walks the base model's surface/material list (`*(model+4)+0x50/0x54` — the same
  `textureList`/`frameList` offsets the `ModelFile` parser reads at `0x50`/`0x54`) looking for two
  specific texture names: **`sign1.tga`** (`0x0074d6b4`) and **`sign2.tga`** (`0x0074d6a8`).
- When found, it builds the `.sgn` path, reads it, then does `GetDC` / `FUN_005ecb40(hDC, …)` /
  `ReleaseDC` against the game window — i.e. it **rasterises text via GDI** and uploads it as the
  sign surfaces' texture. It flags the surfaces (`|= 0x8000`) as dynamically generated.
- On failure it logs **`"Ride %s does not have correctly … "`** (`0x0074d65c`).

Cross-check: extracting `monkey.wad` and parsing `monkey.MD2` with the engine's own parser yields 9
meshes, two of which are literally named **`m_sign`** and **`m_sign2`** — the exact surfaces the `.sgn`
loader targets. So `.sgn` is conclusively the **sign/billboard** subsystem (ride name boards, price
signs, etc.), not motion. The earlier "`.sgn` = animation" hypothesis is **wrong** and is retired here.

## Ride animation — `FUN_00461f10`

This is the model loader proper. It loads the base `.md2`, then for each of 12 animation channels
tries to load that channel's keyframe file(s). The relevant format strings:

| VA | bytes | meaning |
|----|-------|---------|
| `0x0074d2a8` | `%s%s%c.md2`   | `<dir><base><channel>.md2` — a single-frame channel |
| `0x0074d2b4` | `%s%s%c%d.md2` | `<dir><base><channel><frame#>.md2` — a numbered sequence |
| `0x0074d2c4` | `Ride %s has relative animation incorrectly set\nStripping flag, animation will not work!` | error |

The body loops **12 times** (`local_420 = 0xc`), advancing a channel-letter pointer (`local_42c`,
starts at `"C"`) by 8 bytes each iteration, and for each channel keeps reading `<base><c><n>.md2` with
incrementing `n` until a frame is missing — building a per-channel frame list. `FUN_00470b30` is
called once per accumulated frame to fold each keyframe into the base model's animation tables.

### The 12 channels = `ScriptDefs.Animations`, keyed by first letter

The channel letter is the **first letter of the animation's name**. Verified directly against the
WAD contents — `monkey.wad` ships exactly these per-channel files:

| Channel file | Letter | `ScriptDefs.Animations` | Notes |
|--------------|--------|-------------------------|-------|
| `monkeyc.MD2`        | `c` | `ANIM_Create` (0)  | single frame |
| `monkeyi.MD2`        | `i` | `ANIM_Idle` (2)    | single frame |
| `monkeyl.MD2`        | `l` | `ANIM_Load` (3)    | single frame |
| `monkeys.MD2`        | `s` | `ANIM_Start` (4)   | single frame |
| `monkeym1..m7.MD2`   | `m` | `ANIM_Main` (5)    | **7-frame looping sequence** |
| `monkeye.MD2`        | `e` | `ANIM_End` (6)     | single frame |
| `monkeyb.MD2`        | `b` | `ANIM_Break` (9)   | single frame |
| `monkeyr.MD2`        | `r` | `ANIM_Repair` (10) | single frame |

`totem.wad` corroborates: `totemc` (Create), `totemb1/b2` (Break, 2 frames), `totemr` (Repair),
`totemm1..m10` (Main, 10 frames). `wateride.wad` has `wateridec` (Create) and `wateridem` (Main).
The remaining channels (`Unload` → `u`, `Other` → `o`, and the two unused enum slots 1 and 8) simply
have no art on these rides, which is allowed — a missing channel file just means "no animation for
that state".

So **Main (`m`) is the ride's primary looping motion**; the others are short state transitions
(spawn-in, break-down, repair, etc.). This is exactly what `TRIGANIM`/`LOOPANIM`/`WAITANIM` select by
passing a `ScriptDefs.Animations` value: the engine plays that channel's keyframe sequence.

### Asset-name variants

Ride WADs also carry prefixed variants of the same names:

- **`P` prefix** (`Ptotem`, `Pwateride`, `PGOKARTS`, …) — a **preview** model, shown in the build
  menu, *not* in-world. 95 across the four worlds; confirmed by the `PreviewAnimType` string in
  `tp.exe`. OpenTPW loads a ride's in-world model by its exact base name (`<name>.md2`), so these
  preview models are never rendered in the park.
- Trailing **`m`** on coaster track pieces (`wr_trckbm`, `wr_ringM`) is a *different* `m` — those are
  separate track-segment objects in a coaster ride (`wateride`), not the Main animation channel.

> **No separate LOD model set.** An earlier draft listed a `7`-prefix "low-detail" set — that was a
> raw-byte regex artifact (a `0x37` count/flag byte adjacent to names like `Bird.MD2`), not real
> files. Proper WAD extraction across all 76 rides finds **zero** `7`-prefix `.md2` files, and `tp.exe`
> has no `lod`/`lores`/`detail` model strings or a LOD path format (the model loader only ever uses
> `%s%s%c.md2` / `%s%s%c%d.md2`). The original has no distance-based model swap; OpenTPW needs none.

### Frame files are vertex keyframes, not standalone models — verified

Parsing the base `monkey.MD2` with the engine's `ModelFile` succeeds (9 meshes, 363 verts, 596 faces).
Parsing any frame file (`monkeym1.MD2`, `monkeyc.MD2`, …) through the same parser throws
`EndOfStreamException`: the frame files **share the base's header prefix** (`meshCnt` at `0x44`,
`frameCount` at `0x36` read identically) but their mesh table is a thin per-frame **vertex-position
payload**, not a full mesh tree. They only make sense applied on top of the base model's topology.
This matches `FUN_00461f10` folding each frame into the base via `FUN_00470b30` rather than loading
them as independent models.

### Decoded frame-file binary layout

Decompiling the model loader `FUN_0046d6d0` (shared by base and frame files) and cross-checking the
bytes of `monkeym1.MD2` / `monkeyc.MD2` pins the layout down:

- The loader reads the whole file into memory, checks the `0x1CD15D46` magic, then **relocates** a
  table of internal offsets (struct dwords `[0x13]`..`[0x1f]` = file offsets `0x4c`..`0x7c`, plus
  `[0x26]` = `0x98` and `[0x2b]` = `0xac`) into absolute pointers in place. Base and frame files use
  the *same* loader.
- A **frame file's header `0x00`–`0x4f` is byte-identical to the base** (magic, version, the counts at
  `0x36`/`0x3a`/`0x3e`/`0x42`/`0x44`) — so it inherits the base's mesh/face/vertex counts. But the
  base's **offset-table fields `0x50`–`0x7c` are all zero** in a frame: a frame has no texture list, no
  frame list and no mesh table of its own.
- The dword at **`0x98`** is the frame's animation pointer. In the **base it is 0** (the base is the
  static bind pose); in a frame it is a file offset to a small (~72-byte) **per-surface relink
  trailer** near EOF. The bulk of the file, **`0xb8` .. `[0x98]`**, is the frame's **vertex-position
  payload** (verified: the floats there are model-space coordinates, e.g. `-12.92, 2.45, -0.1`).
- The `0x98` trailer is a per-surface descriptor: a count (`+0x12`) and an offset array (`+0x2c`)
  whose entries point back into the `0xb8`-region vertex blocks. `FUN_00461f10` walks it to **relink
  each frame surface to the matching base-model surface** (comparing texture/surface indices), so a
  frame supplies new positions only for the surfaces that actually move — which is why frame sizes
  vary (`monkeym1` 2.9 KB carries a few surfaces; `monkeyc` 33 KB carries most of the model).

So a complete keyframe loader must: load the frame via the same flat-load+relocate path, read the
`0x98` trailer to map each frame surface → base surface, and apply that surface's payload. The trailer
holds `count` (at `+0x12`) surface records of `0x40` bytes each (array at `+0x2c`); each record's
`+0x10` is the base surface index, `+0xc` an element count, and `+0x18`/`+0x1c`/`+0x20` are three
offsets into the data region.

### The keyframe data is time-indexed tracks — decoded from the runtime

Decompiling the runtime apply path settles exactly what the payload is. It is **keyframed animation
tracks**, interpolated by animation time — a per-surface, multi-track system.

**The interpolator — `FUN_00470b60`.** Given a track and the current animation time (`model+0x20`,
converted with `__ftol`), it walks the track's entries (entry stride selected by a **type** arg:
`1→4 B`, `2→0x14=20 B`, `4→0x10=16 B`), reads each entry's leading `u16` as the **keyframe time**,
finds the two keys bracketing now, and returns the bracketing indices + a lerp factor
`t = (now − prevTime) / (nextTime − prevTime)`. Each keyframe header dword is `(marker << 16) | time`:
the **low `u16` is the keyframe time** (`0,10,20,30…`) and the **high `u16` is the interpolation
type** — **`0xFFFF` = a rotation track** (4-float quaternion, stride 20, slerped) and **`0x0000` = a
linear vec3 track** (translation/scale, 3 floats, stride 16, lerped). A stride-20 `0xFFFF` entry =
`[time][quaternion]` is exactly our `0x158` data; a stride-16 `0x0000` entry = `[time][vec3]` is a
scale/translation key (e.g. `space_bouncy`'s `0.07→1.0` grow-in). Tracks are laid out contiguously, so
a track is bounded by the next track's offset (plus the marker + strictly-increasing-time rule) — the
marker scan alone over-reads an identity track into adjacent record data.

**Playback rate.** Keyframe times are in **30 FPS frames**. The animation clock is
`animTime = (nowMs − startMs) × speed × 0.03` (`FUN_004735d0`; the global clock `DAT_007b496c` is
milliseconds, `_DAT_006fec0c = 0.03 ≈ 1/33.33`, and the neighbouring `33.3333 = 1000/30` constants are
the 30 FPS frame time). So at `speed = 1` the clock advances **30 keyframe units per second** — e.g.
monkey's 0→40 arm spin takes ~1.33 s, bbugs's 0→100 morph ~3.3 s. (OpenTPW uses `KeyframeRate = 30`.)

**The apply — `FUN_00471860`** (per surface; `FUN_004679d0` calls it for every surface). A flags word
on the surface's anim descriptor (`desc[1]`) selects which tracks run, and it composes them:

| Flag (`desc[1]`) | Track | What it does |
|---|---|---|
| `0x1000` (+`0x4000`) | **vertex morph** | linearly interpolates per-vertex `vec3` positions between integer keyframes, writing the surface's vertex buffer (`out[0x18]`, SoA-of-4 X/Y/Z at `+0/+0x10/+0x20`) |
| `0x8` | **rotation** | quaternion keyframe track → rotation matrix (`FUN_00474490`) |
| `0x80` | **scale** | scale keyframe track |
| `0x200` / `0x1` | **translation** | position keyframe track → `out[0x10..0x12]` |

So TPW ride animation is a **hybrid**: a surface can morph its vertices *and/or* carry T/R/S keyframe
tracks. `monkeym1`'s two records both target base surface 5 (`m_arm`) and hold the unit-magnitude
quaternions (`(1,0,0,0)`, `(0.7071,0,0,0.7071)`) of the **rotation** track — the monkey's arms swing.
The `(0,0,0,1)`-style and `vec3` blocks in the same file are the scale/translation/morph tracks for
their surfaces. (`0.7071` = sin 45°.) A separate system, **animating textures** (`FUN_00467310`,
`ANIMATINGTEXTURES`), scrolls UVs procedurally — that's water/conveyor flow, not part of this keyframe
path.

### Vertex-morph format (decoded + validated; parser implemented)

The vertex-morph track is the one track type not yet implemented in OpenTPW (rotation, translation and
scale are done — they ride on the per-mesh transform). It is common — **~154 of 595 animated ride
files** carry real multi-frame morph (across all four worlds). Its layout, decoded from the morph
apply `FUN_004714a0` (the flag-`0x1000`-without-`0x4000` path; the inline lerp in `FUN_00471860` is the
rarer `0x4000` variant):

- A morphing surface has **one record per frame** (record `dword[0]` = frame index, `dword[0x5]` =
  frame+1), each record's `dword[0xa]` pointing to that frame's morph block.
- **Vertices are quantised**: each is a packed 32-bit int holding three **10-bit signed** components —
  `X = sext(packed[0:9])`, `Y = sext(packed[10:19])`, `Z = sext(packed[20:29])` (the code does
  `(packed << 0x16) >> 0x16` etc.). Each is dequantised as `component = signed10 * scaleAxis +
  offsetAxis`, with a per-block **bounding-box** scale (`block+0x20/24/28`) and offset
  (`block+0x14/18/1c`).
- The block's `+0x0c` points to an **array of `0x14`-byte sub-entries** (count at block `+0x02`). Each
  sub-entry: vertex count (`+0x02`), an **index array** ptr (`+0x04`, `vc` u16 output slots), a
  **times** array ptr (`+0x08`, ascending u16 keyframes), a **packed-int** array ptr (`+0x0c`,
  frame-contiguous `frameCount × vc` ints), and a runtime key index (`+0x10`, zeroed in the file).
  Decoded from `FUN_00470e90` (the per-sub-entry loop) and confirmed against `fantasy/bbugs`: a
  sub-entry's packed array is exactly `frameCount × vc × 4` bytes and ends precisely where the next
  sub-entry's index array begins.
- **Application is a global additive blend-shape** (`FUN_004721f0`): it iterates *all* records (0x40
  stride) calling the apply with **one shared output vertex buffer** for every record, passing the
  record as the "additive" argument. So records additively deform a single shared vertex buffer; the
  index-array slots are **global vertex indices** into it (`slot>>2` = SoA group, `slot&3` = lane).
  This is why bbugs's 12 records have overlapping small slot ranges — they layer onto the same buffer.
  Needs a **dynamic vertex buffer**; OpenTPW `Model` already exposes `Device.UpdateBuffer`, so a
  per-frame CPU dequantise + re-upload is feasible.

**Status: implemented + working in-engine.** `RideKeyframeFile.MorphSub` parses it (unit-tested: 10-bit
dequant, bbox transform; cross-checked against real `BbugsC.MD2`). `RideEngine.ApplyMorph` drives it
per frame — resets each part to its rest pose, samples the keyframes, maps the **global** slot → our
per-mesh vertex (cumulative offsets), writes the dequantised position (model→world Y/Z swizzle,
**absolute** — confirmed visually), and re-uploads via `Model.UploadVertices`. Verified on
`fantasy/bbugs`: the creature deforms coherently from the real keyframes. All three track families
(rotation, translation/scale, vertex morph) are now driven from real ride data.

**Consequence for the engine.** The clean target is a per-surface animation evaluator that, each frame:
finds the bracketing keys per track (the `FUN_00470b60` logic), interpolates rotation (slerp/lerp on
the quaternion), scale and translation, and composes T·R·S onto the surface's transform (which maps to
our per-mesh `TransformMatrix`); surfaces flagged `0x1000` additionally lerp their vertex positions
(this is the only part that needs a dynamic/morph vertex path). The track byte layout and the apply
order are now fully known — this is the remaining T-033 implementation work.

This is classic Quake-II-style MD2 vertex-morph animation, but with each keyframe stored in its own
file instead of appended into one. (It also explains the earlier finding that a single shipping
`.md2` looks "static": the motion lives in the *sibling* frame files, which the parser had never been
pointed at.)

## What this unlocks, and what remains

`RideEngine` now (this change) **discovers the real animation channels** for a loaded ride by probing
the WAD for `<base><c>.md2` / `<base><c><n>.md2`, maps each `ScriptDefs.Animations` value to its
channel + frame count, and drives `TriggerAnim`/`LoopAnim` against that real data (logging the true
channel and frame count, animating only channels the ride actually ships). The visible motion is still
the procedural placeholder until the keyframe bytes are decoded.

**Remaining (T-033):** decode the frame-file vertex-payload byte layout (and the base model's vertex
quantisation it references) so the engine can morph the base mesh through the real per-frame positions
— replacing the placeholder with the original animation.

## The node "rig" — how node world positions are produced (no file skeleton)

> **Method.** Ghidra 12.1.2 headless on the no-CD `tp.exe` (3.56 MB, the depacked build — see
> [05](05-ghidra-reverse.md)); the functions below were decompiled directly. This closes the
> long-standing T-048 question "decode the skeleton/rig that positions ride nodes."

**Conclusion up front: there is no bone/skeleton structure in the model file, and nothing to decode as a
format.** A node's world position is produced entirely at **runtime**, by composing a per-node 4×4 matrix
that the engine binds on the fly. The whole mechanism:

### The node resolver — `FUN_0044b220(model, selector, id) → index`

Exactly the OpenTPW `ModelFile` model: it reads the node **count** at the file header `0x48` (`u16`) and
the node **table** pointer at `0x7c`, walks `0x14`-byte entries, and returns the **index** of the first
node whose **id** (entry `+4`) equals `id` **and** whose **type mask** (entry `+0`) ANDs non-zero with
`selector`. An all-types request uses `selector = 0x3da1f82`. This is byte-for-byte
`ModelFile.NodesMatching` / `Node.Matches` — confirmed against the binary.

### The node positioner — `FUN_00556b90` + `FUN_00435710`

`FUN_00556b90(model, outPos, outDir, nodeId, selector)` resolves the node index with `FUN_0044b220`, then
reads a **runtime parallel node table** (allocated per model instance, same `0x14` stride, reached via
`instance → +8 → +0x28 → +4`):

- entry `+4` = a pointer to the node's **4×4 transform matrix** (`0` ⇒ position is the origin);
- entry `+0` = a flags byte (bit `0x10` flips the facing direction).

The **position is the matrix's translation row**: `FUN_00435710` copies `matrix +0x30 / +0x34 / +0x38`.
The **facing** is the matrix's forward row `+0x20 / +0x24 / +0x28` (normalised, negated when flag `0x10`).
So a node's matrix is a standard 4×4 (`+0x00` right, `+0x10` up, `+0x20` forward, `+0x30` translation;
each row four floats), and the world position is that matrix composed with the **model's own world
transform** (the walk updater `FUN_00557ab0` post-multiplies by `model … +0x78`). The error path logs
`"RSSE: Invalid Node ID"`; when `nodeId < 0` it falls back to the model body position
(`model … +0x78 → +0x44`) — which is exactly OpenTPW's "no node ⇒ ride centre" fallback.

### Where the matrix comes from — runtime, never the file

In every shipped model the file node entry's two pointer slots (`+0xc/+0x10`) are **null** and there is
**no bone table** — so the matrix at runtime `entry+4` is **not** loaded from the file. It is bound when an
object is attached to the node, and updated by that object's subsystem:

- **Animated meshes** — the keyframe surface animation above (`FUN_00471860`: rotation/scale/translation
  /morph) writes the surface transforms each frame; a node bound to a surface tracks it.
- **Cars (TOUR/BUMP/COAST)** — a car *instance* carries its own matrix and walks a **waypoint list**. The
  kart/bumper updater `FUN_0054a040` holds the class's node/waypoint list at `class[0x2a]` (singly linked by
  `+0x14`, length `class[0x17]`); the instance's current waypoint is `inst[0x1f] = *(node+4)`. A **kart**
  steps the list in order (a fixed loop); a **bumper** picks waypoints at random (`FUN_00516330 % count`)
  and does pairwise proximity collision against the other cars. The car's front/rear nodes are resolved as
  `FUN_0044b220(model, 0x100, 1)` and `(…, 0x100, 2)` — i.e. **car nodes carry type `0x100`, ids 1/2**.

So node motion is the runtime **composition of the keyframe animation (T-033) and the car waypoint sim** —
there is no separable rig to lift out of the file. The car *path shape* is the sequence of the model's car
nodes, but their **positions** are produced by the sim, not stored — matching the T-048 finding and the
footprint-shaped stand-in OpenTPW now uses.

### Cross-validation of existing OpenTPW code (now binary-cited)

- **Head capacity = count of type-`0x80` nodes.** The RSE loader `FUN_005587f0` loops
  `FUN_0044b220(model, 0x80, i)` for `i = 1, 2, …` until `-1`, storing the count — exactly
  `RideVM.SetHeadCapacity(NodeField.ObjectNodeIds.Count)`.
- **Node table layout + resolver match** (count `@0x48`, table `@0x7c`, `0x14`/entry, `{mask, id, …}`,
  match `(mask & selector) && id`) — exactly `ModelFile.ParseNodeTable` / `Node.Matches`.
- **Effect/walk/head node positioning** all flow through `FUN_00556b90`, the function whose `nodeId`
  operand OpenTPW now threads through EVENT/SPARK/REPAIREFFECT and WALKON/ADDHEAD (T-048).

## Function map

| Function | Role |
|----------|------|
| `FUN_00461f10` | Model + animation loader: loads base `.md2`, then per-channel `<base><c>[<n>].md2` keyframe files; walks each frame's `0x98` trailer to relink its surfaces onto the base |
| `FUN_00467a80` | **Sign** loader: rasterises `.sgn` text via GDI onto `sign1.tga`/`sign2.tga` surfaces |
| `FUN_0046d6d0` | Model file loader: flat-loads the file, checks magic, relocates the offset table (`0x4c`–`0x7c`, `0x98`, `0xac`) to absolute pointers in place — used for both base and frame files |
| `FUN_0046dcf0` / `FUN_0044a220` | Higher-level load/lookup of a model by path (calls `FUN_0046d6d0`) |
| `FUN_00471860` | **Per-surface pose apply**: runs the surface's vertex-morph / rotation / scale / translation tracks (flags in `desc[1]`) and composes the result |
| `FUN_004679d0` | Iterates a model's surfaces and calls `FUN_00471860` for each |
| `FUN_00470b60` | **Keyframe interpolator**: finds the two keys bracketing the current time and returns the lerp factor (entry stride by track type: `4`/`20`/`16` B) |
| `FUN_004721f0` | Iterates **all** anim records (0x40 stride), applying each to one **shared** vertex buffer (additive) — the morph blend-shape driver |
| `FUN_004714a0` | **Vertex-morph apply** (flag `0x1000`): dequantises 10-bit-packed per-vertex positions (×scale + offset), blends two keyframes, writes the surface vertex buffer |
| `FUN_00470e90` | Per-sub-entry morph loop (clean): confirms sub-entry layout (vc `+2`, index `+4`, times `+8`, packed `+0xc`, keyIdx `+0x10`) |
| `FUN_004711d0` | Computes the two-keyframe blend for the morph bounds |
| `FUN_00474490` | Builds a rotation matrix from the interpolated quaternion (rotation track) |
| `FUN_00467310` | **Animating textures** (`ANIMATINGTEXTURES`): procedural UV scroll — a *separate* system from keyframes |
| `FUN_00470b30` | Marks a frame surface dirty (sets flag `0x20` on the model) — not the vertex fold |
| `FUN_005ecb40` | GDI text → sign texture rasterisation |
| `FUN_0044b220` | **Node resolver**: count `@0x48`, table `@0x7c`, `0x14`/entry; returns the index where `(mask & selector) && id` |
| `FUN_00556b90` | **Node positioner**: resolves the node, reads its runtime 4×4 matrix (translation `+0x30`, forward `+0x20`); `nodeId < 0` ⇒ model body |
| `FUN_00435710` | Copies a matrix's translation row (`+0x30/0x34/0x38`) — the node→position primitive |
| `FUN_00557ab0` | Per-frame **walk** update: resolves each walk slot's `0x800` node, post-multiplies by the model world transform (`+0x78`) |
| `FUN_0054a040` | **Kart/bumper car** updater: waypoint list `class[0x2a]` (linked `+0x14`), current `inst[0x1f]`; kart loops it, bumper randoms; car nodes `0x100` ids 1/2 |
| `FUN_005587f0` | RSE script loader; counts type-`0x80` nodes (`FUN_0044b220(model,0x80,i)`) for the **head-slot capacity** |

Strings: `%s%s%c.md2` `0x0074d2a8` · `%s%s%c%d.md2` `0x0074d2b4` · `sign1.tga` `0x0074d6b4` ·
`sign2.tga` `0x0074d6a8` · `%s\%s.sgn` `0x0074d68c` · `ANIMATINGTEXTURES` `0x007443f0` ·
"Ride %s has relative animation incorrectly set" `0x0074d2c4`.
