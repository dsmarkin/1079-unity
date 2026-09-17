# Batch-mode check: compile, generate prefabs/scene/world, run EditMode tests. Logs in the project root.
# Run: powershell -ExecutionPolicy Bypass -File Tools\win\check.ps1   (the Unity editor with this project must be closed)
. (Join-Path $PSScriptRoot "editor.ps1")
"Editor: $Editor" | Tee-Object unity-check.log
& $Editor -batchmode -nographics -projectPath "$Root" -runTests -testPlatform EditMode -testResults "$Root\unity-tests.xml" -logFile "$Root\unity-editor.log" | Out-Null
"exit $LASTEXITCODE" | Tee-Object -Append unity-check.log
Select-String -Path unity-editor.log -Pattern 'error CS|Exception|Failed to' | Select-Object -First 80 | ForEach-Object { $_.Line } | Add-Content unity-check.log
if (Test-Path unity-tests.xml) { (Select-String -Path unity-tests.xml -Pattern 'total="\d+" passed="\d+" failed="\d+"' | Select-Object -First 1).Matches.Value | Tee-Object -Append unity-check.log }
"== done $(Get-Date)" | Add-Content unity-check.log
