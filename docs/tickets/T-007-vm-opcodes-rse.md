# T-007 — Ride VM: re-enable the `.RSE` loader and complete the opcodes

- **Priority**: 🟡 Feature (core of ride gameplay)
- **Type**: Feature / reverse engineering
- **Status**: ⚠️ **Partial — 97 / 106 opcodes.** The `.RSE` loader / disassembler is restored and
  working; `RideVM` parses + runs scripts. **Batch A (all 43 `pure`) complete**; Batch B is nearly done —
  object spawn/lifecycle, the animation + `WAIT*` family, sound, the rider-scream family, the **limbo**,
  **cross-VM**, **light**, and now the **walk family**
  (`WALKON`/`WALKOFF`/`WALKGET`/`WALKST_FLOAT`/`WALKFLOATSTAT`/`WALKFLOATSTOP` — a per-VM walk-slot
  scheduler + float timer, RE'd from op_76..op_81) are all in and tested. **Remaining: 9 opcodes** — heads
  (2), effects/particles (4), and the motion ops `TURBO`/`TOUR`/`BUMP` (see docs/06).

## Findings

- ~~`RideVM.cs`: the `.RSE` file loader is commented out.~~ **Fixed** — see below.
- **34 opcode handlers** implemented (`VM/Handlers/`) out of **106** total (~32%). The VM has
  **exactly 106 opcodes (0–105)** — confirmed from the binary's opcode table (see below); the
  old "210" was a miscount.
- The full opcode table (index, name, **operand count**, status) is now ground-truthed in
  [../06-rse-vm-opcodes.md](../06-rse-vm-opcodes.md).
- `BranchTo` is marked "HACK" (manual offset conversion, fragile).
- The call stack was a `Queue<int>` (FIFO) — wrong for nested subroutines. Now a
  `Stack<int>` (LIFO), which also backs `PUSH`/`POP`.

## Opcode table recovered via Ghidra (no-CD `tp.exe`)

The loader `FUN_005587f0` checks magic `"RSSE"` (`0x45535352`) and allocates a 0xF4-byte VM
struct. The VM's **opcode descriptor table** is at VA `0x765280`: a `(char* name, char* opCount)`
pointer pair per opcode, 106 entries (0–105). This yields the authoritative **operand count**
for every opcode — exactly what the reflection dispatcher needs (a wrong arity throws at
runtime). Classification: **43 `pure`** (VM-state only — implementable now) and **63 `engine`**
(ride-engine side effects — blocked).

**Bug it caught:** `POP` was implemented with 0 operands (from a doc); the binary table says
**1 operand** (pop into a destination). Fixed — `POP <dest>` now writes the popped value.

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
   guarded). Coverage **30 → 34 / 106**. Covered by `RideScriptTests.EndHaltsExecution` and
   `RideScriptTests.StackIsLifoAndGuarded`.
7. ✅ Recovered the **opcode table from the binary** (106 opcodes + operand counts), corrected
   the total (`210 → 106`), and **fixed the `POP` arity** (1 operand, not 0).
8. ✅ **Batch A (time) implemented** from the executor `FUN_00551cb0`: the date opcodes
   `YEAR/MONTH/DAY/HOUR/MIN/SEC` read the wall clock and return raw C `tm` fields (year−1900,
   month 0–11; offsets confirmed: sec@0, min@4, hour@8, mday@0xC, mon@0x10, year@0x14), and
   `GETTIME`/`SETTIMER`/`GETTIMER` use the ride's game clock (`SETTIMER` expiry = now+value,
   `GETTIMER` = max(0, expiry−now)). New `Handlers/Time.cs`; the VM gained an injectable
   `WallClock`, `GameTime`, and `Timer`. Coverage **34 → 42 / 106**. Covered by
   `RideScriptTests.DateTimeOpcodes` / `TimerOpcodes`.
9. ✅ **Child/parent variable opcodes** (`SET/GETVARIN{CHILD,PARENT}`) from the executor: each
   resolves the linked VM (the original matches a handle in a global VM list — struct +0x0C
   child, +0x10 parent — and indexes its variables at +0x1C, bounded by +0x8C). Modelled with
   a clean VM hierarchy (`RideVM.Parent` / `RideVM.ActiveChild`), which `SPAWNCHILD` will
   populate. Operand order confirmed: `SET(index, value)`, `GET(dest, index)`. Coverage
   **42 → 46 / 106**. Covered by `RideScriptTests.ChildParentVariableOpcodes`.
10. ✅ **WAIT / WAITABS** (the wait scheduler) from the executor: each arms a wake time
    (`struct +0xa0 = now + duration`) and **rewinds the PC** so the instruction re-runs every
    tick until the game clock reaches it, then falls through. Modelled with `RideVM.WaitUntil`
    and `CurrentPos--`. (`WAITABS` adds the operand raw; `WAIT` scales it by a runtime framerate
    factor in the original — same units here pending the engine clock.) Coverage **46 → 48 /
    106**. Covered by `RideScriptTests.WaitSuspendsUntilGameTime`.

