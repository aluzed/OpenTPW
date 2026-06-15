# T-010 — ADD / SUB do not set arithmetic flags

- **Priority**: 🟠 Medium (correctness)
- **Type**: Bug
- **Status**: ☐ To do
- **Found during**: T-007 opcode work (comparing handlers against the ISA spec).

## Problem

The instruction-set docs state that **ADD** and **SUB** set the Zero/Sign flags from
their result ("Upon performing this calculation, the relevant flags will be set based on
the calculation's result"). The current handlers do not:

```csharp
// source/OpenTPW/VM/Handlers/Math.cs
public static void Add( ref RideVM vm, Operand dest, Operand value )
    => dest.Value = dest.Value + value.Value;            // no flags

public static void Sub( ref RideVM vm, Operand valueA, Operand valueB, Operand dest )
    => dest.Value = valueA.Value - valueB.Value;          // no flags
```

A `BRANCH_Z` / `BRANCH_NV` immediately after an ADD/SUB would therefore read **stale
flags** from a previous `CMP`/`TEST`, producing wrong control flow.

## Proposed fix

- Call the existing `SetArithmeticFlags( ref vm, result )` helper (added for MULT/DIV/MOD
  in `Math.cs`) at the end of `Add` and `Sub`.
- Add a unit test (extend `RideScriptTests`) asserting flags after ADD/SUB.

## Acceptance criteria

- ADD/SUB set Zero/Sign consistently with MULT/DIV/MOD and the spec.
- Covered by a test.

## Affected files

`source/OpenTPW/VM/Handlers/Math.cs`

## Links

Spec: `OpenTPW/OpenTPW.FileFormats` → `vm/instructions.md`. Related: [T-007](T-007-vm-opcodes-rse.md).
