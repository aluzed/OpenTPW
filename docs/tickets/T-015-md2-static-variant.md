# T-015 — Decode the static `.MD2` variant (frameCount 0)

- **Priority**: 🟡 Feature
- **Type**: Reverse engineering
- **Status**: ✅ **Done.** The discriminator was confirmed via Ghidra (loader `FUN_0046d6d0`): the
  **version** fields at offsets 4/8. The legacy/static variant (`GARROW.MD2`/`RARROW.MD2` = `0x18`/`0x17`)
  is now **decoded** — its 2-byte-packed header + 32-byte vertices + 24-byte faces are parsed by
  `ModelFile.ReadLegacyStatic`, verified against both real files (sane geometry + texture binding).
- **Split from**: [T-012](T-012-partial-formats.md) (which is now closed as a record of the
  animated-`.MD2` work).

## Context

The animated `.MD2` variant (frameCount ≥ 1, e.g. `PAUSED.MD2`) parses and renders
(`OpenTPW.Files/Formats/Model/ModelFile.cs`). The **static variant** (`frameCount == 0`,
e.g. `Data/generic/dynamic/garrow.MD2`, `rarrow.MD2`) uses a different header layout — the
frame-list / mesh-table pointers the animated path reads at 0x54 / 0x70 hold unrelated data.
`ModelFile` currently **detects `frameCount == 0` and throws a clear `InvalidDataException`**
rather than seeking to a bogus offset.

## Findings (Ghidra-confirmed)

The MD2 loader in the no-CD `tp.exe` is `FUN_0046d6d0`. It memory-maps the file then
relocates a table of offsets to absolute pointers. The **version gate** is the key:

```c
if (*piVar8 == 0x1cd15d46) {            // magic @ 0x00
    if ((uint)piVar8[1] < 0xde) {        // version @ 0x04
        if ((uint)piVar8[1] < 0xdd && (flags & 1) == 0) { reject; }
        else { /* main load; also requires piVar8[2] @ 0x08 == 0xcb */ } } }
```

So the shipping format is **exactly `(offset4 = 0xDD, offset8 = 0xCB)`**; the loader rejects
anything else by default. Verified across samples:

| file | off 4 | off 8 | frameCount @0x36 |
|------|:-----:|:-----:|:----------------:|
| paused / bankrupt / congrats | `0xDD` | `0xCB` | 1 |
| garrow / rarrow (legacy/static) | `0x18` | `0x17` | 0 |

`frameCount == 0` was a coincidental heuristic; the real discriminator is the version. The
relocated header offset slots are `piVar8[0x13..0x1e]` (file 0x4c..0x78), `[0x1f]` (0x7c),
`[0x26]` (0x98), `[0x2b]` (0xac); mesh count is `u16 @0x44`.

## Done

- `ModelFile` reads the version fields at 4/8: `(0xDD,0xCB)` → the animated path; `(0x18,0x17)` → the
  new legacy `ReadLegacyStatic` path; anything else → a clear `InvalidDataException`
  (`ModelFileTests.RejectsUnknownVersion`).
- **Legacy/static layout decoded** (`GARROW.MD2`/`RARROW.MD2`). The shipped loader gates this version
  behind special flags; the file itself is the same family as the animated `.MD2` but **2-byte packed**
  (its offsets read clean only at +2 misalignment). Decoded structure:
  - header pointers (packed): `0x5a` → texture path, `0x66` → vertex block, `0x6a` → face block,
    `0x72` → **mesh table**;
  - mesh table: `u32 meshCount`, a 16-byte prologue, then a 16-byte descriptor per mesh
    `{u16 numVerts, u16 numFaces, u32 vertPtr, u32 facePtr, float scale}`;
  - **vertices**: `numVerts × 32 B` = 8 floats (position xyz, normal xyz, uv);
  - **faces**: `numFaces × 24 B`; the three triangle indices are the `u16`s at byte offsets +2/+4/+6
    (offset 0 is a per-face flag).
- Verified against the real `GARROW.MD2` and `RARROW.MD2` (14 verts of finite arrow geometry + 24
  triangles, all indices in range, the `texture` binding from the embedded path) and a synthetic sample
  (`ModelFileTests.DecodesLegacyStaticVariant`).

## Acceptance criteria — met

- `garrow.MD2` / `rarrow.MD2` parse to meshes with in-range triangle indices and finite positions
  (both pass `ParsesRealModelSample`; a synthetic legacy file is unit-tested).

## Reverse-engineering aid

The loader `FUN_0046d6d0` (no-CD `tp.exe`) — see [05-ghidra-reverse.md](../05-ghidra-reverse.md).

## Affected files

`source/OpenTPW.Files/Formats/Model/ModelFile.cs`, `source/OpenTPW.Tests/ModelFileTests.cs`.
