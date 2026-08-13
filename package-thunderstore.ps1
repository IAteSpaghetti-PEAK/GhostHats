# Builds the mod and produces a Thunderstore-ready zip in .\artifacts\
# Usage: powershell -ExecutionPolicy Bypass -File .\package-thunderstore.ps1 [-SkipDeploy]
#   -SkipDeploy: don't copy the built DLL into the game's BepInEx\plugins folder.
# This only packages. Uploading is a separate, manual step.
param([switch]$SkipDeploy)
$ErrorActionPreference = "Stop"
$projectDir = $PSScriptRoot

# Read version from the manifest so it stays the single source of truth
$manifest = Get-Content (Join-Path $projectDir "thunderstore\manifest.json") -Raw | ConvertFrom-Json
$version = $manifest.version_number
$name = $manifest.name

Write-Host "Building $name $version..."
$buildArgs = @((Join-Path $projectDir "GhostHats.csproj"), "-c", "Release")
if ($SkipDeploy) { $buildArgs += "-p:SkipDeploy=true"; Write-Host "(not deploying to the game's plugins folder)" }
dotnet build @buildArgs | Out-Null
if ($LASTEXITCODE -ne 0) { throw "Build failed" }

$dll = Join-Path $projectDir "bin\Release\netstandard2.1\GhostHats.dll"
if (-not (Test-Path $dll)) { throw "Built DLL not found at $dll" }

# Stage the package: manifest + icon + README + CHANGELOG at root, DLL in plugins/
$stage = Join-Path $env:TEMP "ghosthats_ts_stage"
if (Test-Path $stage) { Remove-Item $stage -Recurse -Force }
New-Item -ItemType Directory "$stage\plugins" -Force | Out-Null
Copy-Item (Join-Path $projectDir "thunderstore\manifest.json") $stage
Copy-Item (Join-Path $projectDir "thunderstore\icon.png") $stage
Copy-Item (Join-Path $projectDir "thunderstore\CHANGELOG.md") $stage
# The package page gets the user-facing README; the repo root one is dev/build notes.
Copy-Item (Join-Path $projectDir "thunderstore\README.md") $stage
Copy-Item $dll "$stage\plugins"

$artifacts = Join-Path $projectDir "artifacts"
New-Item -ItemType Directory $artifacts -Force | Out-Null
$zip = Join-Path $artifacts "$name-$version.zip"
if (Test-Path $zip) { Remove-Item $zip -Force }
Compress-Archive -Path "$stage\*" -DestinationPath $zip
Remove-Item $stage -Recurse -Force

Write-Host "Packaged: $zip"

# Refresh release-assets/: these are committed because the GitHub runner can't build the
# mod (it needs PEAK's DLLs), and the release workflow attaches whatever is in here.
# Keeping it in step with the zip means a tag can never ship a stale binary.
$release = Join-Path $projectDir "release-assets"
New-Item -ItemType Directory $release -Force | Out-Null
Get-ChildItem $release -Filter "$name-*-thunderstore.zip" | Remove-Item -Force
Copy-Item $zip (Join-Path $release "$name-$version-thunderstore.zip") -Force
Copy-Item $dll $release -Force
Write-Host "Refreshed release-assets\ for $name $version (commit these before tagging)."

Write-Host "To publish (manual): upload it at https://thunderstore.io/c/peak/create/"
