#!/bin/bash
# Launch the Mac build windowed (1280×800) with the player log written to the project root (player.log).
cd "$(dirname "$0")/../.." || exit 1
BIN="$(ls "$PWD"/Builds/mac/1079.app/Contents/MacOS/* 2>/dev/null | head -1)"
[ -x "$BIN" ] || { echo "No build yet — run build.command first"; exit 1; }
"$BIN" -logFile "$PWD/player.log" -screen-fullscreen 0 -screen-width 1280 -screen-height 800 &
