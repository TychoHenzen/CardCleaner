[CmdletBinding()]
param(
    [Parameter()]
    [string]$GodotBinary = $env:GODOT_BIN
)

$ErrorActionPreference = 'Stop'

if ([string]::IsNullOrWhiteSpace($GodotBinary)) {
    throw 'Godot executable is required. Pass -GodotBinary or set GODOT_BIN.'
}

if (-not (Test-Path -LiteralPath $GodotBinary -PathType Leaf)) {
    throw "Godot executable was not found: $GodotBinary"
}

$godotPath = (Get-Item -LiteralPath $GodotBinary).FullName
$consolePath = Join-Path (Split-Path -Parent $godotPath) "$(Split-Path -LeafBase $godotPath)_console.exe"
if ((Test-Path -LiteralPath $consolePath -PathType Leaf) -and $godotPath -ne $consolePath) {
    $godotPath = (Get-Item -LiteralPath $consolePath).FullName
}
$repositoryRoot = (Resolve-Path -LiteralPath (Join-Path $PSScriptRoot '..\..')).Path
$reportRoot = Join-Path $repositoryRoot 'reports\ci'
$startedAt = [DateTime]::UtcNow
$godotArguments = @(
    '--headless'
    '--path'
    '.'
    '-s'
    'res://addons/gdUnit4/bin/GdUnitCmdTool.gd'
    '-a'
    'res://Tests'
    '-c'
    '--ignoreHeadlessMode'
    '-rd'
    'reports/ci'
)

New-Item -ItemType Directory -Path $reportRoot -Force | Out-Null

$godotProcess = Start-Process -FilePath $godotPath -ArgumentList $godotArguments -WorkingDirectory $repositoryRoot -Wait -PassThru
$godotExitCode = $godotProcess.ExitCode

$latestResult = Get-ChildItem -LiteralPath $reportRoot -Filter results.xml -File -Recurse |
    Where-Object { $_.LastWriteTimeUtc -ge $startedAt } |
    Sort-Object -Property LastWriteTimeUtc |
    Select-Object -Last 1

if ($null -eq $latestResult) {
    throw "GdUnit did not produce a results.xml report under $reportRoot"
}

try {
    [xml]$report = Get-Content -LiteralPath $latestResult.FullName -Raw
}
catch {
    throw "GdUnit report is not valid XML: $($latestResult.FullName)"
}

if ($null -eq $report.testsuites) {
    throw "GdUnit report has no testsuites root: $($latestResult.FullName)"
}

$failureCount = 0
$errorCount = 0
if ($report.testsuites.failures) {
    $failureCount = [int]$report.testsuites.failures
}
if ($report.testsuites.errors) {
    $errorCount = [int]$report.testsuites.errors
}

foreach ($suite in @($report.testsuites.testsuite)) {
    if ($suite.failures) {
        $failureCount = [Math]::Max($failureCount, [int]$suite.failures)
    }
    if ($suite.errors) {
        $errorCount = [Math]::Max($errorCount, [int]$suite.errors)
    }
}

$failureNodes = @($report.SelectNodes('//failure'))
$errorNodes = @($report.SelectNodes('//error'))

if ($failureCount -gt 0 -or $errorCount -gt 0 -or $failureNodes.Count -gt 0 -or $errorNodes.Count -gt 0) {
    throw "GdUnit report is not green: failures=$failureCount, errors=$errorCount, report=$($latestResult.FullName)"
}

if ($godotExitCode -eq 101) {
    Write-Warning 'GdUnit reported orphan-node warnings (exit code 101); the JUnit report has no test failures or errors.'
}
elseif ($godotExitCode -ne 0) {
    throw "Godot exited with code $godotExitCode after a green report: $($latestResult.FullName)"
}

Write-Host "GdUnit passed: $($latestResult.FullName)"
