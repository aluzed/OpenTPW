# T-016 — `.MAP` audio catalog: decode the remaining records

- **Priority**: 🟡 Feature
- **Type**: Reverse engineering
- **Status**: ⚠️ **Partial.** Variant detection + the SFX category header are decoded and
  verified across all `Data/global/sound/cat_*` samples; the per-record *mixing* fields
  (opaque) still need the engine. See "Done" / "Remaining" below.
- **Split from**: [T-012](T-012-partial-formats.md).

## Context

`.MAP` files are audio category catalogs (`CAT_*`), not terrain. `MapFile`
(`OpenTPW.Files/Public/MapFile.cs`) decodes the 16-byte category GUID and, for the **BANK**
variant (`…a0c993f203`), the trailing table of `count` length-prefixed entry names
(e.g. `Sound\Kids`, `Sound\UI`). Verified on every `Data/global/sound/cat_*BANK.map`.

## Done

- **Variant detection** (`MapFile.Variant`): the GUID byte at offset 11 distinguishes
  **BANK** (`0xA0`) from **SFX** (`0xB0`).
- **BANK record stride confirmed**: between the `uint32 count` and the name table sit exactly
  `count` fixed **11-byte records** (verified — the constant `0x0066F22C` recurs at a 11-byte
  period; record = `uint32 A` + that constant + `0x9A`,⟨varying byte⟩,`0x99`). `count` ==
  number of records == number of names on every sample.
- **SFX category header decoded** (`MapFile.SoundEntryCount`, `MapFile.CategoryParameters`):
  after the GUID + 8 reserved bytes come `categoryCount`(=1), `soundEntryCount` (4–49 across
  samples), a pad, a flags word, then **three float defaults — (1.0, 2.0, 0.5)** on every full
  SFX file (unity volume + a ±octave pitch range). Verified across all six
  `Data/global/sound/cat_*` files. Covered by `MapFileTests` (synthetic BANK + SFX, real
  samples of both variants via `TPW_MAP_SAMPLE`).

## Remaining work

1. **BANK 11-byte record fields**: `A` is opaque — it is *not* a name hash (the "Ride" and
   "xKids" entries share the same `A`), so the meaning (sound id? bank handle?) needs the
   engine. The constant + `0x9A`/`0x99` markers suggest a fixed packed struct.
2. **SFX per-sound list**: a variable/nested record list follows the category header (sound
   index + sub-records; the values 3200/2300 seen are not a reliable per-entry delimiter).
   Decode it into `{ soundId, volume, pitch, … }`.

Both need **Ghidra** on the DirectMusic-style catalog loader in `TP.EXE`
(see [05-ghidra-reverse.md](../05-ghidra-reverse.md)) — black-box analysis of one set of
samples isn't enough to assign the fields confidently.

## Acceptance criteria

- ✅ Variant + SFX category header decode and validate against real samples.
- ☐ BANK record fields and the SFX per-sound list decode to typed fields (needs Ghidra).

## Affected files

`source/OpenTPW.Files/Public/MapFile.cs`, `source/OpenTPW.Tests/MapFileTests.cs`.
