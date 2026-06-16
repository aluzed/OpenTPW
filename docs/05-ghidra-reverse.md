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

## ⚠️ Status: blocked by SafeDisc (verified 2026-06-16)

The disc image was imported into Ghidra 12.1.2 and analyzed. **Result: the game code —
including every format loader — is SafeDisc-encrypted and cannot be read statically.**

Verified facts (from the disc's `tp.exe` + `TP.ICD`):

| Binary | Finding |
|--------|---------|
| `tp.exe` (266 KB) | SafeDisc **loader stub**. Imports `drvmgt.dll` + `wnaspi32.dll` (SafeDisc signatures). Its `.txt`/`.txt2` sections are entropy **8.00** (encrypted); no format magics or strings present. |
| `TP.ICD` (3.73 MB) | The **real game PE**, SafeDisc-encrypted. `.text` (3 MB) and `CSEG` entropy **~7.99**; even the "data" sections (`IDCT_DAT`, `UVA_DATA`, `GRPOLY_D`…) are encrypted — they show a repeating 8-byte cipher pattern (`31484,16099,-18087,-26905`) where the original held zero-runs. **No** loader code, magic (`0x1CD15D46`, `0x2E5915AF`), or table is readable. |

What *is* visible: the **PE section names** in `TP.ICD` survive unencrypted and confirm the
engine's components — `IDCT_DAT`, `TQIA_DAT` (TQI audio), `LBMPEG_D` (libmpeg), `GRPOLY_D`,
`UVA_DATA`. Useful as confirmation, not as data.

**Conclusion:** static Ghidra RE of the loaders is **blocked** on this disc. The OpenTPW
authors' existing magics/parsers must have come from a runtime-decrypted dump or an
unprotected build. To unblock, one of:

1. **GOG re-release** of Theme Park World — ships an unprotected `tp.exe`; loads straight into
   Ghidra. **Easiest path.**
2. A **runtime-decrypted dump** of `TP.ICD`: run the game on Windows with the protection
   disabled and dump the process image (the decrypted `.text`), then import that. Needs a
   Windows host; SafeDisc 1.x anti-debug + the `secdrv`/`drvmgt` ring-0 driver + CD
   authentication (uses the disc's weak-sector signature, i.e. the `.sub` subchannel) make a
   headless emulation unpack (Qiling/Unicorn) a large, low-odds effort — not attempted here.

Until then, the remaining loader-internals tickets (T-015 `.MD2` static variant, T-016 `.MAP`
records, T-018 `.MTR` semantics, T-019 `.PLB` params, T-020 `.LIP` shapes) can only progress by
**black-box sample analysis** — which has been pushed about as far as one sample set allows.

## Other binaries (not protected, limited RE interest)

| Binary | Note |
|--------|------|
| `Ip.exe` (832 KB) | Internet Play launcher — not packed, but no format loaders. |
| `QMixer.dll` (300 KB) | Audio mixer — not packed; relevant to `.SDT`/sound playback if needed. |
| `clokspl.exe` | Splash/clock helper — not packed, irrelevant. |
| `Acrobat …/… 4-Eng.exe` | **Adobe Acrobat Reader** (manual viewer), not a game binary. |

## Recommended Ghidra workflow

1. Create a Ghidra project, import the unprotected binary (ideally the GOG/no-CD EXE).
2. Let auto-analysis run (PE x86).
3. Find loader routines via **strings**: extensions `.MTR`, `.BF4`, `.LIPS`, `.TQI`,
   `.RSE`, or header magic numbers.
4. Cross-check with a **hex editor** on the real `DATA/` files to validate header layout.
5. Document each format under `docs/`, then implement it in `OpenTPW.Files/Formats/`.

## Priority RE targets once an unprotected binary is available

Black-box analysis has already taken these as far as one sample set allows; the **code-level
confirmation** below is what Ghidra would add (on a GOG/decrypted build).

| Target | Status | What Ghidra would still confirm | Ticket |
|--------|:------:|---------------------------------|--------|
| `.TQI`/`.TGQ` video | ✅ pixel-accurate | exact AAN dequant tables (`IDCT_DAT`/`UVA_DATA`) | [T-021](tickets/T-021-tqi-exact-dequant.md) |
| `.BF4` fonts | ✅ done | — | — |
| `.PLB` particles | ⚠️ names + colour ramp | the non-colour parameter fields | [T-019](tickets/T-019-plb-parameter-fields.md) |
| `.MTR` materials | ⚠️ header + index array | per-element semantics + texture binding | [T-018](tickets/T-018-mtr-material-semantics.md) |
| `.LIP` lip-sync | ⚠️ timestamps (µs) | mouth-shape encoding | [T-020](tickets/T-020-lip-mouth-shapes.md) |
| `.MD2` models | ⚠️ animated done | the static (frameCount-0) header | [T-015](tickets/T-015-md2-static-variant.md) |
| `.MAP` catalogs | ⚠️ names + SFX header | BANK record fields + SFX per-sound list | [T-016](tickets/T-016-map-entry-records.md) |
| `.RSE` VM opcodes | ⚠️ 34/210 | the unimplemented opcodes' semantics | [T-007](tickets/T-007-vm-opcodes-rse.md) |

## Legal framing

Reverse engineering for **interoperability / preservation** of an abandoned game, from
an owned copy. Do not redistribute original assets or binaries. OpenTPW itself requires
a legal copy of the game to run.
