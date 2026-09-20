#!/bin/bash
# Mac build → Builds/mac/1079.app (incremental after the first run). Summary in build.log, full log in build-editor.log.
#
#   Tools/mac/build.command              — with the Elbrus location (the default)
#   Tools/mac/build.command --no-elbrus  — without it (docs/ELBRUS.md)
#
# The switch is ALWAYS set, either way, and in a session of its own: the define decides which assemblies exist and
# Unity can only act on that after a domain reload. Saying nothing would mean inheriting whatever the previous build
# left behind, and the two players look identical until they fail to talk to each other over the network.
source "$(dirname "$0")/editor.sh" || exit 1

SWITCH=Height1079.EditorTools.Builds.WithElbrus
WHAT="с Эльбрусом"
for arg in "$@"; do
  case "$arg" in
    --no-elbrus) SWITCH=Height1079.EditorTools.Builds.WithoutElbrus; WHAT="без Эльбруса" ;;
    --elbrus)    SWITCH=Height1079.EditorTools.Builds.WithElbrus;    WHAT="с Эльбрусом" ;;
    *) echo "build.command: неизвестный ключ $arg (есть --no-elbrus)"; exit 2 ;;
  esac
done

echo "== mac build $(date) — $WHAT" > build.log
# zeroth session: set the define and quit, so the next session starts with the right set of assemblies
"$EDITOR" -batchmode -nographics -quit -projectPath "$PWD" -executeMethod "$SWITCH" -logFile "$PWD/build-switch.log"
echo "switch $? $(date) — $WHAT" >> build.log
# first session: compile scripts and generate the world; a player built in the same session as the generation
# shipped an empty terrain TextAsset (see CLAUDE.md), so the build gets a session of its own
"$EDITOR" -batchmode -nographics -quit -projectPath "$PWD" -executeMethod Height1079.EditorTools.ProjectSetup.Prepare -logFile "$PWD/build-prepare.log"
echo "prepare $? $(date)" >> build.log
"$EDITOR" -batchmode -nographics -quit -projectPath "$PWD" -executeMethod Height1079.EditorTools.Builds.Mac -logFile "$PWD/build-editor.log"
echo "exit $? $(date)" >> build.log
grep -E "error CS|Build StandaloneOSX|Build.*errors|Exception" build-editor.log | head -20 >> build.log
ls -la Builds/mac 2>/dev/null >> build.log
