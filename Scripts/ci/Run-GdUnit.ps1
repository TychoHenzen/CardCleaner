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

    # How many times one run may restart Godot on the unfinished suites after a stall or a crash. More than a
    # handful means the run is not converging, so the bound stops a broken run from looping for hours.
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
# Exit codes gdUnit returns itself (GdUnitTestSessionRunner.gd). Any other exit code from an attempt that wrote no
# results report is a Godot crash, for example the Windows NTSTATUS 0xC000001D (illegal instruction).
$GdUnitExitCodes = @(0, 100, 101, 103, 104)
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
$CodeNoisePattern =
    '//[^\n]*|/\*[\s\S]*?\*/|("{3,})[\s\S]*?\1|@\$?"(?:[^"]|"")*"|"(?:[^"\\\n]|\\.)*"|''(?:[^''\\\n]|\\.)+'''

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

# Blanks every comment and literal to spaces, keeping newlines, so only code can match a suite attribute. With
# -KeepStrings only comments are blanked, so string literals such as the "Unit" in [TestCategory("Unit")] survive.
function Remove-CodeNoise {
    param([string]$Source, [switch]$KeepStrings)

    # ASSUMPTION: PowerShell 7, which runs this in CI, converts these scriptblocks to MatchEvaluator as 5.1 does.
    $blankAll = [System.Text.RegularExpressions.MatchEvaluator] {
        param($match)
        return ($match.Value -replace '[^\r\n]', ' ')
    }
    $blankComments = [System.Text.RegularExpressions.MatchEvaluator] {
        param($match)
        if ($match.Value.StartsWith('//') -or $match.Value.StartsWith('/*')) {
            return ($match.Value -replace '[^\r\n]', ' ')
        }
        return $match.Value
    }
    $evaluator = if ($KeepStrings) { $blankComments } else { $blankAll }
    return [regex]::Replace($Source, $CodeNoisePattern, $evaluator)
}

