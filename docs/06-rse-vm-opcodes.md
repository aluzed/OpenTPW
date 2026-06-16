# 06 — RSE VM opcode table (ground truth from the binary)

The ride-script VM (`.RSE` / RSSEQ) has **exactly 106 opcodes (0–105)**. This table is the
**ground truth**, extracted from the original `tp.exe` (no-CD) via Ghidra: a `(name, operandCount)`
pointer-pair array at VA `0x765280`, dispatched by the executor `FUN_00551cb0` (which checks the
`0x80`-tagged opcode word, bounds `< 0x6A`, then jumps via the table at `0x5567d8`). The old
"210" figure double-counted the descriptor array's two pointers per opcode.

The **Operands** column is authoritative and is what the reflection dispatcher
(`OpcodeHandlerAttribute`) must match — a handler with the wrong arity throws at runtime.

- **Implemented**: a handler exists in `source/OpenTPW/VM/Handlers/`.
- **Kind**: `pure` = realizable with VM state only (arithmetic, flags, stack, variables,
  timers, date, VM hierarchy) — implementable and unit-testable now. `engine` = a ride-engine
  side effect (objects, animations, sound, lights, walk/limbo, scream…) that needs engine
  subsystems that don't exist yet.

Status: **46 / 106 implemented**. Of all 106, **43 are `pure`** and **63 are `engine`**. The
remaining `pure` opcodes (`WAIT`/`WAITABS` need a scheduler; `HUSH`/`HOP`/`SETLV` need their
semantics read) are the tail of Batch A. See [tickets/T-007](tickets/T-007-vm-opcodes-rse.md).

