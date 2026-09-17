#!/bin/bash
# Mac build → Builds/mac/1079.app (incremental after the first run). Summary in build.log, full log in build-editor.log.
source "$(dirname "$0")/editor.sh" || exit 1
echo "== mac build $(date)" > build.log
# first session: compile scripts and generate the world; a player built in the same session as the generation
# shipped an empty terrain TextAsset (see CLAUDE.md), so the build gets a session of its own
"$EDITOR" -batchmode -nographics -quit -projectPath "$PWD" -executeMethod Height1079.EditorTools.ProjectSetup.Prepare -logFile "$PWD/build-prepare.log"
echo "prepare $? $(date)" >> build.log
"$EDITOR" -batchmode -nographics -quit -projectPath "$PWD" -executeMethod Height1079.EditorTools.Builds.Mac -logFile "$PWD/build-editor.log"
echo "exit $? $(date)" >> build.log
grep -E "error CS|Build StandaloneOSX|Build.*errors|Exception" build-editor.log | head -20 >> build.log
ls -la Builds/mac 2>/dev/null >> build.log