# Suite files gdUnit is expected to run: a suite attribute at the start of a code line, in any spelling that
# TestSuiteNamingTest accepts. Comments and literals are blanked first, so a suite named only inside them
# is not expected.
function Get-ExpectedSuites {
    param([string]$RepositoryRoot, [switch]$UnitOnly)

    $suiteLine = [regex]::new($SuiteAttributePattern, [System.Text.RegularExpressions.RegexOptions]::Multiline)
    $files = Get-ChildItem -LiteralPath (Join-Path $RepositoryRoot 'Tests') -Filter '*.cs' -File -Recurse |
        Where-Object { $suiteLine.IsMatch((Remove-CodeNoise -Source ([IO.File]::ReadAllText($_.FullName)))) }
    if ($UnitOnly) {
        # Only comments are blanked: the "Unit" literal must survive, and a commented-out category must not count.
        $unitCategory = [regex]'\[TestCategory\("Unit"\)\]'
        $files = $files | Where-Object {
            $unitCategory.IsMatch((Remove-CodeNoise -Source ([IO.File]::ReadAllText($_.FullName)) -KeepStrings))
        }
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

# The suite GdUnit was running when it was interrupted, or $null when that came between suites or before the first.
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

function Get-InterruptionPoint {
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

# Process.ExitCode is a signed Int32, so a Windows NTSTATUS such as 0xC000001D arrives negative. Int32.ToString('X8')
# prints its two's-complement bits, which is the form Windows shows.
function Format-ExitCode {
    param([int]$ExitCode)

    return '0x' + $ExitCode.ToString('X8')
}

# Reports written at or after $Since. A crashed attempt writes none, so this tells a crash from a clean exit.
function Get-ReportsSince {
    param([string]$ReportRoot, [datetime]$Since)

    return @(Get-ChildItem -LiteralPath $ReportRoot -Filter results.xml -File -Recurse |
        Where-Object { $_.LastWriteTimeUtc -ge $Since })
}

# Why an attempt ended before Godot exited on its own, or $null when it did. A stall is the watchdog's kill. A crash is
# an exit code gdUnit does not return, from an attempt that wrote no report. Only reports from that attempt count.
function Get-Interruption {
    param($Run, [string]$ReportRoot, [datetime]$AttemptStartedAt)

    if ($Run.Stalled) {
        return [pscustomobject]@{ Kind = 'stall'; Exit = $null }
    }
    if ($GdUnitExitCodes -contains $Run.ExitCode) {
        return $null
    }
    if (@(Get-ReportsSince -ReportRoot $ReportRoot -Since $AttemptStartedAt).Count -gt 0) {
        return $null
    }
    $exitHex = Format-ExitCode -ExitCode $Run.ExitCode
    return [pscustomobject]@{ Kind = 'crash'; Exit = $exitHex }
}

# Words one interruption for every message the runner prints about it. Kind alone picks the wording, so no call site
# branches on Kind or Text to build a sentence. $Point says where it happened: "in <suite>", "after <suite>" or
# "before the first suite". $Earlier is the interruption already recorded for $Suite, when there is one, and the repeat
# sentence names both causes. It builds the text and returns it with the cause fields; the caller decides whether to
# warn, throw or record.
function Format-Interruption {
    param($Cause, [string]$Point, [string]$LastResult, $Earlier = $null, [string]$Suite = '')

    if ($Cause.Kind -eq 'stall') {
        $label = 'stall'
        $verb = 'stalled'
        $detail = "no output for $StallSeconds s"
        $resumeVerb = 'stalled'
    }
    else {
        $label = "crash $($Cause.Exit)"
        $verb = 'crashed'
        $detail = "exit $($Cause.Exit), no results report"
        $resumeVerb = "crashed (exit $($Cause.Exit))"
    }
    $repeat = $null
    if ($null -ne $Earlier) {
        if ($Earlier.Kind -eq 'stall' -and $Cause.Kind -eq 'stall') {
            $repeat = "GdUnit stalled twice in $Suite"
        }
        else {
            $repeat = "GdUnit was interrupted twice in $Suite ($($Earlier.Label), then $label)"
        }
    }
    return [pscustomobject]@{
        Kind = $Cause.Kind
        Label = $label
        Point = $Point
        Entry = "$label $Point"
        Warning = "GdUnit $verb $Point ($detail). Last result: $LastResult"
        ResumeVerb = $resumeVerb
        Repeat = $repeat
    }
}

# The closing line of a run that passed after interruptions. A run with only stalls keeps its original wording; once a
# crash is involved, one line names every cause.
function Format-InterruptionSummary {
    param($Interruptions)

    # Not @($Interruptions): Windows PowerShell 5.1 throws "Argument types do not match" when it wraps a List[object].
    $count = $Interruptions.Count
    $crashes = @($Interruptions | Where-Object { $_.Kind -eq 'crash' })
    if ($crashes.Count -eq 0) {
        $points = @($Interruptions | ForEach-Object { $_.Point })
        return "GdUnit passed after $count stall(s): " + ($points -join '; ')
    }
    $entries = @($Interruptions | ForEach-Object { $_.Entry })
    return "GdUnit passed after $count interruption(s): " + ($entries -join '; ')
}

function Read-ReportTotals {
    param([string]$ReportRoot, [datetime]$Since)

    $reports = @(Get-ReportsSince -ReportRoot $ReportRoot -Since $Since)
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
    # PATH can hold more than one dotnet.exe; take the first, as the shell would.
    $dotnet = (Get-Command dotnet -CommandType Application | Select-Object -First 1).Source
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

# -rc 100000 stops gdUnit's report cleanup from trying (and failing) to delete old report_N folders (#198).
$baseArguments = @(
    '--headless', '--path', '.', '-s', 'res://addons/gdUnit4/bin/GdUnitCmdTool.gd', '-c', '--ignoreHeadlessMode',
    '-rd', 'reports/ci', '-rc', '100000'
)
$finished = New-Object 'System.Collections.Generic.HashSet[string]' ([StringComparer]::OrdinalIgnoreCase)
$interruptions = New-Object System.Collections.Generic.List[object]
# Suite -> the first interruption's formatted record, so a second interruption in the same suite fails the run.
$causeMapType = 'System.Collections.Generic.Dictionary[string,object]'
$interruptedSuites = New-Object $causeMapType ([StringComparer]::OrdinalIgnoreCase)
$lastFinished = 'none'
$lastInterruption = $null
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

    $attemptStartedAt = [DateTime]::UtcNow
    $run = Invoke-GodotWatched -GodotPath $godotPath -Arguments ($baseArguments + $suiteArguments) -WorkingDirectory $repositoryRoot -StdoutPath $stdoutPath -StderrPath $stderrPath
    $progress = Read-SuiteProgress -LogText (Read-SharedText -Path $stdoutPath)
    foreach ($suite in $progress.Finished) {
        [void]$finished.Add($suite)
    }

    $interruption = Get-Interruption -Run $run -ReportRoot $reportRoot -AttemptStartedAt $attemptStartedAt
    if ($null -eq $interruption) {
        $finalExitCode = $run.ExitCode
        break
    }

    # A killed or crashed Godot normally writes no results.xml (GdUnit writes it when the session shuts down), so the
    # suites it finished are judged from their Statistics lines. A report it did write is still read below; counting a
    # failure from both sources only matters when it is already non-zero.
    $errors += $progress.Errors
    $failures += $progress.Failures
    $point = Get-InterruptionPoint -Progress $progress
    $unfinished = Get-UnfinishedSuite -Progress $progress
    $earlier = $null
    if ($null -ne $unfinished -and $interruptedSuites.ContainsKey($unfinished)) {
        $earlier = $interruptedSuites[$unfinished]
    }
    $formatArgs = @{
        Cause = $interruption
        Point = $point
        LastResult = $progress.LastResultLine
        Earlier = $earlier
        Suite = $unfinished
    }
    $formatted = Format-Interruption @formatArgs
    Write-Warning $formatted.Warning
    $interruptions.Add($formatted)
    $lastInterruption = $formatted.Entry
    if ($null -ne $formatted.Repeat) {
        throw "$($formatted.Repeat); see $stdoutPath"
    }
    if ($null -ne $unfinished) {
        $interruptedSuites[$unfinished] = $formatted
    }
    # A resume that finishes no suite is not converging, whether it is interrupted in a suite, between suites or at
    # startup.
    if ($attempt -gt 0 -and $progress.Finished.Count -eq 0) {
        throw ("GdUnit $($formatted.ResumeVerb) $point again after resuming, without finishing any suite " +
            "(last finished: $lastFinished); see $stdoutPath")
    }
    if ($progress.Finished.Count -gt 0) {
        $lastFinished = $progress.Finished[$progress.Finished.Count - 1]
    }
    if ($attempt -ge $MaxResumes) {
        $count = $interruptions.Count
        throw "GdUnit was interrupted $count times (limit $MaxResumes resumes); last interruption: $lastInterruption"
    }
    $pending = @($expected | Where-Object { -not $finished.Contains($_) })
    if ($pending.Count -eq 0) {
        break
    }
    $targets = Get-ResumeTargets -Pending $pending -AllSuites $allSuites
    Write-Host "Resuming with $($pending.Count) unfinished suite(s): $($targets -join ' ')"
}

# Every report written since the run began counts, and at least one is required: a run whose last attempt was
# interrupted after its final suite printed Statistics still has to leave a report before it can pass.
$totals = Read-ReportTotals -ReportRoot $reportRoot -Since $runStartedAt
if ($totals.Files -eq 0) {
    $stdoutTail = (Get-Content -LiteralPath $stdoutPath -Tail 40) -join [Environment]::NewLine
    $exitText = if ($null -eq $finalExitCode) { "interrupted: $lastInterruption" } else { "exit=$finalExitCode" }
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

if ($interruptions.Count -gt 0) {
    Write-Warning (Format-InterruptionSummary -Interruptions $interruptions)
}
Write-Host "GdUnit passed: $($finished.Count) of $($expected.Count) suite files ran."
