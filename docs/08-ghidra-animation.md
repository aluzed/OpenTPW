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

- **`7` prefix** (`7monkeym1`, `7Bird`, `7wateride`) — the **low-detail (LOD)** model set, used when
  the ride is far from camera.
- **`P` prefix** (`Ptotem`, `Pwateride`) — a **preview/portrait** model (the icon shown in the
  build menu).
- Trailing **`m`** on coaster track pieces (`wr_trckbm`, `wr_ringM`) is a *different* `m` — those are
  separate track-segment objects in a coaster ride (`wateride`), not the Main animation channel.

### Frame files are vertex keyframes, not standalone models — verified

Parsing the base `monkey.MD2` with the engine's `ModelFile` succeeds (9 meshes, 363 verts, 596 faces).
Parsing any frame file (`monkeym1.MD2`, `monkeyc.MD2`, …) through the same parser throws
`EndOfStreamException`: the frame files **share the base's header prefix** (`meshCnt` at `0x44`,
`frameCount` at `0x36` read identically) but their mesh table is a thin per-frame **vertex-position
payload**, not a full mesh tree. They only make sense applied on top of the base model's topology.
This matches `FUN_00461f10` folding each frame into the base via `FUN_00470b30` rather than loading
them as independent models.

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

## Function map

| Function | Role |
|----------|------|
| `FUN_00461f10` | Model + animation loader: loads base `.md2`, then per-channel `<base><c>[<n>].md2` keyframe files |
| `FUN_00467a80` | **Sign** loader: rasterises `.sgn` text via GDI onto `sign1.tga`/`sign2.tga` surfaces |
| `FUN_00470b30` | Folds one keyframe's vertex data into the base model's animation tables |
| `FUN_0046d6d0` / `FUN_0044a220` | Load/lookup a model file by path |
| `FUN_005ecb40` | GDI text → sign texture rasterisation |

Strings: `%s%s%c.md2` `0x0074d2a8` · `%s%s%c%d.md2` `0x0074d2b4` · `sign1.tga` `0x0074d6b4` ·
`sign2.tga` `0x0074d6a8` · `%s\%s.sgn` `0x0074d68c` · `ANIMATINGTEXTURES` `0x007443f0` ·
"Ride %s has relative animation incorrectly set" `0x0074d2c4`.
