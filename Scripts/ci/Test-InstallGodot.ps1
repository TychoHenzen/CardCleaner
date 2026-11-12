# Fixture for Scripts/ci/Install-Godot.ps1.
#
# The script runs against a loopback HttpListener (in a background job) that answers from a plan: a 504, a valid
# archive, an archive with the wrong content, or a valid archive without the executable. The retry, the exhaustion, the
# unreadable-archive and the missing-executable paths are therefore proven without a network. Windows PowerShell 5.1
# runs it locally; CI runs it under pwsh in the runner-fixture job.
#
# Usage: powershell -NoProfile -ExecutionPolicy Bypass -File Scripts/ci/Test-InstallGodot.ps1 [-Only <case>] [-KeepTemp]
# Exit code 0 when every selected case passes, 1 otherwise.

[CmdletBinding()]
param(
    # Runs one case instead of all of them. The name must be a key of $Cases below.
    [Parameter()]
    [string]$Only,

    # Keeps the temporary fixture folder for inspection.
    [Parameter()]
    [switch]$KeepTemp
)

$ErrorActionPreference = 'Stop'

$InstallScript = Join-Path $PSScriptRoot 'Install-Godot.ps1'
$TempBase = if ([string]::IsNullOrWhiteSpace($env:RUNNER_TEMP)) { $env:TEMP } else { $env:RUNNER_TEMP }
$FixtureParent = Join-Path $TempBase 'InstallGodotFixture'
$FixtureRoot = Join-Path $FixtureParent ([guid]::NewGuid().ToString('N'))
$BinaryName = 'Godot_fixture.exe'

# Answers one request per plan entry, in order; the last entry repeats. One log line per request.
$ServerScript = {
    param($Prefix, $Plan, $LogPath, $ReadyPath, $ZipPath, $StopPath)
    $listener = New-Object System.Net.HttpListener
    $listener.Prefixes.Add($Prefix)
    $listener.Start()
    Set-Content -LiteralPath $ReadyPath -Value 'ready'
    $count = 0
    while (-not (Test-Path -LiteralPath $StopPath)) {
        # GetContext() blocks and cannot be stopped from outside, so poll for the stop file between waits.
        $pending = $listener.GetContextAsync()
        while (-not $pending.Wait(200) -and -not (Test-Path -LiteralPath $StopPath)) { }
        if (-not $pending.IsCompleted) { break }
        $context = $pending.Result
        $kind = $Plan[[Math]::Min($count, $Plan.Count - 1)]
        $count++
        Add-Content -LiteralPath $LogPath -Value $kind
        if ($kind -eq '504') {
            $context.Response.StatusCode = 504
            $bytes = [System.Text.Encoding]::ASCII.GetBytes('Gateway Time-out')
        }
        elseif ($kind -eq 'bad') {
            $bytes = [System.Text.Encoding]::ASCII.GetBytes('this is not a zip archive')
        }
        else {
            $bytes = [System.IO.File]::ReadAllBytes($ZipPath)
        }
        $context.Response.ContentLength64 = $bytes.Length
        $context.Response.OutputStream.Write($bytes, 0, $bytes.Length)
        $context.Response.OutputStream.Close()
    }
    $listener.Close()
}

function New-FixtureZip {
    param([string]$Directory, [string]$EntryName)
    $named = Join-Path $Directory $EntryName
    Set-Content -LiteralPath $named -Value 'fixture'
    $zip = Join-Path $Directory 'served.zip'
    Compress-Archive -LiteralPath $named -DestinationPath $zip
    return $zip
}

# Runs Install-Godot.ps1 once against a server that follows $Plan. Returns what the script did and how often it was asked.
function Invoke-Install {
    param([string[]]$Plan, [int]$MaxAttempts, [string]$ZipEntry = $BinaryName)
    $caseRoot = Join-Path $FixtureRoot ([guid]::NewGuid().ToString('N'))
    New-Item -ItemType Directory -Path $caseRoot | Out-Null
    $tcp = New-Object System.Net.Sockets.TcpListener([System.Net.IPAddress]::Loopback, 0)
    $tcp.Start()
    $port = $tcp.LocalEndpoint.Port
    $tcp.Stop()
    $prefix = "http://127.0.0.1:$port/"
    $logPath = Join-Path $caseRoot 'requests.log'
    $readyPath = Join-Path $caseRoot 'ready.txt'
    $stopPath = Join-Path $caseRoot 'stop.txt'
    $zipPath = New-FixtureZip -Directory $caseRoot -EntryName $ZipEntry
    $job = Start-Job -ScriptBlock $ServerScript -ArgumentList $prefix, $Plan, $logPath, $readyPath, $zipPath, $stopPath
    try {
        $deadline = (Get-Date).AddSeconds(20)
        while (-not (Test-Path -LiteralPath $readyPath)) {
            if ((Get-Date) -gt $deadline) { throw 'the fixture server did not start' }
            Start-Sleep -Milliseconds 100
        }
        $output = @()
        $failure = $null
        try {
            $output = @(& $InstallScript -Uri ($prefix + 'godot.zip') -ArchivePath (Join-Path $caseRoot 'godot.zip') `
                    -ExtractPath (Join-Path $caseRoot 'extract') -BinaryName $BinaryName -MaxAttempts $MaxAttempts `
                    -DelaySeconds 0 -TimeoutSeconds 5 -WarningVariable warned -WarningAction SilentlyContinue)
        }
        catch {
            $failure = $_.Exception.Message
        }
        $requests = 0
        if (Test-Path -LiteralPath $logPath) { $requests = @(Get-Content -LiteralPath $logPath).Count }
        return [pscustomobject]@{
            Output   = $output
            Warnings = @($warned | ForEach-Object { $_.Message })
            Failure  = $failure
            Requests = $requests
        }
    }
    finally {
        Set-Content -LiteralPath $stopPath -Value 'stop'
        Wait-Job -Job $job -Timeout 10 | Out-Null
        Remove-Job -Job $job -Force
    }
}

