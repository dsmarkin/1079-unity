#!/bin/bash
# Windows build from the Mac editor (needs the windows-mono module) → Builds/windows/1079.exe.
source "$(dirname "$0")/editor.sh" || exit 1
echo "== windows build $(date)" > build-win.log
"$EDITOR" -batchmode -nographics -quit -projectPath "$PWD" -executeMethod Height1079.EditorTools.Builds.Windows -logFile "$PWD/build-win-editor.log"
echo "exit $? $(date)" >> build-win.log
grep -E "error CS|Build StandaloneWindows64|Build.*errors|Exception" build-win-editor.log | head -20 >> build-win.log
ls -la Builds/windows 2>/dev/null >> build-win.log
