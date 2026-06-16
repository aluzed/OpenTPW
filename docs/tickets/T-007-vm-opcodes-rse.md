# T-007 — Ride VM: re-enable the `.RSE` loader and complete the opcodes

- **Priority**: 🟡 Feature (core of ride gameplay)
- **Type**: Feature / reverse engineering
- **Status**: ⚠️ **Partial.** The `.RSE` **loader / disassembler is restored and working**
  (`source/OpenTPW/VM/RideScriptFile.cs`, recovered from git history where a refactor had
  deleted it). `RideVM` now actually parses the script; variable init is made defensive
  (`EnsureCommonVariables`). Validated by `RideScriptTests.LoadsTestScript` against a real
  `content/testscripts/Test.RSE` fixture (also recovered from history). **Remaining**:
  implement the ~180 missing opcodes, harden `BranchTo`, and execute scripts on real rides.

## Findings

- ~~`RideVM.cs`: the `.RSE` file loader is commented out.~~ **Fixed** — see below.
- **34 opcode handlers** implemented (`VM/Handlers/`) out of **~210** documented
  (~16%). Several are no-ops marked `TODO` (`Misc.cs`: `GETTIME`, `SETLV`, `ENDSLICE`,
  `CRIT_LOCK`/`CRIT_UNLOCK`).
- `BranchTo` is marked "HACK" (manual offset conversion, fragile).
- The call stack was a `Queue<int>` (FIFO) — wrong for nested subroutines. Now a
  `Stack<int>` (LIFO), which also backs `PUSH`/`POP`.

## Done so far

1. ✅ Restored `RideScriptFile` (loader + disassembler) into `source/OpenTPW/VM/` and
   re-enabled it in `RideVM` (`rsseqFile`, `Disassembly`).
2. ✅ `EnsureCommonVariables()` pads the variable table so minimal scripts don't
   index out of range during default init.
3. ✅ Recovered the `Test.RSE` fixture + `Test.rss` source from git history;
   added `RideScriptTests.LoadsTestScript` (parses header/config/strings/variables).
4. ✅ Implemented the arithmetic opcodes **MULT / DIV / MOD** (3 operands `a, b, dest`,
   set Zero/Sign flags, divide-by-zero guarded). Coverage **27 → 30 / 210**.
   Covered by `RideScriptTests.ArithmeticOpcodes`.
5. ✅ Made the call stack a proper **LIFO `Stack<int>`** (was a FIFO `Queue`), fixing
   nested `JSR`/`RETURN` and guarding `RETURN` underflow.
6. ✅ Implemented **END** (halts the VM via `IsRunning`), **CRIT_UNLOCK** (pairs the
   existing `CRIT_LOCK` no-op), and **PUSH / POP** (on the shared LIFO stack, underflow
   guarded). Operand signatures taken from the `OpenTPW.FileFormats` instruction doc.
   Coverage **30 → 34 / 210**. Covered by `RideScriptTests.EndHaltsExecution` and
   `RideScriptTests.StackIsLifoAndGuarded`.

> Note: the instruction doc describes `CMP` as a *bitwise-AND* comparison, but the current
> `Math.Compare` does equality/less-than. Left as-is for now (changing it could shift branch
> behavior); flagged here as a candidate to verify against `RSSEQCompiler`/Ghidra.

## Spec sources (the gu3.me docs site is behind Cloudflare — use these instead)

- **Instruction semantics**: `OpenTPW/OpenTPW.FileFormats` repo →
  `src/content/docs/vm/instructions.md` (per-opcode operand signatures + descriptions).
- **Authoritative ISA / operand handling**: `OpenTPW/RSSEQCompiler` repo
  (compiler + decompiler source) and `OpenTPW/opentpw-docs` (archived).

## Remaining work

1. Harden `BranchTo` (offset table → clean instruction index).
2. Implement the remaining opcodes in batches (animations, events, walk/limbo, timers).
   Many are ride-engine side-effects (objects, anims, sound) that need engine hooks;
   the pure value/flag ops (like the arithmetic batch) are the low-risk ones to do first.
3. Execute scripts on real rides and cover with more `.RSE` samples (inside the `.WAD`s).

## Reverse-engineering aid

Use **Ghidra** on an unprotected `TP.EXE` to validate the semantics of uncertain
opcodes — see [../05-ghidra-reverse.md](../05-ghidra-reverse.md).

## Affected files

`source/OpenTPW/VM/` (the whole folder).