11. ✅ **HUSH / HOP** — turned out **not** to be sound (the name misled). The executor shows a
    **double-ended stack**: one backing buffer (`+0x20`, capacity `+0x54`) where PUSH/POP grow
    down from the top (index `+0x40`) and HUSH/HOP grow up from the bottom (index `+0x44`) — two
    independent LIFOs. Modelled with a second `RideVM.HushStack`: `HUSH` pushes a value, `HOP`
    pops into dest (underflow-guarded). Coverage **48 → 50 / 106**. Covered by
    `RideScriptTests.HushHopSecondaryStack`.

**Batch A is COMPLETE** — all **43 `pure` opcodes** are implemented and unit-tested.

12. ✅ **SPAWNCHILD** (first Batch B opcode) — reversed from the executor: its operand is a
    **string** naming a child `.RSE`; the original builds a path and calls the script loader
    (`FUN_005587f0`) to create a child VM. Implemented at the VM level: it resolves the name and
    defers the load to an injectable `RideVM.ChildLoader` (the engine supplies the VFS loader;
    tests inject a fake), then links `Parent`/`ActiveChild`/`Children`. This makes the
    child/parent-var opcodes usable in real scripts. Coverage **50 → 51 / 106**. Covered by
    `RideScriptTests.SpawnChildLinksAndDrivesChildVars`.

13. ✅ **Limbo family** (`LIMBO`/`UNLIMBO`/`FORCEUNLIMBO`/`INLIMBO`/`LIMBOSPACE`, opcodes 58–62) — reversed
    from the executor cases (`op_58..op_62`): the limbo table lives in the **VM struct** (`+0x24` entries,
    count `+0x60`, capacity `+0x58`), not the engine, so these are **pure VM** despite the old "engine"
    tag. `LIMBO(value, secs)` parks `(value, now+secs×1000)` in a free slot (result 1/0);
    `UNLIMBO(dest)` releases the first **expired** entry; `FORCEUNLIMBO(dest)` the first regardless;
    `INLIMBO(dest)` = count; `LIMBOSPACE(dest)` = free slots. Each sets the Zero/Sign flags from its
    result (the original's `+0x48` register). New `RideVM.Limbo` list + `Handlers/Limbo.cs`. Coverage
    **78 → 83 / 106**. Covered by `RideScriptTests.LimboOpcodes`.

14. ✅ **Cross-VM family** (`FINDSCRIPTRAND` 90, `GETREMOTEVAR` 91, `SETREMOTEVAR` 92, `REMOVECHILD` 65) —
    reversed from the executor cases + the resolver `FUN_0055a070` (walks the global VM list `DAT_008791b0`
    matching the per-VM handle at struct `+0x08`). Modelled with a **global VM registry** keyed by an
    incrementing handle (weak refs so finished VMs drop out): `RideVM.Registry.cs` (`Handle`, `Resolve`,
    `MatchingByName`, `Unregister`, injectable `RandomIndex`); every VM joins on construction.
    `GETREMOTEVAR(dest, handle, index)` / `SETREMOTEVAR(handle, index, value)` resolve a VM by handle and
    read/write its `Variables` (bounds-checked); `FINDSCRIPTRAND(name, dest)` returns a random live VM
    whose `ScriptName` matches the string operand (0 if none); `REMOVECHILD` destroys + unregisters the
    active child and clears the link. Pure VM/registry state — no engine needed. Coverage **83 → 87 /
    106**. Covered by `RideScriptTests.CrossVmRemoteVarsAndFindScript` / `RemoveChildDetachesAndUnregisters`.

15. ✅ **Light family** (`ENABLELIGHT` 82, `DISABLELIGHT` 83, `SETLIGHT` 84, `COLOURLIGHT` 85) — reversed
    from op_82..op_85: each resolves a ride light object by the script id (a type-`0x20000` object) and
    toggles it / sets its intensity / colour. The brightness + colour operands are integer **percentages**
    scaled by `_DAT_00700fe0 = 0.01` (0..100 → 0..1) before the light setters (`FUN_004587e0` /
    `FUN_00458890`). Added to `IRideEngine` (`EnableLight`/`DisableLight`/`SetLight`/`ColourLight`),
    implemented in `RideEngine` with a per-id light state + an **emissive colour proxy** at the light's
    position (our renderer is unlit, so the proxy is the visible stand-in; real per-pixel lighting is a
    renderer follow-up). New `Handlers/Lights.cs` (dispatch + 0.01 scale). Coverage **87 → 91 / 106**.
    Routing + scale covered by `RideEngineTests.LightOpcodesRouteToEngineWithPercentScale`; the engine
    proxy path is exercised in-game via the `OPENTPW_AUTOPLACE` light drive (no ride *script* uses lights —
    they're for scenery objects, which aren't loaded yet — so the diagnostic drives a placed ride's engine
    directly; verified: `ENABLELIGHT 1/2` then `DISABLELIGHT 2`, proxy renders, no exceptions).

16. ✅ **Walk family** (`WALKON` 76, `WALKOFF` 77, `WALKGET` 78, `WALKST_FLOAT` 79, `WALKFLOATSTAT` 80,
    `WALKFLOATSTOP` 81) — reversed from op_76..op_81 + the helpers `FUN_00556f40` (add), `005571a0`
    (release), `00557110` (retrieve), `00557160` (float). It's a per-VM **walk-slot table** (the original's
    struct `+0x2c` array, count `+0x7c` = config `WalkSize`) + a **walk-float timer** (`+0x80`/`+0x84`) —
    pure VM bookkeeping, like limbo. Modelled in `RideVM.Walk.cs` with the slot lifecycle
    Free→WalkingOn→Arrived→Releasing→Done driven by a per-tick `WalkAdvance()` (wired into `Update`), plus
    `Handlers/Walk.cs`. WALKON parks a peep walking between two nodes, WALKOFF reverses it, WALKGET hands
    back a finished peep; the float ops drive the single-slot timer. Coverage **91 → 97 / 106**. Covered by
    `RideScriptTests.WalkOpcodes`; no in-game regression from the per-tick advance (verified via autoplace).
    *Caveats (documented in the source):* the **visual** glide along walk-node world positions + the
    `atan2` facing need the ride's walk-node geometry, which isn't decoded yet; and WALKON's 7-operand
    order follows `FUN_00556f40`'s param order, unverified (no shipped `.rse` uses the walk opcodes).

