#!/usr/bin/env bash
#
# Dedicated Linux build/test/run helper for OpenTPW.
# See docs/Linux.md for the full toolchain setup and docs/04-linux-compatibility.md
# for the known Windows locks that still affect runtime.
#
# Usage:
#   ./build-linux.sh            # build the solution (Debug)
#   ./build-linux.sh build      # same as above
#   ./build-linux.sh -c Release # build in Release
#   ./build-linux.sh test       # build then run the test suite
#   ./build-linux.sh run        # build then run the OpenTPW game project
#   ./build-linux.sh clean      # remove bin/ and obj/ across the solution
#
set -euo pipefail

REPO_ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
SLN="$REPO_ROOT/source/OpenTPW.sln"
GAME_PROJ="$REPO_ROOT/source/OpenTPW/OpenTPW.csproj"

# --- Locate the .NET SDK (PATH first, then a user-local install) ---
if command -v dotnet >/dev/null 2>&1; then
  DOTNET="$(command -v dotnet)"
elif [ -x "$HOME/.dotnet/dotnet" ]; then
  DOTNET="$HOME/.dotnet/dotnet"
  export PATH="$HOME/.dotnet:$PATH"
else
  echo "error: 'dotnet' not found. Install the .NET 8 SDK — see docs/Linux.md." >&2
  exit 1
fi

export DOTNET_CLI_TELEMETRY_OPTOUT=1
export DOTNET_NOLOGO=1

# --- Parse a simple verb; remaining args are forwarded to dotnet ---
CONFIG="Debug"
VERB="build"
ARGS=()
while [ $# -gt 0 ]; do
  case "$1" in
    build|test|run|clean) VERB="$1"; shift ;;
    -c|--configuration)   CONFIG="$2"; shift 2 ;;
    *)                    ARGS+=("$1"); shift ;;
  esac
done

echo ">> dotnet: $DOTNET ($("$DOTNET" --version))"
echo ">> verb:   $VERB   config: $CONFIG"

case "$VERB" in
  clean)
    find "$REPO_ROOT/source" -type d \( -name bin -o -name obj \) -prune -exec rm -rf {} +
    echo ">> cleaned bin/ and obj/"
    ;;
  build)
    "$DOTNET" build "$SLN" -c "$CONFIG" "${ARGS[@]}"
    ;;
  test)
    "$DOTNET" test "$REPO_ROOT/source/OpenTPW.Tests/OpenTPW.Tests.csproj" -c "$CONFIG" "${ARGS[@]}"
    ;;
  run)
    # Tip: set OPENTPW_GAMEPATH or the GamePath setting to your installed game data.
    "$DOTNET" run --project "$GAME_PROJ" -c "$CONFIG" "${ARGS[@]}"
    ;;
esac
