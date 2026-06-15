# Linux Setup Guide

Step-by-step setup of the toolchain used to build OpenTPW and reverse-engineer the
original game on Linux. Validated on Ubuntu 24.04 (kernel 6.17) on **2026-06-15**.

The two pieces installed here:
1. **.NET 8 SDK** — to build/run OpenTPW.
2. **Ghidra 12.1.2** (+ JDK 21) — to reverse the original binaries.

Plus optional runtime libraries (SDL2/Vulkan) and disc-extraction tools.

---

## 1. .NET 8 SDK

### Option A — user-local install (no root, what was used here)

```bash
cd /tmp
curl -sSL https://dot.net/v1/dotnet-install.sh -o dotnet-install.sh
chmod +x dotnet-install.sh
./dotnet-install.sh --channel 8.0 --install-dir "$HOME/.dotnet"
```

Add it to your `PATH` (append to `~/.bashrc` to make it permanent):

```bash
export PATH="$HOME/.dotnet:$PATH"
export DOTNET_CLI_TELEMETRY_OPTOUT=1
export DOTNET_NOLOGO=1
```

Verify:

```bash
dotnet --version      # → 8.0.422 (or newer 8.0.x)
```

### Option B — system package (needs root)

```bash
sudo apt-get update
sudo apt-get install -y dotnet-sdk-8.0
```

---

## 2. Build & test OpenTPW

### Quick way — the dedicated Linux helper

A `build-linux.sh` script at the repo root wraps the dotnet calls (locates the SDK on
`PATH` or in `~/.dotnet`, sets telemetry opt-out):

```bash
./build-linux.sh            # build the solution (Debug)
./build-linux.sh -c Release # build in Release
./build-linux.sh test       # build + run the test suite
./build-linux.sh run        # build + run the OpenTPW game project
./build-linux.sh clean      # remove bin/ and obj/
```

> If `./build-linux.sh` is blocked by filesystem permissions, run `bash build-linux.sh`.

### Manual way

```bash
cd /var/www/reverse/OpenTPW/source
dotnet build OpenTPW.sln -c Debug      # → 6 projects, 0 errors (109 warnings)
dotnet test OpenTPW.Tests/OpenTPW.Tests.csproj --no-build
```

> A `global.json` pins the SDK to .NET 8 (any installed `8.0.x` works), and
> `source/Directory.Build.props` defines per-OS compilation constants
> (`WINDOWS` / `LINUX` / `OSX`) so platform-specific code can be guarded with
> `#if LINUX` once the Windows locks are addressed (see the tickets).

> Current state: the build succeeds, but tests fail (hardcoded paths + no game
> install). See [tickets/T-001](tickets/T-001-backslash-paths-linux.md) and
> [tickets/T-002](tickets/T-002-tests-absolute-paths.md).

### Runtime libraries (to actually launch the game window)

Veldrid uses Vulkan or OpenGL via SDL2. Install the runtime libs:

```bash
sudo apt-get install -y libsdl2-2.0-0 libvulkan1 mesa-vulkan-drivers libopenal1
```

> Note: even with these, the game will not run cleanly until the Windows locks are
> fixed (paths/NAudio/System.Drawing) — see [04-linux-compatibility.md](04-linux-compatibility.md).

---

## 3. Ghidra 12.1.2 (+ JDK 21)

Ghidra 12 requires **JDK 21**.

```bash
# JDK 21 (skip if 'java -version' already shows 21)
sudo apt-get install -y openjdk-21-jdk
java -version            # → openjdk version "21..."
```

Download & extract Ghidra:

```bash
cd /tmp
curl -sL -o ghidra.zip \
  "https://github.com/NationalSecurityAgency/ghidra/releases/download/Ghidra_12.1.2_build/ghidra_12.1.2_PUBLIC_20260605.zip"
unzip -q ghidra.zip -d "$HOME"      # → ~/ghidra_12.1.2_PUBLIC/
```

> For a newer release, check
> <https://github.com/NationalSecurityAgency/ghidra/releases/latest> and adjust the URL.

Launch:

```bash
~/ghidra_12.1.2_PUBLIC/ghidraRun
```

See [05-ghidra-reverse.md](05-ghidra-reverse.md) for the reverse-engineering workflow.

---

## 4. Disc-image extraction tools (the provided `.7z`)

```bash
sudo apt-get install -y p7zip-full      # provides 7z/7za
# optional, to convert CloneCD .img → .iso:
sudo apt-get install -y ccd2iso          # or 'bchunk'
```

Extract the disc image from the archive:

```bash
cd /var/www/reverse/OpenTPW
7z e "jeu-02988-theme_park_world-pcwin.7z" "Theme Park World/Theme Park World.img"
```

Convert and mount (one option):

```bash
ccd2iso "Theme Park World.img" tpw.iso     # CloneCD 2352-byte sectors → 2048 ISO
mkdir -p /tmp/tpw-cd
sudo mount -o loop,ro tpw.iso /tmp/tpw-cd
ls /tmp/tpw-cd/DATA            # the assets OpenTPW needs
```

> The `*.7z`, `*.img`, `*.iso`, `*.ccd`, `*.sub` patterns are git-ignored — never
> commit game data. See [03-disc-compatibility.md](03-disc-compatibility.md).

---

## 5. Point OpenTPW at the game data

OpenTPW expects `GamePath/data/` (default is a Windows path in
`source/OpenTPW/Settings.Designer.cs`). On Linux, copy/normalize the `DATA/` folder to
lowercase `data/` and set `GamePath` to its parent directory.

> A clean environment-variable override is proposed in
> [tickets/T-006](tickets/T-006-gamepath-config.md).

---

## Summary of installed components

| Component | Version | Location | Purpose |
|-----------|---------|----------|---------|
| .NET SDK | 8.0.422 | `~/.dotnet` | Build/run OpenTPW |
| JDK | 21.0.11 | system | Required by Ghidra |
| Ghidra | 12.1.2 PUBLIC | `~/ghidra_12.1.2_PUBLIC` | Reverse the original binaries |
| 7-Zip | 23.01 | system (`/usr/bin/7z`) | Extract the provided archive |
