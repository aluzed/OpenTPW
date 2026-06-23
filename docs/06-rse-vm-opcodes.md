# 06 — RSE VM opcode table (ground truth from the binary)

The ride-script VM (`.RSE` / RSSEQ) has **exactly 106 opcodes (0–105)**, extracted from the
original `tp.exe` (no-CD) via Ghidra: a `(name, operandCount)` array at VA `0x765280`,
dispatched by the executor `FUN_00551cb0` (opcode word tagged `0x80`, bounds `< 0x6A`, jump
table at `0x5567d8`). The **Operands** column is authoritative (the reflection dispatcher must
match it). **Kind**: `pure` = VM-state only (done); `engine` = needs the ride engine.

Status: **97 / 106 implemented** — **Batch A (all 43 `pure`) complete**, plus a growing set of
Batch B engine opcodes routed through `IRideEngine` (object spawn/lifecycle, sound, the animation +
`WAIT*` family, and now the **rider scream family** `STARTSCREAM`/`STOPSCREAM`/`SINGLESCREAM`/
`SCREAMLEVEL`). See [tickets/T-007](tickets/T-007-vm-opcodes-rse.md) / [T-032](tickets/T-032-ride-engine.md).

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
| 13 | `EVENT` | 3 | ✅ | engine | ride event dispatch (routed; full sound/effect map is a follow-up) |
| 14 | `EVENT_EXT` | 4 | ✅ | engine | as EVENT with an extra parameter |
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
| 42 | `HUSH` | 1 | ✅ | pure |
| 43 | `HOP` | 1 | ✅ | pure |
| 44 | `WAIT` | 1 | ✅ | pure |
| 45 | `WAITABS` | 1 | ✅ | pure |
| 46 | `WAIT4ANIM` | 0 | ☐ | engine |
| 47 | `ADD` | 2 | ✅ | pure |
| 48 | `MULT` | 3 | ✅ | pure |
| 49 | `DIV` | 3 | ✅ | pure |
| 50 | `MOD` | 3 | ✅ | pure |
| 51 | `TURBO` | 1 | ☐ | engine |
| 52 | `END` | 0 | ✅ | pure |
| 53 | `TOUR` | 2 | ☐ | engine |
| 54 | `BUMP` | 2 | ☐ | engine |
| 55 | `COAST` | 2 | ✅ | engine | coaster control, multiplexed by subcommand (1 load·2 can-load?·3 wants-off?·4 state·5 mode·6 capacity·7 worn·8 init); queries set the Zero flag |
| 56 | `ADDHEAD` | 1 | ☐ | engine |
| 57 | `DELHEAD` | 1 | ☐ | engine |
| 58 | `LIMBO` | 2 | ✅ | engine | park a value in the per-VM timed limbo list (expiry = now+secs); pure VM state |
| 59 | `UNLIMBO` | 1 | ✅ | engine | release the first **expired** limbo value into dest |
| 60 | `FORCEUNLIMBO` | 1 | ✅ | engine | release the first limbo value into dest regardless of expiry |
| 61 | `INLIMBO` | 1 | ✅ | engine | read the limbo count into dest |
| 62 | `LIMBOSPACE` | 1 | ✅ | engine | read the free limbo slots into dest |
| 63 | `SPAWNCHILD` | 1 | ✅ | engine |
| 64 | `SPAWNSOUND` | 1 | ☐ | engine |
| 65 | `REMOVECHILD` | 0 | ✅ | engine | destroy the active child VM + clear the child link (pure VM/registry) |
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
| 76 | `WALKON` | 7 | ✅ | engine | send a peep walking between two walk nodes (VM walk-slot table; visual glide needs node geometry) |
| 77 | `WALKOFF` | 1 | ✅ | engine | start a walking peep heading back off |
| 78 | `WALKGET` | 1 | ✅ | engine | retrieve a peep that finished walking off into dest (0 if none) |
| 79 | `WALKST_FLOAT` | 3 | ✅ | engine | start the walk-float timer (value, p3, p4) |
| 80 | `WALKFLOATSTAT` | 1 | ✅ | engine | read the walk-float value into dest |
| 81 | `WALKFLOATSTOP` | 0 | ✅ | engine | finalise the walk-float timer |
| 82 | `ENABLELIGHT` | 1 | ✅ | engine | turn on ride light `id` (engine: emissive colour proxy — renderer is unlit) |
| 83 | `DISABLELIGHT` | 1 | ✅ | engine | turn off ride light `id` |
| 84 | `SETLIGHT` | 2 | ✅ | engine | set light `id` brightness (operand 0..100 ÷100 → 0..1) |
| 85 | `COLOURLIGHT` | 4 | ✅ | engine | set light `id` colour (r,g,b each 0..100 ÷100 → 0..1) |
| 86 | `STARTSCREAM` | 2 | ✅ | engine | `(soundCode, level 0..100)` — sustained rider scream |
| 87 | `STOPSCREAM` | 0 | ✅ | engine | end the sustained scream |
| 88 | `SINGLESCREAM` | 2 | ✅ | engine | `(soundCode, level)` one-shot (`-1` = default level) |
| 89 | `SCREAMLEVEL` | 1 | ✅ | engine | set scream volume `0..100` |
| 90 | `FINDSCRIPTRAND` | 2 | ✅ | engine | random live script matching a name string → its handle in dest (pure VM/registry) |
| 91 | `GETREMOTEVAR` | 3 | ✅ | engine | read var[index] from the VM with the given handle into dest |
| 92 | `SETREMOTEVAR` | 3 | ✅ | engine | write var[index] in the VM with the given handle |
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
| 103 | `SETREVERB` | 1 | ✅ | engine | audio reverb (routed) |
| 104 | `DIPMUSIC` | 1 | ✅ | engine | duck background music (routed) |
| 105 | `SPARK` | 4 | ☐ | engine |
_Generated from the binary opcode table; method in docs/05-ghidra-reverse.md._
