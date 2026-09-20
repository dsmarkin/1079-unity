#!/bin/bash
# Physics sandbox: build the body-only player (no world, no network) and run it windowed.
# The editor must be closed — Unity allows one process per project.
#   Tools/mac/sandbox.command             build and run
#   Tools/mac/sandbox.command --run       run what is already built
#   Tools/mac/sandbox.command --day       open on the physics range in daylight, not the night yard
#   Tools/mac/sandbox.command --selftest  run the scripted physics check, print the verdict, quit
source "$(dirname "$0")/editor.sh" || exit 1
ARGS=""
BUILD=1
for a in "$@"; do
  case "$a" in
    --run) BUILD=0 ;;
    --day) ARGS="$ARGS -day" ;;
    --selftest) ARGS="$ARGS -selftest" ;;
    --shot) ARGS="$ARGS -shot -shotdir $PWD/Builds/sandbox/shots" ;;
    *) ARGS="$ARGS $a" ;;
  esac
done
if [ $BUILD -eq 1 ]; then
echo "Editor: $EDITOR" | tee sandbox-build.log
"$EDITOR" -batchmode -nographics -quit -projectPath "$PWD" \
  -executeMethod Height1079.EditorTools.SandboxBuilds.Mac -logFile "$PWD/sandbox-editor.log"
code=$?
echo "exit $code" | tee -a sandbox-build.log
grep -E "error CS|Sandbox build|Exception" sandbox-editor.log | head -40 >> sandbox-build.log
[ $code -eq 0 ] || { echo "build failed — see sandbox-build.log"; exit 1; }
fi
BIN="$(ls "$PWD"/Builds/sandbox/mac/1079-sandbox.app/Contents/MacOS/* 2>/dev/null | head -1)"
[ -x "$BIN" ] || { echo "no player at Builds/sandbox/mac"; exit 1; }
if [ -n "${ARGS// /}" ] && { [[ "$ARGS" == *"-selftest"* ]] || [[ "$ARGS" == *"-shot"* ]]; }; then
  # the scripted check runs to the end and quits itself, so wait for it and show the verdict.
  # Each mode keeps its own log: --selftest and --shot used to write the same file, and running one
  # after the other threw away the first one's verdict.
  LOG="$PWD/sandbox-player.log"
  [[ "$ARGS" == *"-selftest"* ]] && LOG="$PWD/sandbox-selftest.log"
  [[ "$ARGS" == *"-shot"* ]] && LOG="$PWD/sandbox-shot.log"
  "$BIN" -logFile "$LOG" -screen-fullscreen 0 -screen-width 1280 -screen-height 800 $ARGS
  code=$?
  echo "exit $code"
  grep -E "^selftest|^shots" "$LOG"
  # the script's own status must be the check's, not the grep's: a failed self-test used to end in exit 0
  exit $code
else
  "$BIN" -logFile "$PWD/sandbox-player.log" -screen-fullscreen 0 -screen-width 1440 -screen-height 900 $ARGS &
  echo "running — log: sandbox-player.log"
fi
