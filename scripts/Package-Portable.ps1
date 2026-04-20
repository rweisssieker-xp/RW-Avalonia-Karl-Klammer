#Requires -Version 5.1
$ErrorActionPreference = "Stop"
$root = Split-Path -Parent $PSScriptRoot
$outDir = Join-Path $root "artifacts\CarolusNexus-publish"
$zipPath = Join-Path $root "artifacts\CarolusNexus-portable.zip"

New-Item -ItemType Directory -Force -Path (Split-Path $outDir) | Out-Null

dotnet publish (Join-Path $root "CarolusNexus\CarolusNexus.csproj") `
    -c Release `
    -o $outDir `
    --nologo

if (-not (Test-Path (Join-Path $outDir "CarolusNexus.exe"))) {
    Write-Error "Publish failed: CarolusNexus.exe not found under $outDir"
}

if (Test-Path $zipPath) { Remove-Item $zipPath -Force }
Compress-Archive -Path (Join-Path $outDir "*") -DestinationPath $zipPath -Force
Write-Host "OK: $zipPath"
