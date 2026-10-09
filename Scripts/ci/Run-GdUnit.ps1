[CmdletBinding()]
param(
    [Parameter()]
    [string]$GodotBinary = $env:GODOT_BIN,

    [Parameter()]
    [switch]$Fast,

    # Seconds without new GdUnit output before Godot counts as stalled. Healthy suites print a line every few
    # seconds and the slowest single cases take about 3 s; the bounds keep a typo from disabling the watchdog.
    [Parameter()]
    [ValidateRange(10, 3600)]
    [int]$StallSeconds = 180,

    # How many times one run may restart Godot on the unfinished suites after a stall. More than a handful
    # means the run is not converging, so the bound stops a broken run from looping for hours.
    [Parameter()]
    [ValidateRange(0, 10)]
    [int]$MaxResumes = 3
)

$ErrorActionPreference = 'Stop'

# Runs on Windows PowerShell 5.1 (local) and PowerShell 7 (CI). PowerShell 5.1 has no backtick-e escape for
# the ESC character, so ESC is written as [char]27 below, and it has no Split-Path -LeafBase.
# Output growth is checked this often; small against the stall timeout, cheap against a 5-minute run.
$PollSeconds = 5
# After taskkill, wait this long for the killed tree to exit and release its log files.
$KillWaitMilliseconds = 15000
# Attempts, one second apart, to read a log the killed tree may still hold open.
$ReadRetries = 10
$AnsiEscape = [regex]::new([string][char]27 + '\[[0-9;]*m')
$SuiteStartPattern = [regex]::new('^\s*Run Test Suite: (?<suite>res://\S+)')
$StatisticsPattern = [regex]::new('^\s*Statistics: \d+ test cases \| (?<errors>\d+) errors \| (?<failures>\d+) failures')
# Only GdUnit's own result shapes: a test's PASSED/FAILED line and a suite's Statistics line. Tests print their
# own lines too (for example "Atlas Mapping Statistics:"), which must not be reported as results.
$ResultLinePattern = [regex]::new('^\s*res://\S+ > .+ (PASSED|FAILED)\b|^\s*Statistics: \d+ test cases')
# Suite detection, shared with Tests/TestSuiteNamingTest.cs. Test-RunGdUnit.ps1 fails when either copy changes alone.
$SuiteAttributePattern = '^\s*\[(?:GdUnit4\.)?TestSuite(?:Attribute)?(?:\(\s*\))?\]'
# Comments and literals, in the same alternation order as the C# pattern. Known gaps: strings nested in an interpolation
# hole, and #if false blocks.
$CodeNoisePattern = '//[^\n]*|/\*[\s\S]*?\*/|("{3,})[\s\S]*?\1|@"(?:[^"]|"")*"|"(?:[^"\\\n]|\\.)*"|''(?:[^''\\\n]|\\.)+'''

function Resolve-GodotConsole {
    param([string]$Binary)

    $path = (Get-Item -LiteralPath $Binary).FullName
    $name = [IO.Path]::GetFileNameWithoutExtension($path)
    $console = Join-Path (Split-Path -Parent $path) "${name}_console.exe"
    if ((Test-Path -LiteralPath $console -PathType Leaf) -and $path -ne $console) {
        return (Get-Item -LiteralPath $console).FullName
    }
    return $path
}

