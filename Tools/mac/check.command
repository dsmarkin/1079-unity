#!/bin/bash
# Batch-mode check: compile, run ProjectSetup (prefabs/scene/resources), run EditMode tests. Logs land in the project root.
#
#   Tools/mac/check.command              — with the Elbrus location (the default)
#   Tools/mac/check.command --no-elbrus  — without it, which is how you find out the game still builds alone
#
# Like the build, this always sets the switch explicitly, in a session of its own (docs/ELBRUS.md).
source "$(dirname "$0")/editor.sh" || exit 1

SWITCH=Height1079.EditorTools.Builds.WithElbrus
WHAT="с Эльбрусом"
for arg in "$@"; do
  case "$arg" in
    --no-elbrus) SWITCH=Height1079.EditorTools.Builds.WithoutElbrus; WHAT="без Эльбруса" ;;
    --elbrus)    SWITCH=Height1079.EditorTools.Builds.WithElbrus;    WHAT="с Эльбрусом" ;;
    *) echo "check.command: неизвестный ключ $arg (есть --no-elbrus)"; exit 2 ;;
  esac
done

echo "Editor: $EDITOR ($WHAT)" | tee unity-check.log
"$EDITOR" -batchmode -nographics -quit -projectPath "$PWD" -executeMethod "$SWITCH" -logFile "$PWD/unity-switch.log"
echo "switch $?" | tee -a unity-check.log
"$EDITOR" -batchmode -nographics -projectPath "$PWD" -runTests -testPlatform EditMode -testResults "$PWD/unity-tests.xml" -logFile "$PWD/unity-editor.log"
echo "exit $?" | tee -a unity-check.log
grep -E "error CS|Exception|Tests run|Test .*(passed|failed)|Failed to" unity-editor.log | head -80 >> unity-check.log
grep -o 'total="[0-9]*"\|passed="[0-9]*"\|failed="[0-9]*"' unity-tests.xml 2>/dev/null | head -3 >> unity-check.log
echo "== done $(date)" >> unity-check.log
