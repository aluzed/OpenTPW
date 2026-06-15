# T-007 — Ride VM: re-enable the `.RSE` loader and complete the opcodes

- **Priority**: 🟡 Feature (core of ride gameplay)
- **Type**: Feature / reverse engineering

## Findings

- `source/OpenTPW/VM/RideVM.cs`: the `.RSE` file loader is **commented out**
  (`// rsseqFile = new RideScriptFile(...)`, `// public string Disassembly => ...`).
- **~27 opcode handlers** implemented (`VM/Handlers/`) out of **~210** documented
  (~13%). Several are no-ops marked `TODO` (`Misc.cs`: `GETTIME`, `SETLV`, `ENDSLICE`,
  `CRIT_LOCK`).
- `BranchTo` is marked "HACK" (manual offset conversion, fragile).

## Work

1. Rewrite/re-enable the `.RSE` **loader + disassembler** (`RideScriptFile`).
2. Harden `BranchTo` (offset table → clean instruction index).
3. Implement the missing opcodes in batches (animations, logic, math, events).
   Reference: <https://opentpw.gu3.me/formats/rsse-vm-instructions.html>.
4. Cover with unit tests on real `.RSE` scripts (present inside the `.WAD`s).

## Reverse-engineering aid

Use **Ghidra** on an unprotected `TP.EXE` to validate the semantics of uncertain
opcodes — see [../05-ghidra-reverse.md](../05-ghidra-reverse.md).

## Affected files

`source/OpenTPW/VM/` (the whole folder).
