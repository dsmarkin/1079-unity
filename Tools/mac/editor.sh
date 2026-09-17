#!/bin/bash
# Shared by the .command scripts: cd to the project root and find the Unity editor the project is pinned to.
cd "$(dirname "$0")/../.." || exit 1
VERSION=$(sed -n 's/^m_EditorVersion: //p' ProjectSettings/ProjectVersion.txt)
EDITOR="/Applications/Unity/Hub/Editor/$VERSION/Unity.app/Contents/MacOS/Unity"
[ -x "$EDITOR" ] || EDITOR=$(find /Applications "$HOME/Applications" -maxdepth 5 -path "*$VERSION*" -name "Unity.app" 2>/dev/null | head -1)/Contents/MacOS/Unity
[ -x "$EDITOR" ] || { echo "Unity $VERSION not found (install: ~/.unity/bin/unity install $VERSION -m windows-mono -y --accept-eula)"; exit 1; }
