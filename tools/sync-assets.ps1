<#
.SYNOPSIS
  Copies the licensed Synty files listed in tools/synty-assets.json into Assets/Synty, inside the private Assets submodule.
.DESCRIPTION
  Commit the copied files (and their .import files) inside the Assets submodule, never in this repository.
  The source root comes from the environment variable named by sourceRootEnvVar in the manifest, or from
  defaultSourceRoot (relative to the repository root) when the variable is unset. A missing source root or a
  missing listed file stops the script with a non-zero exit code. Nothing is skipped silently.
.PARAMETER ManifestPath
  Manifest to read. Defaults to tools/synty-assets.json next to this script.
#>
[CmdletBinding()]
param(
    [string]$ManifestPath
)

$ErrorActionPreference = 'Stop'

$scriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
$repoRoot = Split-Path -Parent $scriptDir
if (-not $ManifestPath) { $ManifestPath = Join-Path $scriptDir 'synty-assets.json' }
$destinationRoot = Join-Path $repoRoot 'Assets/Synty'

if (-not (Test-Path -LiteralPath (Join-Path $repoRoot 'Assets/.git'))) {
    throw "The Assets submodule is not initialised. Run: git submodule update --init"
}

if (-not (Test-Path -LiteralPath $ManifestPath)) {
    throw "Manifest not found: $ManifestPath"
}
$manifest = Get-Content -LiteralPath $ManifestPath -Raw | ConvertFrom-Json

$envValue = [Environment]::GetEnvironmentVariable($manifest.sourceRootEnvVar)
if ([string]::IsNullOrWhiteSpace($envValue)) {
    $sourceRoot = Join-Path $repoRoot $manifest.defaultSourceRoot
} else {
    $sourceRoot = $envValue
}

if (-not (Test-Path -LiteralPath $sourceRoot -PathType Container)) {
    throw "Asset source root not found: $sourceRoot. Set $($manifest.sourceRootEnvVar) to the folder that holds the Synty packs."
}

$missing = @()
foreach ($entry in $manifest.files) {
    $source = Join-Path $sourceRoot $entry.source
    if (-not (Test-Path -LiteralPath $source -PathType Leaf)) {
        $missing += $source
    }
}
if ($missing.Count -gt 0) {
    throw "Manifest files missing from source root:`n  $($missing -join "`n  ")"
}

foreach ($entry in $manifest.files) {
    $source = Join-Path $sourceRoot $entry.source
    $target = Join-Path $destinationRoot $entry.target
    New-Item -ItemType Directory -Force -Path (Split-Path -Parent $target) | Out-Null
    Copy-Item -LiteralPath $source -Destination $target -Force
    Write-Host "synced $($entry.target)"
}