| # | Opcode | Operands | Implemented | Kind |
|---|--------|:--------:|:-----------:|------|
| 0 | `NOP` | 0 | ✅ | pure |
| 1 | `CRIT_LOCK` | 0 | ✅ | pure |
| 2 | `CRIT_UNLOCK` | 0 | ✅ | pure |
| 3 | `COPY` | 2 | ✅ | pure |
| 4 | `SETLV` | 1 | ✅ | pure |
| 5 | `SUB` | 3 | ✅ | pure |
| 6 | `ENDSLICE` | 0 | ✅ | pure |
| 7 | `GETTIME` | 1 | ✅ | pure |
| 8 | `ADDOBJ` | 4 | ✅ | engine |
| 9 | `ADDOBJ_EXT` | 5 | ☐ | engine |
| 10 | `KILLOBJ` | 1 | ☐ | engine |
| 11 | `FADEOBJ` | 1 | ☐ | engine |
| 12 | `SETOBJPARAM` | 3 | ☐ | engine |
| 13 | `EVENT` | 3 | ☐ | engine |
| 14 | `EVENT_EXT` | 4 | ☐ | engine |
| 15 | `FLUSHANIM` | 0 | ☐ | engine |
| 16 | `TRIGANIM` | 3 | ☐ | engine |
| 17 | `WAITANIM` | 2 | ☐ | engine |
| 18 | `LOOPANIM` | 2 | ☐ | engine |
| 19 | `TRIGWAITANIM` | 3 | ☐ | engine |
| 20 | `GETANIM` | 1 | ☐ | engine |
| 21 | `TRIGANIMSPEED` | 4 | ☐ | engine |
| 22 | `FLUSHANIM_CH` | 1 | ☐ | engine |
| 23 | `TRIGANIM_CH` | 4 | ☐ | engine |
| 24 | `WAITANIM_CH` | 3 | ☐ | engine |
| 25 | `LOOPANIM_CH` | 3 | ☐ | engine |
| 26 | `TRIGWAITANIM_CH` | 4 | ☐ | engine |
| 27 | `GETANIM_CH` | 2 | ☐ | engine |
| 28 | `RAND` | 2 | ✅ | pure |
| 29 | `JSR` | 1 | ✅ | pure |
| 30 | `RETURN` | 0 | ✅ | pure |
| 31 | `BRANCH` | 1 | ✅ | pure |
| 32 | `BRANCH_Z` | 1 | ✅ | pure |
| 33 | `BRANCH_NZ` | 1 | ✅ | pure |
| 34 | `BRANCH_NV` | 1 | ✅ | pure |
| 35 | `BRANCH_PV` | 1 | ✅ | pure |
| 36 | `DBGMSG` | 1 | ✅ | pure |
| 37 | `NAME` | 1 | ✅ | pure |
| 38 | `TEST` | 1 | ✅ | pure |
| 39 | `CMP` | 2 | ✅ | pure |
| 40 | `PUSH` | 1 | ✅ | pure |
| 41 | `POP` | 1 | ✅ | pure |
| 42 | `HUSH` | 1 | ☐ | pure |
| 43 | `HOP` | 1 | ☐ | pure |
| 44 | `WAIT` | 1 | ☐ | pure |
| 45 | `WAITABS` | 1 | ☐ | pure |
| 46 | `WAIT4ANIM` | 0 | ☐ | engine |
| 47 | `ADD` | 2 | ✅ | pure |
| 48 | `MULT` | 3 | ✅ | pure |
| 49 | `DIV` | 3 | ✅ | pure |
| 50 | `MOD` | 3 | ✅ | pure |
| 51 | `TURBO` | 1 | ☐ | engine |
| 52 | `END` | 0 | ✅ | pure |
| 53 | `TOUR` | 2 | ☐ | engine |
| 54 | `BUMP` | 2 | ☐ | engine |
| 55 | `COAST` | 2 | ☐ | engine |
| 56 | `ADDHEAD` | 1 | ☐ | engine |
| 57 | `DELHEAD` | 1 | ☐ | engine |
| 58 | `LIMBO` | 2 | ☐ | engine |
| 59 | `UNLIMBO` | 1 | ☐ | engine |
| 60 | `FORCEUNLIMBO` | 1 | ☐ | engine |
| 61 | `INLIMBO` | 1 | ☐ | engine |
| 62 | `LIMBOSPACE` | 1 | ☐ | engine |
| 63 | `SPAWNCHILD` | 1 | ☐ | engine |
| 64 | `SPAWNSOUND` | 1 | ☐ | engine |
| 65 | `REMOVECHILD` | 0 | ☐ | engine |
| 66 | `SETVARINCHILD` | 2 | ✅ | pure |
| 67 | `GETVARINCHILD` | 2 | ✅ | pure |
| 68 | `SETVARINPARENT` | 2 | ✅ | pure |
| 69 | `GETVARINPARENT` | 2 | ✅ | pure |
| 70 | `BOUNCESETNODE` | 1 | ✅ | engine |
| 71 | `BOUNCESETBASE` | 1 | ✅ | engine |
| 72 | `BOUNCE` | 2 | ✅ | engine |
| 73 | `UNBOUNCE` | 1 | ✅ | engine |
| 74 | `FORCEUNBOUNCE` | 1 | ✅ | engine |
| 75 | `BOUNCING` | 1 | ✅ | engine |
| 76 | `WALKON` | 7 | ☐ | engine |
| 77 | `WALKOFF` | 1 | ☐ | engine |
| 78 | `WALKGET` | 1 | ☐ | engine |
| 79 | `WALKST_FLOAT` | 3 | ☐ | engine |
| 80 | `WALKFLOATSTAT` | 1 | ☐ | engine |
| 81 | `WALKFLOATSTOP` | 0 | ☐ | engine |
| 82 | `ENABLELIGHT` | 1 | ☐ | engine |
| 83 | `DISABLELIGHT` | 1 | ☐ | engine |
| 84 | `SETLIGHT` | 2 | ☐ | engine |
| 85 | `COLOURLIGHT` | 4 | ☐ | engine |
| 86 | `STARTSCREAM` | 2 | ☐ | engine |
| 87 | `STOPSCREAM` | 0 | ☐ | engine |
| 88 | `SINGLESCREAM` | 2 | ☐ | engine |
| 89 | `SCREAMLEVEL` | 1 | ☐ | engine |
| 90 | `FINDSCRIPTRAND` | 2 | ☐ | engine |
| 91 | `GETREMOTEVAR` | 3 | ☐ | engine |
| 92 | `SETREMOTEVAR` | 3 | ☐ | engine |
| 93 | `REPAIREFFECT` | 1 | ☐ | engine |
| 94 | `GETCUSTPTCLCODE` | 2 | ☐ | engine |
| 95 | `SETTIMER` | 1 | ✅ | pure |
| 96 | `GETTIMER` | 1 | ✅ | pure |
| 97 | `YEAR` | 1 | ✅ | pure |
| 98 | `MONTH` | 1 | ✅ | pure |
| 99 | `DAY` | 1 | ✅ | pure |
| 100 | `HOUR` | 1 | ✅ | pure |
| 101 | `MIN` | 1 | ✅ | pure |
| 102 | `SEC` | 1 | ✅ | pure |
| 103 | `SETREVERB` | 1 | ☐ | engine |
| 104 | `DIPMUSIC` | 1 | ☐ | engine |
| 105 | `SPARK` | 4 | ☐ | engine |
_Generated from the binary opcode table; method in docs/05-ghidra-reverse.md._
