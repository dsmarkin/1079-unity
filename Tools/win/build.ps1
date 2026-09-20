# Windows build -> Builds\windows\1079.exe (incremental after the first run). Summary in build-win.log, full log in build-editor.log.
# Run: powershell -ExecutionPolicy Bypass -File Tools\win\build.ps1              (the Unity editor with this project must be closed)
#      powershell -ExecutionPolicy Bypass -File Tools\win\build.ps1 -NoElbrus    -- without the Elbrus location (docs/ELBRUS.md)
#
# The switch is ALWAYS set, either way, and in a session of its own: the define decides which assemblies exist and
# Unity can only act on that after a domain reload. Saying nothing would mean inheriting whatever the previous build
# left behind, and the two players look identical until they fail to talk to each other over the network.
param([switch]$NoElbrus)
. (Join-Path $PSScriptRoot "editor.ps1")

if ($NoElbrus) { $Switch = "Height1079.EditorTools.Builds.WithoutElbrus"; $What = "без Эльбруса" }
else           { $Switch = "Height1079.EditorTools.Builds.WithElbrus";    $What = "с Эльбрусом" }

"== windows build $(Get-Date) -- $What" | Set-Content build-win.log
# zeroth session: set the define and quit, so the next session starts with the right set of assemblies
& $Editor -batchmode -nographics -quit -projectPath "$Root" -executeMethod $Switch -logFile "$Root\build-switch.log" | Out-Null
"switch $LASTEXITCODE $(Get-Date) -- $What" | Add-Content build-win.log
# first session generates the world, the second builds the player: a player built in the same session as the
# generation shipped an empty terrain TextAsset (see CLAUDE.md)
& $Editor -batchmode -nographics -quit -projectPath "$Root" -executeMethod Height1079.EditorTools.ProjectSetup.Prepare -logFile "$Root\build-prepare.log" | Out-Null
"prepare $LASTEXITCODE $(Get-Date)" | Add-Content build-win.log
& $Editor -batchmode -nographics -quit -projectPath "$Root" -executeMethod Height1079.EditorTools.Builds.Windows -logFile "$Root\build-editor.log" | Out-Null
"exit $LASTEXITCODE $(Get-Date)" | Add-Content build-win.log
Select-String -Path build-editor.log -Pattern 'error CS|Build StandaloneWindows|Exception' | Select-Object -First 20 | ForEach-Object { $_.Line } | Add-Content build-win.log
Get-Content build-win.log