function Assert-That {
    param([bool]$Condition, [string]$Message)
    if (-not $Condition) { throw "assertion failed: $Message" }
}

# The script must print exactly one object, the executable path; a failure names every object it printed.
function Assert-OnlyExecutablePath {
    param($Result)
    $printed = @($Result.Output | ForEach-Object { "[$($_.GetType().Name)] $_" }) -join ' | '
    Assert-That ($Result.Output.Count -eq 1 -and ([string]$Result.Output[0]).EndsWith($BinaryName)) `
        "one output, the executable path, expected; got $($Result.Output.Count): $printed"
}

$Cases = [ordered]@{
    'first-try'              = {
        $r = Invoke-Install -Plan @('zip') -MaxAttempts 3
        Assert-That ($null -eq $r.Failure) "no failure expected, got: $($r.Failure)"
        Assert-That ($r.Requests -eq 1) "1 request expected, got $($r.Requests)"
        Assert-That ($r.Warnings.Count -eq 0) "0 warnings expected, got $($r.Warnings.Count)"
        Assert-OnlyExecutablePath $r
    }
    'retry-504'              = {
        $r = Invoke-Install -Plan @('504', 'zip') -MaxAttempts 3
        Assert-That ($null -eq $r.Failure) "no failure expected, got: $($r.Failure)"
        Assert-That ($r.Requests -eq 2) "2 requests expected, got $($r.Requests)"
        Assert-That ($r.Warnings.Count -eq 1) "1 warning expected, got $($r.Warnings.Count)"
        Assert-That ($r.Warnings[0] -like 'Godot download attempt 1 of 3 failed:*504*') "warning text: $($r.Warnings[0])"
        Assert-OnlyExecutablePath $r
    }
    'always-504'             = {
        $r = Invoke-Install -Plan @('504') -MaxAttempts 3
        Assert-That ($r.Requests -eq 3) "3 requests expected, got $($r.Requests)"
        Assert-That ($r.Warnings.Count -eq 3) "3 warnings expected, got $($r.Warnings.Count)"
        Assert-That ($r.Warnings[2] -like 'Godot download attempt 3 of 3 failed:*') "warning text: $($r.Warnings[2])"
        Assert-That ($r.Failure -like 'Godot download failed after 3 attempts:*504*') "failure text: $($r.Failure)"
        Assert-That ($r.Output.Count -eq 0) 'no output expected'
    }
    'corrupt-archive'        = {
        $r = Invoke-Install -Plan @('bad', 'zip') -MaxAttempts 3
        Assert-That ($null -eq $r.Failure) "no failure expected, got: $($r.Failure)"
        Assert-That ($r.Requests -eq 2) "2 requests expected, got $($r.Requests)"
        Assert-That ($r.Warnings.Count -eq 1) "1 warning expected, got $($r.Warnings.Count)"
        Assert-OnlyExecutablePath $r
    }
    'missing-binary'         = {
        $r = Invoke-Install -Plan @('zip') -MaxAttempts 3 -ZipEntry 'readme.txt'
        Assert-That ($r.Requests -eq 1) "1 request expected, got $($r.Requests)"
        Assert-That ($r.Warnings.Count -eq 0) "0 warnings expected, got $($r.Warnings.Count)"
        Assert-That ($r.Failure -like 'Godot binary was not found under*') "failure text: $($r.Failure)"
    }
    'single-attempt-control' = {
        $r = Invoke-Install -Plan @('504', 'zip') -MaxAttempts 1
        Assert-That ($r.Requests -eq 1) "1 request expected, got $($r.Requests)"
        Assert-That ($r.Failure -like 'Godot download failed after 1 attempts:*504*') "failure text: $($r.Failure)"
    }
}

if ($Only -and -not $Cases.Contains($Only)) {
    Write-Host "Unknown case '$Only'. Cases: $($Cases.Keys -join ', ')"
    exit 1
}

New-Item -ItemType Directory -Path $FixtureRoot -Force | Out-Null
$failed = 0
try {
    foreach ($name in $Cases.Keys) {
        if ($Only -and $name -ne $Only) { continue }
        try {
            & $Cases[$name]
            Write-Host "PASS $name"
        }
        catch {
            $failed++
            Write-Host "FAIL $name : $($_.Exception.Message)"
        }
    }
}
finally {
    $leaf = Split-Path -Leaf $FixtureRoot
    $isOwnFolder = [System.IO.Path]::IsPathRooted($FixtureRoot) -and
        (Split-Path -Parent $FixtureRoot) -eq $FixtureParent -and $leaf -match '^[0-9a-f]{32}$'
    if (-not $KeepTemp -and $isOwnFolder -and (Test-Path -LiteralPath $FixtureRoot)) {
        Remove-Item -LiteralPath $FixtureRoot -Recurse -Force
    }
}

exit $(if ($failed -eq 0) { 0 } else { 1 })
