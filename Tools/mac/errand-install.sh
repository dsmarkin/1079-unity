#!/bin/bash
# Installs the errand runner as a launchd agent for the current user, so Tools/mac/errand.sh starts
# by itself whenever the queue folder changes. Run once:
#
#     bash Tools/mac/errand-install.sh          # install and start watching
#     bash Tools/mac/errand-install.sh remove   # stop watching and delete the agent
#
# Nothing here needs sudo, nothing runs at login except the watcher itself, and the watcher only ever
# starts Tools/mac/errand.sh, which runs one of four fixed project commands. See errand.sh.
set -eu

ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)"
LABEL="com.dsmarkin.1079-errand"
PLIST="$HOME/Library/LaunchAgents/$LABEL.plist"
Q="$ROOT/Tools/mac/errand"

if [ "${1:-install}" = "remove" ]; then
    launchctl bootout "gui/$(id -u)/$LABEL" 2>/dev/null || true
    rm -f "$PLIST"
    echo "errand runner removed"
    exit 0
fi

mkdir -p "$Q" "$HOME/Library/LaunchAgents"

cat > "$PLIST" <<PLIST_END
<?xml version="1.0" encoding="UTF-8"?>
<!DOCTYPE plist PUBLIC "-//Apple//DTD PLIST 1.0//EN" "http://www.apple.com/DTDs/PropertyList-1.0.dtd">
<plist version="1.0">
<dict>
    <key>Label</key><string>$LABEL</string>
    <key>ProgramArguments</key>
    <array>
        <string>/bin/bash</string>
        <string>$ROOT/Tools/mac/errand.sh</string>
    </array>
    <key>WatchPaths</key>
    <array><string>$Q</string></array>
    <key>RunAtLoad</key><false/>
    <key>StandardOutPath</key><string>$Q/launchd.log</string>
    <key>StandardErrorPath</key><string>$Q/launchd.log</string>
</dict>
</plist>
PLIST_END

launchctl bootout "gui/$(id -u)/$LABEL" 2>/dev/null || true
launchctl bootstrap "gui/$(id -u)" "$PLIST"
echo "errand runner installed and watching $Q"
echo "check it with:  launchctl print gui/$(id -u)/$LABEL | head -5"
