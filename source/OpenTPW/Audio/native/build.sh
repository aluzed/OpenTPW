#!/usr/bin/env bash
# Builds the minimp3 wrapper into a native lib per platform. Linux build committed under
# linux-x64/; rebuild with this script. Windows (.dll) / macOS (.dylib) builds are TODO (CI).
set -e
cd "$(dirname "$0")"
gcc -O2 -fPIC -shared tpwmp3.c -o linux-x64/libtpwmp3.so -lm
echo "built linux-x64/libtpwmp3.so"
