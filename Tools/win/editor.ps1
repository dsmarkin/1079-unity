# Shared by check.ps1 / build.ps1: go to the project root and find the Unity editor the project is pinned to.
$Root = Resolve-Path (Join-Path $PSScriptRoot "..\..")
Set-Location $Root
$Version = (Select-String -Path "ProjectSettings\ProjectVersion.txt" -Pattern '^m_EditorVersion: (.+)$').Matches[0].Groups[1].Value
$Editor = "C:\Program Files\Unity\Hub\Editor\$Version\Editor\Unity.exe"
if (-not (Test-Path $Editor)) {
    $found = Get-ChildItem -Path "C:\Program Files", "$env:LOCALAPPDATA" -Filter Unity.exe -Recurse -ErrorAction SilentlyContinue |
        Where-Object { $_.FullName -like "*$Version*" } | Select-Object -First 1
    if ($found) { $Editor = $found.FullName } else { throw "Unity $Version not found. Install it from Unity Hub (Installs → Install Editor → Archive → $Version)." }
}
