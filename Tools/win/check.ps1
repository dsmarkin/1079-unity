# Batch-mode check: compile, generate prefabs/scene/world, run EditMode tests. Logs in the project root.
# Run: powershell -ExecutionPolicy Bypass -File Tools\win\check.ps1              (the Unity editor with this project must be closed)
#      powershell -ExecutionPolicy Bypass -File Tools\win\check.ps1 -NoElbrus    -- without the Elbrus location (docs/ELBRUS.md)
#
# Like the build, this always sets the switch explicitly, in a session of its own.
param([switch]$NoElbrus)
. (Join-Path $PSScriptRoot "editor.ps1")

if ($NoElbrus) { $Switch = "Height1079.EditorTools.Builds.WithoutElbrus"; $What = "без Эльбруса" }
else           { $Switch = "Height1079.EditorTools.Builds.WithElbrus";    $What = "с Эльбрусом" }

"Editor: $Editor ($What)" | Tee-Object unity-check.log
& $Editor -batchmode -nographics -quit -projectPath "$Root" -executeMethod $Switch -logFile "$Root\unity-switch.log" | Out-Null
"switch $LASTEXITCODE" | Tee-Object -Append unity-check.log
& $Editor -batchmode -nographics -projectPath "$Root" -runTests -testPlatform EditMode -testResults "$Root\unity-tests.xml" -logFile "$Root\unity-editor.log" | Out-Null
"exit $LASTEXITCODE" | Tee-Object -Append unity-check.log
Select-String -Path unity-editor.log -Pattern 'error CS|Exception|Failed to' | Select-Object -First 80 | ForEach-Object { $_.Line } | Add-Content unity-check.log
if (Test-Path unity-tests.xml) { (Select-String -Path unity-tests.xml -Pattern 'total="\d+" passed="\d+" failed="\d+"' | Select-Object -First 1).Matches.Value | Tee-Object -Append unity-check.log }
"== done $(Get-Date)" | Add-Content unity-check.log
