# 05 — Ghidra: Installation and Reverse-Engineering Roadmap

## Installation performed

- **Ghidra 12.1.2 PUBLIC** downloaded and extracted to:
  `~/ghidra_12.1.2_PUBLIC/`
- Launcher: `~/ghidra_12.1.2_PUBLIC/ghidraRun`
- **Java 21** required by Ghidra 12: **present** (`openjdk 21.0.11`).

```bash
~/ghidra_12.1.2_PUBLIC/ghidraRun      # launch the Ghidra UI
```

See [Linux.md](Linux.md) for the full install steps.

## Why Ghidra on this project?

OpenTPW is a re-implementation: to make progress on the **undocumented** formats
(marked ❌ in the README), we need to understand how the original game reads/writes
them. Ghidra is used to **disassemble/decompile the original binaries** and extract
the parsing logic — the source of truth when documentation is missing.

## Binaries worth analyzing (present on the disc)

| Binary | RE interest |
|--------|-------------|
| `TP.EXE` (266 KB) | Loader / entry point. **But** SafeDisc-protected → the real code is in `TP.ICD`. |
| `TP.ICD` (3.7 MB) | **Main encrypted executable (SafeDisc)**. Target #1 once decrypted/dumped. |
| `QMIXER.DLL` | Bullfrog audio engine → useful for `.SDT`/sounds. |
| `WEA*.DLL` | Online modules (chat, mail, news, RAS…) → useful for the multiplayer aspect. |
| `IP.EXE` | Internet Play. |

> ⚠️ `TP.ICD` is **SafeDisc-encrypted**. To analyze it you need a **decrypted in-memory
> dump** (the on-disk binary won't decompile directly). A legitimate approach for
> preservation/interoperability: run with the protection disabled and dump the process.
> **Out of scope** here; do this separately.
>
> Often simpler: the **GOG / no-CD** release of the game ships an unprotected `TP.EXE`
> that is far easier to load into Ghidra. Prefer it for reversing the formats.

## Recommended Ghidra workflow

1. Create a Ghidra project, import the unprotected binary (ideally the GOG/no-CD EXE).
2. Let auto-analysis run (PE x86).
3. Find loader routines via **strings**: extensions `.MTR`, `.BF4`, `.LIPS`, `.TQI`,
   `.RSE`, or header magic numbers.
4. Cross-check with a **hex editor** on the real `DATA/` files to validate header layout.
5. Document each format under `docs/`, then implement it in `OpenTPW.Files/Formats/`.

## Priority RE targets (aligned with the project's ❌/⚠️)

| Target | OpenTPW status | Samples on the disc |
|--------|:--------------:|---------------------|
| `.RSE` VM opcodes (~180 missing) | ⚠️ | ride scripts inside the `.WAD`s |
| `.TQI` / `.TGQ` video | ❌ | `DATA/MOVIES/*.TGQ` (BUB, JUG, ROLL…) |
| `.MTR` materials | ❌ | inside the `.WAD`s |
| `.BF4` fonts | ❌ | `FONTS.WAD` |
| `.LIPS` lip-sync | ❌ | `GLOBAL/SPEECH` |
| `.PLB` particles | (not listed) | `DATA/PARTICLE/TP2.PLB` (+ `PAR_LIB.H` = hints!) |

> Tip: `DATA/PARTICLE/PAR_LIB.H` is a **C header** left on the disc — it likely
> documents the particle format without even opening Ghidra.

## Legal framing

Reverse engineering for **interoperability / preservation** of an abandoned game, from
an owned copy. Do not redistribute original assets or binaries. OpenTPW itself requires
a legal copy of the game to run.
