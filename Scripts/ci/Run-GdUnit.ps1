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
$buildStdoutPath = Join-Path $reportRoot 'dotnet-debug.stdout.log'
$buildStderrPath = Join-Path $reportRoot 'dotnet-debug.stderr.log'
$importStdoutPath = Join-Path $reportRoot 'godot-import.stdout.log'
$importStderrPath = Join-Path $reportRoot 'godot-import.stderr.log'
$stdoutPath = Join-Path $reportRoot 'godot.stdout.log'
$stderrPath = Join-Path $reportRoot 'godot.stderr.log'
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
Write-Host "Using Godot executable: $godotPath"

$dotnetPath = (Get-Command dotnet -CommandType Application).Source
$buildArguments = @(
    'build'
    'CardCleaner.csproj'
    '--configuration'
    'Debug'
    '--no-restore'
)
$buildProcess = Start-Process -FilePath $dotnetPath -ArgumentList $buildArguments -WorkingDirectory $repositoryRoot -RedirectStandardOutput $buildStdoutPath -RedirectStandardError $buildStderrPath -Wait -PassThru
if ($buildProcess.ExitCode -ne 0) {
    $buildStderr = if (Test-Path -LiteralPath $buildStderrPath) { (Get-Content -LiteralPath $buildStderrPath -Tail 40) -join [Environment]::NewLine } else { '<missing>' }
    throw "Godot test solution build failed with exit code $($buildProcess.ExitCode): $buildStderr"
}

$importArguments = @(
    '--headless'
    '--editor'
    '--recovery-mode'
    '--import'
    '--path'
    '.'
    '--quit'
)
$importProcess = Start-Process -FilePath $godotPath -ArgumentList $importArguments -WorkingDirectory $repositoryRoot -RedirectStandardOutput $importStdoutPath -RedirectStandardError $importStderrPath -Wait -PassThru
if ($importProcess.ExitCode -ne 0) {
    $importStderr = if (Test-Path -LiteralPath $importStderrPath) { (Get-Content -LiteralPath $importStderrPath -Tail 40) -join [Environment]::NewLine } else { '<missing>' }
    throw "Godot project import failed with exit code $($importProcess.ExitCode): $importStderr"
}

$godotProcess = Start-Process -FilePath $godotPath -ArgumentList $godotArguments -WorkingDirectory $repositoryRoot -RedirectStandardOutput $stdoutPath -RedirectStandardError $stderrPath -Wait -PassThru
$godotExitCode = $godotProcess.ExitCode

$latestResult = Get-ChildItem -LiteralPath $reportRoot -Filter results.xml -File -Recurse |
    Where-Object { $_.LastWriteTimeUtc -ge $startedAt } |
    Sort-Object -Property LastWriteTimeUtc |
    Select-Object -Last 1

if ($null -eq $latestResult) {
    $stdout = if (Test-Path -LiteralPath $stdoutPath) { (Get-Content -LiteralPath $stdoutPath -Tail 40) -join [Environment]::NewLine } else { '<missing>' }
    $stderr = if (Test-Path -LiteralPath $stderrPath) { (Get-Content -LiteralPath $stderrPath -Tail 40) -join [Environment]::NewLine } else { '<missing>' }
    throw "GdUnit did not produce a results.xml report under $reportRoot (exit=$godotExitCode). stdout=$stdout stderr=$stderr"
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
