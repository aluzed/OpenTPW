# T-011 — Harden `RideVM.BranchTo` (remove the offset "HACK")

- **Priority**: 🟡 Feature / refactor
- **Type**: Tech debt / correctness
- **Status**: ☐ To do
- **Split out from**: [T-007](T-007-vm-opcodes-rse.md) (was a remaining bullet there).

## Problem

`RideVM.BranchTo` is explicitly marked as a hack: branch targets are compiled offsets
expressed in 4-byte instruction-stream units, and the method converts them back to an
instruction-list index by multiplying by 4 and searching for a matching `offset`:

```csharp
// source/OpenTPW/VM/RideVM.cs
var fileOffset = value * 4;             // each opcode/operand is 4 bytes
fileOffset += (int)Instructions.First().offset;
CurrentPos = Instructions.FindIndex( x => x.offset == fileOffset );
CurrentPos += 1; // Ignore NO-OP
```

This is `O(n)` per branch, fragile (depends on the leading NOP and exact byte offsets),
and easy to break when the loader/disassembler changes.

## Proposed fix

- Build an explicit **compiled-offset → instruction-index** map when the script is
  loaded (`RideScriptFile` already tracks `Branch { InstructionOffset, CompiledOffset }`).
- `BranchTo` becomes a dictionary lookup; drop the `*4` arithmetic and the magic `+1`.
- Add unit tests for a script with forward/backward branches (JSR/RETURN, BRANCH_Z).

## Acceptance criteria

- Branching resolves via a precomputed map, no per-branch scan.
- Tested against a script that loops/branches.

## Affected files

`source/OpenTPW/VM/RideVM.cs`, `source/OpenTPW/VM/RideScriptFile.cs`
