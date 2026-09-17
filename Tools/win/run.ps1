# Launch the Windows build windowed (1280x800), player log in the project root (player.log).
. (Join-Path $PSScriptRoot "editor.ps1")
$exe = Join-Path $Root "Builds\windows\1079.exe"
if (-not (Test-Path $exe)) { throw "No build yet - run Tools\win\build.ps1 first" }
Start-Process $exe -ArgumentList "-logFile `"$Root\player.log`" -screen-fullscreen 0 -screen-width 1280 -screen-height 800"
