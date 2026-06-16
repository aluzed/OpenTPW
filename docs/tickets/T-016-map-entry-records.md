# T-016 — `.MAP` audio catalog: decode the remaining records

- **Priority**: 🟡 Feature
- **Type**: Reverse engineering
- **Status**: ☐ To do
- **Split from**: [T-012](T-012-partial-formats.md).

## Context

`.MAP` files are audio category catalogs (`CAT_*`), not terrain. `MapFile`
(`OpenTPW.Files/Public/MapFile.cs`) decodes the 16-byte category GUID and, for the **BANK**
variant (`…a0c993f203`), the trailing table of `count` length-prefixed entry names
(e.g. `Sound\Kids`, `Sound\UI`). Verified on every `Data/global/sound/cat_*BANK.map`.

## Remaining work

1. **BANK per-entry records**: between the `uint32 count` and the name table sit `count`
   fixed **11-byte records** (currently raw). Decode their fields (cross-reference the entry
   names; look for volume/flags/priority).
2. **SFX variant** (`…b0c993f203`): a different binary layout (`count` then float-bearing
   records — `0x3F800000` = 1.0f seen). Decode it.
3. Confirm the `cat_*` GUID → category-type mapping (ambient / rides / kids / staff / UI / speech).

## Acceptance criteria

- BANK records and SFX records decode to typed fields, validated against several real
  `cat_*` samples; extend `MapFileTests`.

## Affected files

`source/OpenTPW.Files/Public/MapFile.cs`, `source/OpenTPW.Tests/MapFileTests.cs`.
