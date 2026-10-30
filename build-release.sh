#!/usr/bin/env bash
# Сборка пакета winToMac: release/winToMac/{winToMac.exe, Instructions/, Licenses/} и zip-архив.
# Использование: DOTNET=/путь/к/dotnet ./build-release.sh
set -euo pipefail
cd "$(dirname "$0")"
DOTNET="${DOTNET:-dotnet}"
PYTHON="${PYTHON:-python3}"
VERSION=$(sed -n 's:.*<Version>\(.*\)</Version>.*:\1:p' winToMac.csproj)
OUT=release/winToMac
rm -rf release && mkdir -p "$OUT"
"$DOTNET" publish winToMac.csproj -c Release -r win-x64 --self-contained true \
  -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true \
  -p:EnableCompressionInSingleFile=false -p:DebugType=none -o release/publish
cp release/publish/winToMac.exe "$OUT/"
rm -rf release/publish
"$PYTHON" -I Docs/instructions.py "$OUT/Instructions" Assets/winToMac.png
mkdir -p "$OUT/Licenses" && cp Licenses/* "$OUT/Licenses/"
"$PYTHON" -I - "$OUT" "release/winToMac-$VERSION.zip" <<'PY'
import os, sys, zipfile
src, dst = sys.argv[1], sys.argv[2]
with zipfile.ZipFile(dst, "w", zipfile.ZIP_DEFLATED, compresslevel=9) as archive:
    for root, _, files in os.walk(src):
        for name in files:
            path = os.path.join(root, name)
            archive.write(path, os.path.join("winToMac", os.path.relpath(path, src)))
PY
cp "release/winToMac-$VERSION.zip" release/winToMac.zip  # постоянное имя для ссылки releases/latest/download/winToMac.zip
echo "Готово: $OUT и release/winToMac-$VERSION.zip"
