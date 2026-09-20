#!/bin/bash
# Errand runner — how an agent gets a build out of this Mac without a terminal.
#
# Claude can put files into the project folder but cannot type into Terminal: computer-use grants
# terminals in "look and click" mode only. So it writes one keyword into Tools/mac/errand/next, a
# launchd agent notices the folder changed and starts this script, and this script runs the matching
# project command and writes the result back into the same folder, where Claude can read it.
#
# The keyword is matched against a fixed list below and is NEVER executed as a shell command: the
# queue can ask for a check, a build, a run or a quit, and for nothing else. Install the launchd
# agent once with `bash Tools/mac/errand-install.sh`; remove it with `launchctl bootout`.
#
# Files in Tools/mac/errand/ (all git-ignored):
#   next      what to do, plus a stamp that makes each request unique: "build+run 20260920-0925"
#   state     "running <token>" while it works, then "done rc=<code> <token> <when>"
#   done      the token of the last finished request — a second launchd trigger for the same token
#             is ignored, so a rewrite of an unrelated file cannot start the same job twice
#   last.log  everything the command printed
set -u

ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)"
Q="$ROOT/Tools/mac/errand"
mkdir -p "$Q"
NEXT="$Q/next"; DONE="$Q/done"; STATE="$Q/state"; LOG="$Q/last.log"

[ -f "$NEXT" ] || exit 0
TOKEN="$(tr -d '\r\n' < "$NEXT")"
[ -n "$TOKEN" ] || exit 0
if [ -f "$DONE" ] && [ "$(tr -d '\r\n' < "$DONE")" = "$TOKEN" ]; then exit 0; fi

# launchd can fire again while a build is still going; mkdir is the atomic lock macOS has without flock
if ! mkdir "$Q/.lock" 2>/dev/null; then exit 0; fi
trap 'rmdir "$Q/.lock" 2>/dev/null' EXIT

JOB="${TOKEN%% *}"
printf 'running %s  %s\n' "$TOKEN" "$(date '+%F %T %Z')" > "$STATE"

{
    printf '== %s  %s\n' "$TOKEN" "$(date '+%F %T %Z')"
    case "$JOB" in
        check)     bash "$ROOT/check.command" ;;
        build)     bash "$ROOT/build.command" ;;
        run)       bash "$ROOT/run.command" ;;
        build+run) bash "$ROOT/build.command" && bash "$ROOT/run.command" ;;
        quit)      pkill -f 'Builds/mac/1079.app/Contents/MacOS/' && echo 'game stopped' || echo 'game was not running' ;;
        *)         printf 'unknown errand: %s\n' "$JOB"; exit 64 ;;
    esac
} > "$LOG" 2>&1
RC=$?

printf '%s\n' "$TOKEN" > "$DONE"
printf 'done rc=%s  %s  %s\n' "$RC" "$TOKEN" "$(date '+%F %T %Z')" > "$STATE"