function ConvertTo-ResPath {
    param([string]$RepositoryRoot, [string]$Path)

    return 'res://' + $Path.Substring($RepositoryRoot.Length).TrimStart('\').Replace('\', '/')
}

# Blanks every comment and literal to spaces, keeping newlines, so only code can match a suite attribute.
function Remove-CodeNoise {
    param([string]$Source)

    $blank = [System.Text.RegularExpressions.MatchEvaluator] { param($match) return ($match.Value -replace '[^\r\n]', ' ') }
    return [regex]::Replace($Source, $CodeNoisePattern, $blank)
}

# Suite files gdUnit is expected to run: a suite attribute at the start of a code line, in any spelling that
# TestSuiteNamingTest accepts. Comments and literals are blanked first, so a suite named only inside them is not expected.
function Get-ExpectedSuites {
    param([string]$RepositoryRoot, [switch]$UnitOnly)

    $suiteLine = [regex]::new($SuiteAttributePattern, [System.Text.RegularExpressions.RegexOptions]::Multiline)
    $files = Get-ChildItem -LiteralPath (Join-Path $RepositoryRoot 'Tests') -Filter '*.cs' -File -Recurse |
        Where-Object { $suiteLine.IsMatch((Remove-CodeNoise -Source ([IO.File]::ReadAllText($_.FullName)))) }
    if ($UnitOnly) {
        # Read from the raw text: blanking would erase the "Unit" literal this filter names.
        $files = $files | Where-Object { Select-String -LiteralPath $_.FullName -Pattern '\[TestCategory\("Unit"\)\]' -Quiet }
    }
    return @($files | ForEach-Object { ConvertTo-ResPath -RepositoryRoot $RepositoryRoot -Path $_.FullName } | Sort-Object)
}

# Pure log reading: which suites started, which finished (printed Statistics), their error and failure totals.
function Read-SuiteProgress {
    param([string]$LogText)

    $progress = [pscustomobject]@{
        Started = New-Object System.Collections.Generic.List[string]
        Finished = New-Object System.Collections.Generic.List[string]
        Errors = 0
        Failures = 0
        LastResultLine = '<none>'
    }
    $current = $null
    foreach ($line in ($AnsiEscape.Replace($LogText, '') -split "`r?`n")) {
        $start = $SuiteStartPattern.Match($line)
        if ($start.Success) {
            $current = $start.Groups['suite'].Value
            $progress.Started.Add($current)
            continue
        }
        if ($ResultLinePattern.IsMatch($line)) {
            $progress.LastResultLine = $line.Trim()
        }
        $statistics = $StatisticsPattern.Match($line)
        if ($statistics.Success -and $null -ne $current) {
            $progress.Finished.Add($current)
            $progress.Errors += [int]$statistics.Groups['errors'].Value
            $progress.Failures += [int]$statistics.Groups['failures'].Value
            $current = $null
        }
    }
    return $progress
}

function Get-ParentResPath {
    param([string]$ResPath)

    return $ResPath.Substring(0, $ResPath.LastIndexOf('/'))
}

function Test-AllPending {
    param([string]$Directory, [string[]]$AllSuites, $PendingSet)

    foreach ($suite in $AllSuites) {
        if ($suite.StartsWith("$Directory/", [StringComparison]::OrdinalIgnoreCase) -and -not $PendingSet.Contains($suite)) {
            return $false
        }
    }
    return $true
}

# Shortest -a list for a resume, so the command line stays far below the Windows limit as the suite grows:
# the highest directory whose suites are all unfinished, otherwise the single suite file.
function Get-ResumeTargets {
    param([string[]]$Pending, [string[]]$AllSuites)

    $pendingSet = New-Object 'System.Collections.Generic.HashSet[string]' ([StringComparer]::OrdinalIgnoreCase)
    foreach ($suite in $Pending) {
        [void]$pendingSet.Add($suite)
    }
    $targets = New-Object System.Collections.Generic.List[string]
    foreach ($suite in $Pending) {
        $target = $suite
        $directory = Get-ParentResPath -ResPath $suite
        while ($directory -ne 'res://Tests' -and (Test-AllPending -Directory $directory -AllSuites $AllSuites -PendingSet $pendingSet)) {
            $target = $directory
            $directory = Get-ParentResPath -ResPath $directory
        }
        if (-not $targets.Contains($target)) {
            $targets.Add($target)
        }
    }
    return , $targets.ToArray()
}

# The suite GdUnit was running when it stalled, or $null when the stall came between suites or before the first.
function Get-UnfinishedSuite {
    param($Progress)

    if ($Progress.Started.Count -eq 0) {
        return $null
    }
    $last = $Progress.Started[$Progress.Started.Count - 1]
    if ($Progress.Finished.Contains($last)) {
        return $null
    }
    return $last
}

function Get-StallPoint {
    param($Progress)

    $unfinished = Get-UnfinishedSuite -Progress $Progress
    if ($null -ne $unfinished) {
        return "in $unfinished"
    }
    if ($Progress.Finished.Count -gt 0) {
        return "after $($Progress.Finished[$Progress.Finished.Count - 1])"
    }
    return 'before the first suite'
}

# Kills Godot's whole process tree and confirms the root process is gone, so a resume never overlaps a survivor.
function Stop-ProcessTree {
    param([System.Diagnostics.Process]$Process)

    $ErrorActionPreference = 'Continue'
    & taskkill.exe /PID $Process.Id /T /F *> $null
    $taskkillExit = $LASTEXITCODE
    if (-not $Process.WaitForExit($KillWaitMilliseconds)) {
        throw "Could not stop Godot (PID $($Process.Id)); taskkill exit code $taskkillExit"
    }
}

# A just-killed Godot can still hold its log open for a moment, so read with shared access and retry briefly.
function Read-SharedText {
    param([string]$Path)

    for ($try = 1; ; $try++) {
        try {
            $stream = [IO.File]::Open($Path, [IO.FileMode]::Open, [IO.FileAccess]::Read, [IO.FileShare]'ReadWrite, Delete')
            try {
                return (New-Object IO.StreamReader($stream)).ReadToEnd()
            }
            finally {
                $stream.Dispose()
            }
        }
        catch {
            if ($try -ge $ReadRetries) {
                throw
            }
            Start-Sleep -Seconds 1
        }
    }
}

# Starts Godot and waits for it, killing the whole process tree once its stdout stops growing for $StallSeconds.
function Invoke-GodotWatched {
    param([string]$GodotPath, [string[]]$Arguments, [string]$WorkingDirectory, [string]$StdoutPath, [string]$StderrPath)

    $process = Start-Process -WindowStyle Hidden -FilePath $GodotPath -ArgumentList $Arguments -WorkingDirectory $WorkingDirectory -RedirectStandardOutput $StdoutPath -RedirectStandardError $StderrPath -PassThru
    $null = $process.Handle # keeps ExitCode readable after exit
    $lastLength = -1
    $quietSince = Get-Date
    while (-not $process.HasExited) {
        Start-Sleep -Seconds $PollSeconds
        $length = (Get-Item -LiteralPath $StdoutPath).Length
        if ($length -ne $lastLength) {
            $lastLength = $length
            $quietSince = Get-Date
        }
        elseif (((Get-Date) - $quietSince).TotalSeconds -ge $StallSeconds) {
            Stop-ProcessTree -Process $process
            return [pscustomobject]@{ Stalled = $true; ExitCode = $null }
        }
    }
    $process.WaitForExit()
    return [pscustomobject]@{ Stalled = $false; ExitCode = $process.ExitCode }
}

function Read-ReportTotals {
    param([string]$ReportRoot, [datetime]$Since)

    $reports = @(Get-ChildItem -LiteralPath $ReportRoot -Filter results.xml -File -Recurse |
        Where-Object { $_.LastWriteTimeUtc -ge $Since })
    $totals = [pscustomobject]@{ Files = $reports.Count; Errors = 0; Failures = 0 }
    foreach ($report in $reports) {
        try {
            [xml]$xml = Get-Content -LiteralPath $report.FullName -Raw
        }
        catch {
            throw "GdUnit report is not valid XML: $($report.FullName)"
        }
        if ($null -eq $xml.testsuites) {
            throw "GdUnit report has no testsuites root: $($report.FullName)"
        }
        $totals.Failures += @($xml.SelectNodes('//failure')).Count
        $totals.Errors += @($xml.SelectNodes('//error')).Count
    }
    return $totals
}

function Invoke-Build {
    param([string]$RepositoryRoot, [string]$ReportRoot)

    $stdout = Join-Path $ReportRoot 'dotnet-debug.stdout.log'
    $stderr = Join-Path $ReportRoot 'dotnet-debug.stderr.log'
    $dotnet = (Get-Command dotnet -CommandType Application).Source
    $arguments = @('build', 'CardCleaner.csproj', '--configuration', 'Debug', '--no-restore')
    $build = Start-Process -FilePath $dotnet -ArgumentList $arguments -WorkingDirectory $RepositoryRoot -RedirectStandardOutput $stdout -RedirectStandardError $stderr -Wait -PassThru
    if ($build.ExitCode -ne 0) {
        $tail = if (Test-Path -LiteralPath $stderr) { (Get-Content -LiteralPath $stderr -Tail 40) -join [Environment]::NewLine } else { '<missing>' }
        throw "Godot test solution build failed with exit code $($build.ExitCode): $tail"
    }
}

function Invoke-Import {
    param([string]$GodotPath, [string]$RepositoryRoot, [string]$ReportRoot)

    $stdout = Join-Path $ReportRoot 'godot-import.stdout.log'
    $stderr = Join-Path $ReportRoot 'godot-import.stderr.log'
    $arguments = @('--headless', '--editor', '--recovery-mode', '--import', '--path', '.', '--quit')
    $import = Invoke-GodotWatched -GodotPath $GodotPath -Arguments $arguments -WorkingDirectory $RepositoryRoot -StdoutPath $stdout -StderrPath $stderr
    if ($import.Stalled) {
        throw "Godot project import stalled (no output for $StallSeconds s); see $stdout"
    }
    if ($import.ExitCode -ne 0) {
        $tail = if (Test-Path -LiteralPath $stderr) { (Get-Content -LiteralPath $stderr -Tail 40) -join [Environment]::NewLine } else { '<missing>' }
        throw "Godot project import failed with exit code $($import.ExitCode): $tail"
    }
}

if ([string]::IsNullOrWhiteSpace($GodotBinary)) {
    throw 'Godot executable is required. Pass -GodotBinary or set GODOT_BIN.'
}
if (-not (Test-Path -LiteralPath $GodotBinary -PathType Leaf)) {
    throw "Godot executable was not found: $GodotBinary"
}

$godotPath = Resolve-GodotConsole -Binary $GodotBinary
$repositoryRoot = (Resolve-Path -LiteralPath (Join-Path $PSScriptRoot '..\..')).Path
$reportRoot = Join-Path $repositoryRoot 'reports\ci'
New-Item -ItemType Directory -Path $reportRoot -Force | Out-Null
Write-Host "Using Godot executable: $godotPath"

$allSuites = Get-ExpectedSuites -RepositoryRoot $repositoryRoot
$expected = if ($Fast) { Get-ExpectedSuites -RepositoryRoot $repositoryRoot -UnitOnly } else { $allSuites }
if ($expected.Count -eq 0) {
    throw 'GdUnit found no [TestSuite] files to run.'
}

Invoke-Build -RepositoryRoot $repositoryRoot -ReportRoot $reportRoot
Invoke-Import -GodotPath $godotPath -RepositoryRoot $repositoryRoot -ReportRoot $reportRoot

$baseArguments = @('--headless', '--path', '.', '-s', 'res://addons/gdUnit4/bin/GdUnitCmdTool.gd', '-c', '--ignoreHeadlessMode', '-rd', 'reports/ci')
$finished = New-Object 'System.Collections.Generic.HashSet[string]' ([StringComparer]::OrdinalIgnoreCase)
$stallPoints = New-Object System.Collections.Generic.List[string]
$stalledSuites = New-Object 'System.Collections.Generic.HashSet[string]' ([StringComparer]::OrdinalIgnoreCase)
$lastFinished = 'none'
$errors = 0
$failures = 0
$runStartedAt = [DateTime]::UtcNow
$finalExitCode = $null
# A full run starts from the Tests folder, -Fast names its suite files, and a resume names its compact targets.
$targets = if ($Fast) { $expected } else { @('res://Tests') }

for ($attempt = 0; ; $attempt++) {
    $suffix = if ($attempt -eq 0) { '' } else { ".resume$attempt" }
    $stdoutPath = Join-Path $reportRoot "godot$suffix.stdout.log"
    $stderrPath = Join-Path $reportRoot "godot$suffix.stderr.log"
    $suiteArguments = @($targets | ForEach-Object { '-a', $_ })

    $run = Invoke-GodotWatched -GodotPath $godotPath -Arguments ($baseArguments + $suiteArguments) -WorkingDirectory $repositoryRoot -StdoutPath $stdoutPath -StderrPath $stderrPath
    $progress = Read-SuiteProgress -LogText (Read-SharedText -Path $stdoutPath)
    foreach ($suite in $progress.Finished) {
        [void]$finished.Add($suite)
    }

    if (-not $run.Stalled) {
        $finalExitCode = $run.ExitCode
        break
    }

    # A killed Godot normally writes no results.xml (GdUnit writes it when the session shuts down), so the suites it
    # finished are judged from their Statistics lines. A report it did write is still read below; counting a
    # failure from both sources only matters when it is already non-zero.
    $errors += $progress.Errors
    $failures += $progress.Failures
    $point = Get-StallPoint -Progress $progress
    Write-Warning "GdUnit stalled $point (no output for $StallSeconds s). Last result: $($progress.LastResultLine)"
    $stallPoints.Add($point)
    $unfinished = Get-UnfinishedSuite -Progress $progress
    if ($null -ne $unfinished -and -not $stalledSuites.Add($unfinished)) {
        throw "GdUnit stalled twice in $unfinished; see $stdoutPath"
    }
    # A resume that finishes no suite is not converging, whether it stalls in a suite, between suites or at startup.
    if ($attempt -gt 0 -and $progress.Finished.Count -eq 0) {
        throw "GdUnit stalled $point again after resuming, without finishing any suite (last finished: $lastFinished); see $stdoutPath"
    }
    if ($progress.Finished.Count -gt 0) {
        $lastFinished = $progress.Finished[$progress.Finished.Count - 1]
    }
    if ($attempt -ge $MaxResumes) {
        throw "GdUnit stalled $($stallPoints.Count) times (limit $MaxResumes resumes); last stall $point"
    }
    $pending = @($expected | Where-Object { -not $finished.Contains($_) })
    if ($pending.Count -eq 0) {
        break
    }
    $targets = Get-ResumeTargets -Pending $pending -AllSuites $allSuites
    Write-Host "Resuming with $($pending.Count) unfinished suite(s): $($targets -join ' ')"
}

# Every report written since the run began counts, and at least one is required: a run whose last attempt was
# killed after its final suite printed Statistics still has to leave a report before it can pass.
$totals = Read-ReportTotals -ReportRoot $reportRoot -Since $runStartedAt
if ($totals.Files -eq 0) {
    $stdoutTail = (Get-Content -LiteralPath $stdoutPath -Tail 40) -join [Environment]::NewLine
    $exitText = if ($null -eq $finalExitCode) { 'killed after a stall' } else { "exit=$finalExitCode" }
    throw "GdUnit did not produce a results.xml report under $reportRoot ($exitText). stdout=$stdoutTail"
}
$errors += $totals.Errors
$failures += $totals.Failures

if ($failures -gt 0 -or $errors -gt 0) {
    throw "GdUnit run is not green: failures=$failures, errors=$errors, reports under $reportRoot"
}

$missing = @($expected | Where-Object { -not $finished.Contains($_) })
if ($missing.Count -gt 0) {
    throw "GdUnit never ran $($missing.Count) suite file(s); a class named differently from its file is skipped: $($missing -join ', ')"
}

if ($finalExitCode -eq 101) {
    Write-Warning 'GdUnit reported orphan-node warnings (exit code 101); the reports have no test failures or errors.'
}
elseif ($null -ne $finalExitCode -and $finalExitCode -ne 0) {
    throw "Godot exited with code $finalExitCode after a green report under $reportRoot"
}

if ($stallPoints.Count -gt 0) {
    Write-Warning "GdUnit passed after $($stallPoints.Count) stall(s): $($stallPoints -join '; ')"
}
Write-Host "GdUnit passed: $($finished.Count) of $($expected.Count) suite files ran."
