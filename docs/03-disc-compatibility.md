# 03 — Is the provided `.7z` compatible with OpenTPW?

**Short answer: YES.** It is the original Theme Park World installation CD, and it
contains exactly the assets OpenTPW reads.

## Archive contents

`jeu-02988-theme_park_world-pcwin.7z` (563 MiB):

```
Theme Park World/
  Theme Park World.ccd   (772 B)            ← CloneCD control file
  Theme Park World.img   (781 MB / 746 MiB) ← raw disc image
  Theme Park World.sub   (31 MB)            ← subchannels
Lisez-Moi.txt            (6 KB)             ← Abandonware-France notice (FR)
```

It is **not an `.iso`** but a **CloneCD image** (`.ccd`/`.img`/`.sub`).
Source: Abandonware-France (game #02988, FR PC release).

## Disc analysis

- **Type**: **data** CD-ROM, single session, 1 data track
  (`.ccd` → `Control=0x04`). Raw 2352-byte sectors (Mode 1, data at offset 16).
- **ISO9660 volume label**: `TPWorld`.
- **Root**: a classic Bullfrog/EA installer — `SETUP.EXE`, `AUTORUN.EXE`,
  `TP.EXE` + `TP.ICD`, language files (`FRENCH`, `GERMAN`, `ENGLISH`, `DANISH`,
  `SWEDISH`), help (`FRHELP.HLP`…), and most importantly the **`DATA/`** folder.

### `DATA/` — the assets OpenTPW needs

```
DATA/
  ESPRITES.WAD (10 MB)  FONTS.WAD     LOBBY.WAD     UI.WAD
  *.SAM (HIGH/MED/LOW/SOUND/ONLINE/CHALLENGE…)
  2DMAP/  ADVISOR/  GENERIC/  GLOBAL/{SOUND,SPEECH}  INIT/{400..1024}
  LANGUAGE/ENGLISH/
  LEVELS/{JUNGLE, FANTASY, HALLOW, SPACE}  + STANDARD.SAM, ONLINE.SAM
  MOVIES/*.TGQ (videos)  PARTICLE/*.PLB  POSTCARD/*.TGA  UI/CURSORS
```

This is **exactly** what the code expects:
- `Client/Game.cs` mounts `GamePath/data/` and registers the `.WAD` handler.
- `World/Terrain/Terrain.cs` reads `levels/jungle/terrain/textures/...wct`.
- `World/Level.cs` reads `/levels/{name}/global.sam`.

→ **The disc is a 100% correct asset source.**

## How to use it

The `DATA/` folder is **directly readable** on the disc (not packed inside a
compressed installer). Two paths:

### Path A — Extract / mount the image (recommended, no Windows)
1. Extract the `.img` from the archive:
   `7z e "jeu-02988-theme_park_world-pcwin.7z" "Theme Park World/Theme Park World.img"`
2. Mount the raw image (Mode 1, 2352-byte sectors → converting to a 2048 `.iso` with
   `ccd2iso`/`bchunk` is easiest, otherwise mount with an offset) **or** copy the
   `DATA/` folder directly.
3. Point OpenTPW's **`GamePath`** setting at the folder containing `data/`.
   - Default (Windows): `C:\Program Files (x86)\Bullfrog\Theme Park World`
     (`Settings.Designer.cs`). Change it to your path.
   - ⚠️ On Linux, mind the case-sensitivity and the hardcoded `\` separators — see [04](04-linux-compatibility.md).

### Path B — Classic install (Windows or Wine)
Run `SETUP.EXE` (via Wine on Linux). The installer copies `DATA/` and creates the
long/lowercase filenames expected by the original game. **No need for the SafeDisc
protection** since we never launch `TP.EXE`.

## Caveat: case & 8.3 names

On the CD (ISO9660), names are **UPPERCASE 8.3** (`ESPRITES.WAD`, `CHALLE~1.SAM`).
OpenTPW references **lowercase** paths (`levels/jungle/...`). On a case-sensitive
Linux FS, plan for normalization (Wine install, or rename to lowercase after extraction).

## Conclusion

| Question | Answer |
|----------|--------|
| Is it the right game? | ✅ Yes — Theme Park World, label `TPWorld`. |
| Does it contain the required assets? | ✅ Yes — `DATA/` with `.WAD`/`.SAM`/`LEVELS`. |
| Directly usable format? | ⚠️ CloneCD image → extract/mount first. |
| Need to bypass SafeDisc? | ❌ No — we never run `TP.EXE`. |
