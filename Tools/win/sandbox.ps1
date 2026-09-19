# Physics sandbox on Windows: build the body-only player and run it. The editor must be closed.
# Written to match Tools/mac/sandbox.command; not yet run on Windows.
$ErrorActionPreference = "Stop"
Set-Location (Join-Path $PSScriptRoot "..\..")
$version = (Select-String -Path ProjectSettings\ProjectVersion.txt -Pattern '^m_EditorVersion: (.+)$').Matches[0].Groups[1].Value
$editor = "C:\Program Files\Unity\Hub\Editor\$version\Editor\Unity.exe"
if (-not (Test-Path $editor)) { throw "Unity $version not found at $editor" }
& $editor -batchmode -nographics -quit -projectPath $PWD `
  -executeMethod Height1079.EditorTools.SandboxBuilds.Windows -logFile "$PWD\sandbox-editor.log"
if ($LASTEXITCODE -ne 0) { throw "build failed - see sandbox-editor.log" }
$exe = "$PWD\Builds\sandbox\windows\1079-sandbox.exe"
if (-not (Test-Path $exe)) { throw "no player at $exe" }
Start-Process $exe -ArgumentList "-logFile","$PWD\sandbox-player.log","-screen-fullscreen","0","-screen-width","1440","-screen-height","900"
