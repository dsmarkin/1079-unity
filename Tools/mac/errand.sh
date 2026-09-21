#!/bin/bash
# Errand runner — how an agent gets a build out of this Mac without a terminal.
#
# Claude can put files into the project folder but cannot type into Terminal: computer-use grants
# terminals in "look and click" mode only. So it writes one keyword into Tools/mac/errand/next, a
# launchd agent notices the folder changed and starts this script, and this script runs the matching
# project command and writes the result back into the same folder, where Claude can read it.
#
# The keyword is matched against a fixed list below and is NEVER executed as a shell command: the
# queue can ask for the handful of things listed below, and for nothing else. Install the launchd
# agent once with `bash Tools/mac/errand-install.sh`; remove it with `launchctl bootout`.
#
# `push` lives here rather than on the agent's side on purpose: the agent's shell is a Linux VM that
# mounts this folder, and the macOS keychain is not reachable from it. Running the push here means git
# runs on macOS, picks the credentials out of the keychain by itself, and no token is ever handled by
# the agent or written into a config.
#
# The scripts it runs are the ones in Tools/mac, NOT the copies of the same names in the project root. Those
# copies are not in git, they drift, and the drift is invisible: on 21.09 the root build.command was still the
# two-session version from before Elbrus became optional, so it skipped the session that sets the assembly
# define. The player it produced had scene data that did not match its own assemblies — "level0 is corrupted",
# and then a player that died before it could open a log. Everything else said the build had succeeded.
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

# run.command backgrounds the player with `&`, which is fine from a terminal and useless from here:
# launchd tears the job's whole process group down when the script returns, and the game went with it.
# `open` hands the bundle to Launch Services instead, so the player outlives the errand.
stop_game() {
    pkill -f 'Builds/mac/1079.app/Contents/MacOS/' && printf 'game stopped\n' || printf 'game was not running\n'
    sleep 1
}

start_game() {
    local app="$ROOT/Builds/mac/1079.app"
    local EXTRA="${1:-}"
    [ -d "$app" ] || { printf 'no build at %s\n' "$app"; return 66; }
    # `open` failing looked exactly like the game starting: this line printed "launched" whatever happened, and
    # the only symptom was an empty player.log, which reads like a crash before logging ever opened.
    open -n "$app" --args -logFile "$ROOT/player.log" -screen-fullscreen 0 -screen-width 1280 -screen-height 800 $EXTRA 2>&1
    local rc=$?
    [ $rc -eq 0 ] || { printf 'open failed rc=%s\n' "$rc"; return $rc; }
    printf 'player launched%s\n' "${EXTRA:+ ($EXTRA)}"
}

# A function, not a { } group: `exit` inside a group ends the whole script, so an unknown keyword used
# to leave `state` reading "running" for ever and never write `done` — the queue wedged on a typo.
run_job() {
    printf '== %s  %s\n' "$TOKEN" "$(date '+%F %T %Z')"
    case "$JOB" in
        check)     bash "$ROOT/Tools/mac/check.command" ;;
        build)     bash "$ROOT/Tools/mac/build.command" ;;
        build-no-elbrus) bash "$ROOT/Tools/mac/build.command" --no-elbrus ;;
        run)       start_game ;;
        shots)     start_game -shots ;;
        build+run) stop_game; bash "$ROOT/Tools/mac/build.command" && start_game ;;
        quit)      stop_game ;;
        push)      GIT_TERMINAL_PROMPT=0 git -C "$ROOT" push origin main ;;
        ping)      printf 'errand runner alive: %s\n' "$(sw_vers -productVersion 2>/dev/null || uname -s)" ;;
        *)         printf 'unknown errand: %s\n' "$JOB"; return 64 ;;
    esac
}

run_job > "$LOG" 2>&1
RC=$?

printf '%s\n' "$TOKEN" > "$DONE"
printf 'done rc=%s  %s  %s\n' "$RC" "$TOKEN" "$(date '+%F %T %Z')" > "$STATE"
