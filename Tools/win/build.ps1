# Windows build -> Builds\windows\1079.exe (incremental after the first run). Summary in build-win.log, full log in build-editor.log.
# Run: powershell -ExecutionPolicy Bypass -File Tools\win\build.ps1   (the Unity editor with this project must be closed)
. (Join-Path $PSScriptRoot "editor.ps1")
"== windows build $(Get-Date)" | Set-Content build-win.log
& $Editor -batchmode -nographics -quit -projectPath "$Root" -executeMethod Height1079.EditorTools.Builds.Windows -logFile "$Root\build-editor.log" | Out-Null
"exit $LASTEXITCODE $(Get-Date)" | Add-Content build-win.log
Select-String -Path build-editor.log -Pattern 'error CS|Build StandaloneWindows|Exception' | Select-Object -First 20 | ForEach-Object { $_.Line } | Add-Content build-win.log
Get-Content build-win.log
