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

## ✅ Status: UNBLOCKED via a no-CD build (2026-06-16)

The disc binaries are SafeDisc-encrypted (analysis below), but an **unprotected no-CD
`tp.exe`** (3.6 MB — the depacked `TP.ICD`; the title is abandonware, never re-licensed) was
imported into Ghidra 12.1.2 and analyzes cleanly: `.text` entropy 6.68, the codec data
sections (`idct_dat`, `grpoly_d`, `uva_data`) readable, and the format magics/strings present
in clear (`0x1CD15D46`, `.md2`×43, `M3D2`, `.rse`, `lips`, `.plb`…).

**Loaders reversed so far:**
- **`.MD2`** — `FUN_0046d6d0`: load-and-relocate; gates on version fields at offsets 4/8
  (current = `0xDD`/`0xCB`, legacy/static = `0x18`/`0x17`). Applied in `ModelFile` (T-015).
- **`.TQI` codec** — `FUN_00677140` is a **float AAN IDCT**; constants in `idct_dat`
  (`0.382683` = sin π/8, `0.541196`, `1.306563`, `1/(2√2)`). Dequant is `coeff × Q16 matrix`
  at `DAT_00fb7820` (AAN-prescaled). Findings in [T-021](tickets/T-021-tqi-exact-dequant.md)
  (deferred — the current decoder already renders correctly).
- **`.MTR`** — *no loader exists* (the magic `0x2E5915AF` and string `.mtr` are absent from
  the runtime). Model textures bind from the `.MD2` itself (`.md2`/`.tga`/`.wct` strings are
  present). So `.MTR` is a tool artifact, not a runtime format — [T-018](tickets/T-018-mtr-material-semantics.md).
- **`.RSE` VM** — loader `FUN_005587f0` (magic `"RSSE"` = `0x45535352`). The opcode descriptor
  table is at VA `0x765280` (`(name, operandCount)` pairs): **106 opcodes** with authoritative
  operand counts → [06-rse-vm-opcodes.md](06-rse-vm-opcodes.md), [T-007](tickets/T-007-vm-opcodes-rse.md).

Headless workflow used:
```bash
~/ghidra_12.1.2_PUBLIC/support/analyzeHeadless <proj> tpw -import tp.exe
# then a GhidraScript (FindMagic.java) to locate functions referencing a magic constant
~/.../analyzeHeadless <proj> tpw -process tp.exe -noanalysis \
    -scriptPath <dir> -postScript FindMagic.java <outdir> 0x1CD15D46 0x2E5915AF
```

---

## Appendix: the disc binaries are SafeDisc-encrypted (verified 2026-06-16)

For the record — why the *disc's* `tp.exe`/`TP.ICD` can't be analyzed directly (use the no-CD
build above instead). **The disc's game code is SafeDisc-encrypted and cannot be read
statically.**

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
| `.MTR` materials | ✅ resolved | *no runtime loader* — texture binding is in the `.MD2` | [T-018](tickets/T-018-mtr-material-semantics.md) |
| `.LIP` lip-sync | ⚠️ timestamps (µs) | mouth-shape encoding | [T-020](tickets/T-020-lip-mouth-shapes.md) |
| `.MD2` models | ⚠️ animated done | the static (frameCount-0) header | [T-015](tickets/T-015-md2-static-variant.md) |
| `.MAP` catalogs | ⚠️ names + SFX header | BANK record fields + SFX per-sound list | [T-016](tickets/T-016-map-entry-records.md) |
| `.RSE` VM opcodes | ⚠️ 34/106 | per-opcode semantics (table + arities recovered) | [T-007](tickets/T-007-vm-opcodes-rse.md) |

## Legal framing

Reverse engineering for **interoperability / preservation** of an abandoned game, from
an owned copy. Do not redistribute original assets or binaries. OpenTPW itself requires
a legal copy of the game to run.
