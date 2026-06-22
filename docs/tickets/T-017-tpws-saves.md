# T-017 — `.TPWS` save files (read + write)

- **Priority**: 🟡 Feature
- **Type**: Reverse engineering
- **Status**: ⚠️ Partial — the **container** format (header + BILZ/zlib) is Ghidra-corrected, fully read,
  and now **writable** (round-trips). The inner `SAD_*` module stream stays opaque, and a *real* sample
  still can't be obtained on this install (the `save/` dir is empty, no `.INTS` template).
- **Split from**: [T-012](T-012-partial-formats.md).

## Context

`SaveReader` (`OpenTPW.Files/Formats/Save/SaveReader.cs`) did a partial, partly-wrong read of `.TPWS`
saves and couldn't write them. There is **no save sample on the install disc** (saves are
user-generated), so validating against a real save needs one from a real install.

## Done (Ghidra: loader `FUN_00414d40` → header `FUN_00416240`, no-CD `tp.exe`)

- **Corrected the container header.** The leading `F4 01 00 00` is **not a magic** — it's the
  little-endian **version 500** (the loader rejects version > 500 as a "future-version save"). The full
  header is now decoded: `u32 version`, `u8`, **1280-byte** legal/copyright block (the loader checksums
  it), **256-byte** header struct, a **big-endian** (`ntohl`) `fileType` (`0x00012219`, checked against a
  constant), and a `u32` header/online flag — then the `BILZ` + zlib block. (The previous reader's
  824-byte copyright / offset guesses were wrong, so it never actually loaded a real save.)
- **Completed the read path** for the container and exposed it typed (`Version`, `FileType`, `HeaderFlag`,
  `LegalText`, `HeaderStruct`, decompressed `Payload`).
- **Implemented writing** (`Serialize` / `Build`): header + `BILZ` + zlib(payload). `Read → Write → Read`
  round-trips the header fields and payload (synthesised-save tests; future-version rejection tested).

## Remaining

1. **A real `.TPWS` sample** — still none here. The env-gated `ReadsRealSaveSample` test
   (`TPW_SAVE_SAMPLE`) validates the read + round-trip the moment one is provided.
2. **The `SAD_*` module stream** inside the payload (UI, Advisor, Coasters, Ridesystem, Particles, …) —
   each module serialises a slice of game state with a saved/loaded byte-count check. Decoding them is a
   large, separate effort and is kept opaque for now.

## Acceptance criteria

- A real `.TPWS` round-trips under test (read → write → read produces equal state), gated on
  `TPW_SAVE_SAMPLE` like the other format tests — wired and passing for synthesised saves; awaits a real
  sample for the live gate.

## Affected files

`source/OpenTPW.Files/Formats/Save/SaveReader.cs`, new `source/OpenTPW.Tests/SaveFileTests.cs`.
