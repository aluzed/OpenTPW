# T-016 — `.MAP` audio catalog: decode the remaining records

- **Priority**: 🟡 Feature
- **Type**: Reverse engineering
- **Status**: ✅ **Decoded.** Variant detection, the BANK name table, the SFX category header, **and
  the SFX per-sound record table** all decode and validate across every `Data/global/sound/cat_*`
  sample. The BANK 11-byte records were RE'd (Ghidra) to be **serialized object pointers, not catalog
  data**. The only thing still raw is the SFX trailing **mixing-curve blob** (a serialized object graph),
  which isn't needed to use the catalog. See "Done".
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

## Done (this pass)

- **BANK 11-byte records resolved (Ghidra)**: `A` is *not* a name hash or sound id — it's a stale
  **`.text` code pointer** baked in when the catalog was serialized (the `A` values `0x00494A4C`,
  `0x00478A74`, … and the recurring constant `0x0066F22C` all land in the executable's code section,
  verified by a memory peek; "Ride" and "xKids" share the same `A` because they're the same serialized
  object). So the records carry **no catalog data** — the trailing name list is the real content. Doc
  in `MapFile` updated to say so.
- **SFX per-sound record table decoded**: after the category header come exactly `SoundEntryCount`
  fixed **20-byte** records (count == `SoundEntryCount` verified on rides/staff/lobby samples). Layout:
  `uint32 soundId` · `uint32 variationCount` · `uint32` reserved(0) · `uint32 param` · `uint32 flags`.
  Exposed as `MapFile.SoundEntries` (`MapSoundEntry`). E.g. `cat_ridesSFX`: 25 entries, ids
  18/14/17/12/24/184/…, mostly 1 variation, param 3200/2300. `MapFileTests` assert the decode on a
  synthetic file and on real BANK + SFX samples (`TPW_MAP_SAMPLE`).

## Remaining (optional)

- **SFX trailing mixing-curve blob**: a serialized DirectMusic object graph (embedded `.text`/data
  pointers + `0x6464`/`0x3232`/`0xFFFF` markers, like the BANK records) follows the 20-byte table. It
  holds per-sound mixing curves/envelopes. Kept raw; decoding it needs the engine and isn't required to
  *use* the catalog (the sound ids + variation counts + category defaults are the actionable data).

## Acceptance criteria

- ✅ Variant + SFX category header decode and validate against real samples.
- ✅ BANK records explained (serialized pointers, not data); SFX per-sound list decodes to typed fields.

## Affected files

`source/OpenTPW.Files/Public/MapFile.cs`, `source/OpenTPW.Tests/MapFileTests.cs`.