**Remaining: 9 opcodes**, all needing engine subsystems:
- **Heads** (`ADDHEAD`/`DELHEAD`), **effects** (`ADDOBJ_EXT`/`SPARK`/`REPAIREFFECT`/`GETCUSTPTCLCODE`), and the motion ops `TURBO`/`TOUR`/`BUMP`.

These create/drive world entities or particles, so (unlike the limbo / cross-VM / walk-slot bookkeeping,
which is pure VM) they need ride-engine subsystems to build + verify. Reverse each from its executor case
(`op_<n>` in `FUN_00551cb0`) when its subsystem comes online.

> Note: the instruction doc describes `CMP` as a *bitwise-AND* comparison, but the current
> `Math.Compare` does equality/less-than. Left as-is for now (changing it could shift branch
> behavior); flagged here as a candidate to verify against `RSSEQCompiler`/Ghidra.

## Spec sources (the gu3.me docs site is behind Cloudflare — use these instead)

- **Instruction semantics**: `OpenTPW/OpenTPW.FileFormats` repo →
  `src/content/docs/vm/instructions.md` (per-opcode operand signatures + descriptions).
- **Authoritative ISA / operand handling**: `OpenTPW/RSSEQCompiler` repo
  (compiler + decompiler source) and `OpenTPW/opentpw-docs` (archived).

## How this is split (the answer to "is T-007 a mono-ticket?")

T-007 stays the **VM epic** (loader, dispatch, stack, branches — all done). The remaining
opcode work splits along the only axis that matters — **realizable now vs engine-blocked** —
using the recovered table:

- **Batch A — `pure` opcodes (43, implementable + unit-testable now):** timers
  (`SETTIMER`/`GETTIMER`), date/time (`YEAR`…`SEC`, `GETTIME`), child/parent variables
  (`SET/GETVARIN{CHILD,PARENT}`), `WAIT`/`WAITABS` (VM-side timing), and the remaining pure
  value ops. Operand counts are known, so each is safe to add. → its own focused ticket once
  the per-opcode semantics are read from `FUN_005587f0`'s sibling executor in Ghidra.
- **Batch B — `engine` opcodes (63, blocked):** objects, animations, sound, lights,
  walk/limbo, scream, bounce side-effects. These need ride-engine subsystems that don't exist
  yet; tracked here under T-007, not spun into blocked tickets.

So: **one split (pure vs engine)**, not per-opcode. Full per-opcode status in
[../06-rse-vm-opcodes.md](../06-rse-vm-opcodes.md).

## Remaining work

1. Read the **executor** (the sibling of `FUN_005587f0`) in Ghidra to get per-opcode semantics
   for Batch A, then implement + test that batch.
2. Harden `BranchTo` (offset table → clean instruction index).
3. Batch B as the ride engine comes online; cover with more `.RSE` samples (inside the `.WAD`s).

## Reverse-engineering aid

The opcode table is at VA `0x765280` in the no-CD `tp.exe`; the loader is `FUN_005587f0`.
See [../05-ghidra-reverse.md](../05-ghidra-reverse.md).

## Affected files

`source/OpenTPW/VM/` (the whole folder).
