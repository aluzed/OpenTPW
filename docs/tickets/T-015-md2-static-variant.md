# T-015 — Decode the static `.MD2` variant (frameCount 0)

- **Priority**: 🟡 Feature
- **Type**: Reverse engineering
- **Status**: ⚠️ **Partial.** The real discriminator is now **confirmed via Ghidra** (the
  no-CD `tp.exe` loader `FUN_0046d6d0`): it's the **version** fields at offsets 4/8, not
  frameCount. `ModelFile` now gates on them exactly as the original does — rejecting the
  legacy/static variant cleanly. **Decoding** that legacy layout still remains.
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

- `ModelFile` now reads the version fields at 4/8 and rejects any non-`(0xDD,0xCB)` file with
  a clear message — matching the original loader exactly. Covered by
  `ModelFileTests.RejectsLegacyVersion` (the GARROW `0x18`/`0x17` values).

## Remaining work

1. Trace the loader's legacy path (`flags & 1`, version `0x18`) to map the static-variant
   header (mesh-table / vertex / face / UV offsets).
2. Parse it and reuse the mesh-decode path; keep PAUSED/BANKRUPT/CONGRATS working.

## Acceptance criteria

- `garrow.MD2` / `rarrow.MD2` parse to meshes with in-range triangle indices and finite
  positions (extend `ModelFileTests` with a static-variant sample).

## Reverse-engineering aid

The loader `FUN_0046d6d0` (no-CD `tp.exe`) — see [05-ghidra-reverse.md](../05-ghidra-reverse.md).

## Affected files

`source/OpenTPW.Files/Formats/Model/ModelFile.cs`, `source/OpenTPW.Tests/ModelFileTests.cs`.
