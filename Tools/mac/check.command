#!/bin/bash
# Batch-mode check: compile, run ProjectSetup (prefabs/scene/resources), run EditMode tests. Logs land in the project root.
source "$(dirname "$0")/editor.sh" || exit 1
echo "Editor: $EDITOR" | tee unity-check.log
"$EDITOR" -batchmode -nographics -projectPath "$PWD" -runTests -testPlatform EditMode -testResults "$PWD/unity-tests.xml" -logFile "$PWD/unity-editor.log"
echo "exit $?" | tee -a unity-check.log
grep -E "error CS|Exception|Tests run|Test .*(passed|failed)|Failed to" unity-editor.log | head -80 >> unity-check.log
grep -o 'total="[0-9]*"\|passed="[0-9]*"\|failed="[0-9]*"' unity-tests.xml 2>/dev/null | head -3 >> unity-check.log
echo "== done $(date)" >> unity-check.log
