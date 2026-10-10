# Fake-Godot integration fixture for Scripts/ci/Run-GdUnit.ps1.
#
# The runner runs against a fake Godot executable, compiled here with the in-box .NET Framework csc.exe, inside a
# temporary fake repository. The watchdog, process-tree kill, resume targets, repeated-stall failure, crash handling
# and report aggregation are therefore checked without a real Godot. Windows PowerShell 5.1 runs it locally; CI passes
# -RunnerShell pwsh so the runner itself runs under PowerShell 7.
#
# Usage: powershell -NoProfile -ExecutionPolicy Bypass -File Scripts/ci/Test-RunGdUnit.ps1 [-Only <check>] [-KeepTemp]
# Exit code 0 when every check passes, 1 otherwise.

[CmdletBinding()]
param(
    # Shell that runs the runner. Windows PowerShell 5.1 is the local default; CI passes pwsh.
    [Parameter()]
    [ValidateSet('powershell.exe', 'pwsh')]
    [string]$RunnerShell = 'powershell.exe',

    # Runs one check instead of all of them. The name must be one of the checks listed in $AllChecks below.
    [Parameter()]
    [string]$Only,

    # Keeps the temporary fake repositories for inspection.
    [Parameter()]
    [switch]$KeepTemp
)

$ErrorActionPreference = 'Stop'

$RepositoryRoot = (Resolve-Path -LiteralPath (Join-Path $PSScriptRoot '..\..')).Path
$RealRunner = Join-Path $PSScriptRoot 'Run-GdUnit.ps1'
# CI runners expose a short TEMP path, so RUNNER_TEMP is preferred when it is set. Every expected path is built from
# the same string that is passed to the runner, so the comparison does not depend on how the path is spelled.
$TempBase = if ([string]::IsNullOrWhiteSpace($env:RUNNER_TEMP)) { $env:TEMP } else { $env:RUNNER_TEMP }
$FixtureRoot = Join-Path $TempBase ('RunGdUnitFixture\' + [guid]::NewGuid().ToString('N'))

$SuiteAlpha = 'res://Tests/Alpha/AlphaTest.cs'
$SuiteAlphaTwo = 'res://Tests/Alpha/AlphaTwoTest.cs'
$SuiteBeta = 'res://Tests/Beta/BetaTest.cs'

# ASSUMPTION: the in-box csc.exe compiles C# 5 only, so the fake avoids interpolation, null-conditional operators,
# expression-bodied members and nameof.
$FakeGodotSource = @'
using System;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Text;
using System.Threading;

internal static class FakeGodot
{
    // One compiled program plays three roles: dotnet (build succeeds), a grandchild started by a stall (sleep),
    // and Godot itself, which replays the next plan file named by FAKE_GODOT_PLAN.
    private static int Main(string[] args)
    {
        string self = Assembly.GetEntryAssembly().Location;
        string name = Path.GetFileNameWithoutExtension(self);
        string state = Environment.GetEnvironmentVariable("FAKE_GODOT_STATE");
        if (string.Equals(name, "dotnet", StringComparison.OrdinalIgnoreCase))
        {
            Log(state, "dotnet", args);
            if (args.Length > 0 && args[0] == "build")
            {
                return 0;
            }
            Console.Out.WriteLine("FAKE: unexpected dotnet invocation");
            Console.Out.Flush();
            return 98;
        }
        if (args.Length == 1 && args[0] == "sleep")
        {
            Thread.Sleep(Timeout.Infinite);
            return 0;
        }
        return RunGodot(args, self, state, Environment.GetEnvironmentVariable("FAKE_GODOT_PLAN"));
    }

    private static int RunGodot(string[] args, string self, string state, string plan)
    {
        Log(state, Path.GetFileName(self), args);
        if (Array.IndexOf(args, "--import") >= 0)
        {
            return 0;
        }
        string counterPath = Path.Combine(state, "next.txt");
        int index = File.Exists(counterPath) ? int.Parse(File.ReadAllText(counterPath).Trim()) : 0;
        File.WriteAllText(counterPath, (index + 1).ToString());
        string planPath = Path.Combine(plan, "run" + index + ".txt");
        if (!File.Exists(planPath))
        {
            Console.Out.WriteLine("FAKE: unexpected invocation");
            Console.Out.Flush();
            return 99;
        }
        foreach (string raw in File.ReadAllLines(planPath))
        {
            string line = raw.TrimEnd();
            if (line.StartsWith("print ", StringComparison.Ordinal))
            {
                Console.Out.WriteLine(line.Substring(6));
                Console.Out.Flush();
            }
            else if (line.StartsWith("report ", StringComparison.Ordinal))
            {
                string[] parts = line.Split(' ');
                WriteReport(parts[1], int.Parse(parts[2]), int.Parse(parts[3]));
            }
            else if (line == "stall")
            {
                Stall(state, self);
                Thread.Sleep(Timeout.Infinite);
            }
            else if (line.StartsWith("exit ", StringComparison.Ordinal))
            {
                Console.Out.Flush();
                return int.Parse(line.Substring(5));
            }
            else if (line.Length > 0)
            {
                Console.Out.WriteLine("FAKE: unknown directive " + line);
                Console.Out.Flush();
                return 97;
            }
        }
        Console.Out.Flush();
        return 0;
    }

    private static void Stall(string state, string self)
    {
        var psi = new ProcessStartInfo(self, "sleep");
        psi.UseShellExecute = false;
        Process child = Process.Start(psi);
        string image = Path.GetFileNameWithoutExtension(self);
        File.AppendAllText(Path.Combine(state, "pids.txt"),
            Process.GetCurrentProcess().Id + " " + image + Environment.NewLine +
            child.Id + " " + image + Environment.NewLine);
        Console.Out.Flush();
    }

    private static void WriteReport(string dir, int failures, int errors)
    {
        string folder = Path.Combine(Directory.GetCurrentDirectory(), "reports", "ci", dir);
        Directory.CreateDirectory(folder);
        var xml = new StringBuilder();
        xml.AppendLine("<?xml version=\"1.0\" encoding=\"UTF-8\"?>");
        xml.AppendLine("<testsuites>");
        xml.AppendLine("  <testsuite name=\"fake\">");
        xml.AppendLine("    <testcase name=\"fake\" classname=\"fake\">");
        for (int i = 0; i < failures; i++)
        {
            xml.AppendLine("      <failure message=\"fake failure\"/>");
        }
        for (int i = 0; i < errors; i++)
        {
            xml.AppendLine("      <error message=\"fake error\"/>");
        }
        xml.AppendLine("    </testcase>");
        xml.AppendLine("  </testsuite>");
        xml.AppendLine("</testsuites>");
        File.WriteAllText(Path.Combine(folder, "results.xml"), xml.ToString());
    }

    private static void Log(string state, string name, string[] args)
    {
        File.AppendAllText(Path.Combine(state, "calls.log"), name + " " + string.Join(" ", args) + Environment.NewLine);
    }
}
'@

# The runner is invoked through this wrapper, so an exception it throws arrives as one raw RUNNER-ERROR line instead of
# PowerShell's wrapped error rendering, which breaks long paths across lines.
$WrapperSource = @'
param(
    [Parameter(Mandatory = $true)][string]$RunnerPath,
    [Parameter(Mandatory = $true)][string]$GodotBinary,
    [Parameter(Mandatory = $true)][int]$StallSeconds,
    [Parameter(Mandatory = $true)][int]$MaxResumes,
    [switch]$Fast
)

$ErrorActionPreference = 'Stop'
try {
    $splat = @{ GodotBinary = $GodotBinary; StallSeconds = $StallSeconds; MaxResumes = $MaxResumes }
    if ($Fast) {
        $splat['Fast'] = $true
    }
    & $RunnerPath @splat
    exit 0
}
catch {
    [Console]::Error.WriteLine('RUNNER-ERROR: ' + $_.Exception.Message)
    exit 1
}
'@

$AlphaSuite = @'
[TestSuite]
[TestCategory("Unit")]
public class AlphaTest
{
}
'@

# Positive control: quote and comment characters above the attribute must not hide the suite.
$AlphaTwoSuite = @'
// A comment with a quote: "
var marker = "/*";
var quote = '"';
[TestSuite]
public class AlphaTwoTest
{
}
'@

$BetaSuite = @'
[TestSuite]
public class BetaTest
{
}
'@

# A real non-Unit suite whose only Unit category is commented out: -Fast must not select it.
$CommentedUnitSuite = @'
// [TestCategory("Unit")]
[TestSuite]
public class GammaTest
{
}
'@

# Phantoms: a suite attribute that appears only inside a comment or a string is not a suite, so the runner must not
# expect these files. The block-comment phantom carries the Unit category too, so -Fast would select it if it counted.
$PhantomBlockComment = @'
/*
[TestSuite]
[TestCategory("Unit")]
public class BlockComment
{
}
*/
'@

$PhantomVerbatim = @'
var text = @"
[TestSuite]
public class Verbatim
{
}
";
'@

$PhantomInterpolatedVerbatim = @'
var text = @$"
[TestSuite]
public class Interpolated
{{
}}
";
'@

$PhantomRaw = @'
var text = """
[TestSuite]
public class Raw
{
}
""";
'@

$PhantomNotes = @'
/* Notes about the Beta suite.
[TestSuite]
public class Notes
{
}
*/
'@

$StaleReportXml = '<?xml version="1.0" encoding="UTF-8"?><testsuites><testsuite>' +
    '<testcase><failure/><failure/><failure/><failure/><failure/></testcase></testsuite></testsuites>'

function Write-FileText {
    param([string]$Path, [string]$Text)

    New-Item -ItemType Directory -Path (Split-Path -Parent $Path) -Force | Out-Null
    [IO.File]::WriteAllText($Path, $Text)
}

function ConvertTo-ProcessArgument {
    param([string]$Value)

    if ($Value.Length -gt 0 -and $Value -notmatch '[\s"]') {
        return $Value
    }
    return '"' + ($Value -replace '"', '\"') + '"'
}

# Runs a native tool with file redirects, so its stderr never becomes a PowerShell error record.
function Invoke-NativeQuiet {
    param([string]$FilePath, [string[]]$Arguments, [string]$Directory)

    $stdout = Join-Path $Directory ('native-' + [guid]::NewGuid().ToString('N') + '.stdout.txt')
    $stderr = Join-Path $Directory ('native-' + [guid]::NewGuid().ToString('N') + '.stderr.txt')
    $argumentLine = (@($Arguments) | ForEach-Object { ConvertTo-ProcessArgument -Value ([string]$_) }) -join ' '
    $startArguments = @{
        FilePath = $FilePath
        ArgumentList = $argumentLine
        RedirectStandardOutput = $stdout
        RedirectStandardError = $stderr
        WindowStyle = 'Hidden'
        Wait = $true
        PassThru = $true
    }
    $process = Start-Process @startArguments
    $stdoutText = ''
    $stderrText = ''
    if (Test-Path -LiteralPath $stdout) {
        $stdoutText = [IO.File]::ReadAllText($stdout)
    }
    if (Test-Path -LiteralPath $stderr) {
        $stderrText = [IO.File]::ReadAllText($stderr)
    }
    return [pscustomobject]@{
        ExitCode = $process.ExitCode
        Stdout = $stdoutText
        Stderr = $stderrText
    }
}

function New-FakeBuild {
    param([string]$Directory)

    New-Item -ItemType Directory -Path $Directory -Force | Out-Null
    $csc = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
    if (-not (Test-Path -LiteralPath $csc -PathType Leaf)) {
        throw ("csc.exe was not found at $csc; " +
            'the fixture compiles its fake Godot with the in-box .NET Framework compiler.')
    }
    $source = Join-Path $Directory 'FakeGodot.cs'
    $output = Join-Path $Directory 'FakeGodot.exe'
    [IO.File]::WriteAllText($source, $FakeGodotSource)
    $compile = Invoke-NativeQuiet -FilePath $csc -Directory $Directory -Arguments @(
        '/nologo', '/target:exe', ('/out:' + $output), $source)
    if ($compile.ExitCode -ne 0) {
        throw "csc.exe failed with exit code $($compile.ExitCode): $($compile.Stdout)$($compile.Stderr)"
    }
    return $output
}

function New-ScenarioContext {
    param([string]$Name, [string]$BuildExe)

    $root = Join-Path $FixtureRoot $Name
    $repo = Join-Path $root 'repo'
    $bin = Join-Path $root 'bin'
    $context = [pscustomobject]@{
        Name = $Name
        Root = $root
        Repo = $repo
        Bin = $bin
        Fake = Join-Path $bin 'FakeGodot.exe'
        Console = Join-Path $bin 'FakeGodot_console.exe'
        Runner = Join-Path $repo 'Scripts\ci\Run-GdUnit.ps1'
        Plan = Join-Path $root 'plan'
        State = Join-Path $root 'state'
        Wrapper = Join-Path $root 'run-runner.ps1'
    }
    foreach ($directory in @($context.Plan, $context.State, $context.Bin, (Join-Path $repo 'Tests'))) {
        New-Item -ItemType Directory -Path $directory -Force | Out-Null
    }
    # The dotnet copy is found first on PATH, so the runner's Get-Command dotnet resolves to the fake.
    Copy-Item -LiteralPath $BuildExe -Destination $context.Fake
    Copy-Item -LiteralPath $BuildExe -Destination $context.Console
    Copy-Item -LiteralPath $BuildExe -Destination (Join-Path $bin 'dotnet.exe')
    New-Item -ItemType Directory -Path (Split-Path -Parent $context.Runner) -Force | Out-Null
    Copy-Item -LiteralPath $RealRunner -Destination $context.Runner
    Write-FileText -Path (Join-Path $repo 'Tests\Alpha\AlphaTest.cs') -Text $AlphaSuite
    Write-FileText -Path (Join-Path $repo 'Tests\Alpha\AlphaTwoTest.cs') -Text $AlphaTwoSuite
    Write-FileText -Path (Join-Path $repo 'Tests\Beta\BetaTest.cs') -Text $BetaSuite
    Write-FileText -Path (Join-Path $repo 'Tests\Beta\Notes.cs') -Text $PhantomNotes
    Write-FileText -Path (Join-Path $repo 'Tests\Phantom\BlockComment.cs') -Text $PhantomBlockComment
    Write-FileText -Path (Join-Path $repo 'Tests\Phantom\Verbatim.cs') -Text $PhantomVerbatim
    Write-FileText -Path (Join-Path $repo 'Tests\Phantom\InterpolatedVerbatim.cs') -Text $PhantomInterpolatedVerbatim
    Write-FileText -Path (Join-Path $repo 'Tests\Phantom\Raw.cs') -Text $PhantomRaw
    Write-FileText -Path $context.Wrapper -Text $WrapperSource
    return $context
}

function Set-FakePlan {
    param($Context, [int]$Index, [string[]]$Lines)

    [IO.File]::WriteAllLines((Join-Path $Context.Plan ('run' + $Index + '.txt')), [string[]]$Lines)
}

function Get-SuiteStart {
    param([string]$Suite)

    return 'print Run Test Suite: ' + $Suite
}

function Get-SuiteStats {
    param([int]$Cases = 0, [int]$ErrorCount = 0, [int]$FailureCount = 0)

    return "print Statistics: $Cases test cases | $ErrorCount errors | $FailureCount failures"
}

function Get-FakeCalls {
    param($Context)

    $path = Join-Path $Context.State 'calls.log'
    if (-not (Test-Path -LiteralPath $path -PathType Leaf)) {
        return @()
    }
    return @([IO.File]::ReadAllLines($path))
}

function Get-FakeProcessRecords {
    param($Context)

    $path = Join-Path $Context.State 'pids.txt'
    if (-not (Test-Path -LiteralPath $path -PathType Leaf)) {
        return @()
    }
    $records = New-Object System.Collections.Generic.List[object]
    foreach ($line in [IO.File]::ReadAllLines($path)) {
        $parts = $line -split ' '
        $records.Add([pscustomobject]@{ Id = [int]$parts[0]; Name = $parts[1] })
    }
    return @($records.ToArray())
}

function Read-RunnerText {
    param([string]$Path)

    if (-not (Test-Path -LiteralPath $Path -PathType Leaf)) {
        return ''
    }
    # ASSUMPTION: Write-Warning output may land on stdout or stderr depending on the shell, so warning checks read both.
    return [regex]::Replace([IO.File]::ReadAllText($Path), [string][char]27 + '\[[0-9;]*m', '')
}

function Get-RunnerOutput {
    param($Result)

    return $Result.Stdout + [Environment]::NewLine + $Result.Stderr
}

# Write-Warning wraps long messages at the console width, so a line break can fall at a space or inside a long path.
# Warning checks compare the text with line breaks removed. A wrap keeps the space it breaks on,
# so no word merges.
# ASSUMPTION: pwsh 7 wraps warnings the same way; only line breaks are removed, so any wrap position still matches.
function Get-FlatRunnerOutput {
    param($Result)

    return [regex]::Replace((Get-RunnerOutput $Result), '\r?\n', '')
}

function Invoke-Runner {
    param($Context, [switch]$Fast, [int]$MaxResumes = 3)

    $runnerArguments = @(
        '-NoProfile', '-ExecutionPolicy', 'Bypass', '-File', $Context.Wrapper,
        '-RunnerPath', $Context.Runner, '-GodotBinary', $Context.Fake,
        '-StallSeconds', '10', '-MaxResumes', [string]$MaxResumes
    )
    if ($Fast) {
        $runnerArguments += '-Fast'
    }
    $argumentLine = (@($runnerArguments) | ForEach-Object { ConvertTo-ProcessArgument -Value ([string]$_) }) -join ' '
    $stdoutPath = Join-Path $Context.Root 'runner.stdout.txt'
    $stderrPath = Join-Path $Context.Root 'runner.stderr.txt'

    $savedPath = $env:PATH
    $savedPlan = $env:FAKE_GODOT_PLAN
    $savedState = $env:FAKE_GODOT_STATE
    try {
        # The runner takes the first dotnet on PATH, so the fake comes first, and every folder holding a real
        # dotnet.exe is left off the PATH it sees: no real SDK can be reached.
        $entries = @($savedPath -split ';' | Where-Object {
                $_.Length -gt 0 -and -not (Test-Path -LiteralPath (Join-Path $_ 'dotnet.exe') -PathType Leaf)
            })
        $env:PATH = (@($Context.Bin) + $entries) -join ';'
        $env:FAKE_GODOT_PLAN = $Context.Plan
        $env:FAKE_GODOT_STATE = $Context.State
        $startArguments = @{
            FilePath = $RunnerShell
            ArgumentList = $argumentLine
            RedirectStandardOutput = $stdoutPath
            RedirectStandardError = $stderrPath
            WindowStyle = 'Hidden'
            PassThru = $true
        }
        $process = Start-Process @startArguments
        $null = $process.Handle
        if (-not $process.WaitForExit(120000)) {
            $killArguments = @('/PID', [string]$process.Id, '/T', '/F')
            Invoke-NativeQuiet -FilePath 'taskkill.exe' -Arguments $killArguments -Directory $Context.Root | Out-Null
            throw "Runner did not finish within 120 s in $($Context.Name); its process tree was killed."
        }
        $exitCode = $process.ExitCode
    }
    finally {
        $env:PATH = $savedPath
        $env:FAKE_GODOT_PLAN = $savedPlan
        $env:FAKE_GODOT_STATE = $savedState
    }
    return [pscustomobject]@{
        ExitCode = $exitCode
        Stdout = Read-RunnerText -Path $stdoutPath
        Stderr = Read-RunnerText -Path $stderrPath
    }
}

function Assert-That {
    param([bool]$Condition, [string]$Message)

    if (-not $Condition) {
        throw "Check failed: $Message"
    }
}

function Assert-Equal {
    param($Expected, $Actual, [string]$Message)

    if ($Expected -ne $Actual) {
        throw "Check failed: $Message (expected [$Expected], got [$Actual])"
    }
}

function Assert-ExitCode {
    param($Result, [int]$Expected, [string]$Message)

    if ($Result.ExitCode -ne $Expected) {
        $errorLine = @($Result.Stderr -split "`r?`n" |
                Where-Object { $_ -like 'RUNNER-ERROR*' }) | Select-Object -First 1
        throw "Check failed: $Message (expected exit $Expected, got $($Result.ExitCode)) $errorLine"
    }
}

function Assert-Line {
    param([string]$Text, [string]$Expected, [string]$Message)

    $lines = @($Text -split "`r?`n")
    if (-not ($lines -ccontains $Expected)) {
        throw "Check failed: $Message (no line [$Expected])"
    }
}

function Assert-Contains {
    param([string]$Text, [string]$Needle, [string]$Message)

    if (-not $Text.Contains($Needle)) {
        throw "Check failed: $Message (missing [$Needle])"
    }
}

function Assert-ProcessesGone {
    param($Context)

    $records = @(Get-FakeProcessRecords -Context $Context)
    Assert-That ($records.Count -gt 0) 'a stalled fake recorded its process IDs'
    $alive = @()
    for ($attempt = 0; $attempt -lt 10; $attempt++) {
        $alive = @($records | Where-Object {
                $live = Get-Process -Id $_.Id -ErrorAction SilentlyContinue
                $null -ne $live -and $live.ProcessName -eq $_.Name
            })
        if ($alive.Count -eq 0) {
            return
        }
        Start-Sleep -Milliseconds 500
    }
    $survivors = @($alive | ForEach-Object { "$($_.Id) $($_.Name)" }) -join ', '
    throw "Check failed: stalled fake process(es) survived the kill: $survivors"
}

function Get-RunnerTestCalls {
    param($Context)

    return @(Get-FakeCalls -Context $Context | Where-Object { $_ -like '*GdUnitCmdTool.gd*' })
}

function Test-CleanRun {
    param($Context)

    Set-FakePlan -Context $Context -Index 0 -Lines @(
        (Get-SuiteStart $SuiteAlpha), (Get-SuiteStats),
        (Get-SuiteStart $SuiteAlphaTwo), (Get-SuiteStats),
        (Get-SuiteStart $SuiteBeta), (Get-SuiteStats),
        'report r1 0 0',
        'exit 0')
    $result = Invoke-Runner -Context $Context
    Assert-ExitCode -Result $result -Expected 0 -Message 'a clean run exits 0'
    $consoleLine = 'Using Godot executable: ' + $Context.Console
    Assert-Line $result.Stdout $consoleLine 'the runner picks the _console variant next to the fake'
    Assert-Line $result.Stdout 'GdUnit passed: 3 of 3 suite files ran.' 'every suite ran once'
    Assert-That (-not (Get-RunnerOutput $result).Contains('WARNING:')) 'a clean run prints no WARNING'
    $calls = @(Get-FakeCalls -Context $Context)
    Assert-Equal 1 @($calls | Where-Object { $_ -like 'dotnet build *' }).Count 'one solution build'
    Assert-Equal 1 @($calls | Where-Object { $_ -like '* --import *' }).Count 'one project import'
    $tests = @(Get-RunnerTestCalls -Context $Context)
    Assert-Equal 1 $tests.Count 'one test invocation'
    $fullRunArguments = '-rd reports/ci -rc 100000 -a res://Tests'
    Assert-That $tests[0].EndsWith($fullRunArguments) 'a full run starts from the Tests folder'
}

function Test-Fast {
    param($Context)

    Write-FileText -Path (Join-Path $Context.Repo 'Tests\Gamma\GammaTest.cs') -Text $CommentedUnitSuite
    Set-FakePlan -Context $Context -Index 0 -Lines @(
        (Get-SuiteStart $SuiteAlpha), (Get-SuiteStats),
        'report r1 0 0',
        'exit 0')
    $result = Invoke-Runner -Context $Context -Fast
    Assert-ExitCode -Result $result -Expected 0 -Message 'a -Fast run exits 0'
    Assert-Line $result.Stdout 'GdUnit passed: 1 of 1 suite files ran.' 'only the Unit-category suite runs'
    $tests = @(Get-RunnerTestCalls -Context $Context)
    Assert-Equal 1 $tests.Count 'one test invocation'
    Assert-That $tests[0].EndsWith('-a res://Tests/Alpha/AlphaTest.cs') 'the -Fast run names only the Unit suite file'
}

function Test-StallResumes {
    param($Context)

    Set-FakePlan -Context $Context -Index 0 -Lines @(
        (Get-SuiteStart $SuiteAlpha), (Get-SuiteStats),
        (Get-SuiteStart $SuiteAlphaTwo),
        ('print ' + $SuiteAlphaTwo + ' > First PASSED'),
        'stall')
    Set-FakePlan -Context $Context -Index 1 -Lines @(
        (Get-SuiteStart $SuiteAlphaTwo), (Get-SuiteStats),
        (Get-SuiteStart $SuiteBeta), (Get-SuiteStats),
        'report r1 0 0',
        'exit 0')
    $result = Invoke-Runner -Context $Context
    $output = Get-FlatRunnerOutput $result
    Assert-ExitCode -Result $result -Expected 0 -Message 'a run that resumes after one stall exits 0'
    $stallWarning = 'WARNING: GdUnit stalled in ' + $SuiteAlphaTwo + ' (no output for 10 s). Last result: ' +
        $SuiteAlphaTwo + ' > First PASSED'
    Assert-Contains $output $stallWarning 'the stall names the unfinished suite and its last result'
    $resumeLine = 'Resuming with 2 unfinished suite(s): ' + $SuiteAlphaTwo + ' res://Tests/Beta'
    Assert-Line $result.Stdout $resumeLine 'the resume names the unfinished suite and the Beta folder'
    Assert-Line $result.Stdout 'GdUnit passed: 3 of 3 suite files ran.' 'every suite finished across both attempts'
    $passedWarning = 'WARNING: GdUnit passed after 1 stall(s): in ' + $SuiteAlphaTwo
    Assert-Contains $output $passedWarning 'a passing run still reports its stall'
    $tests = @(Get-RunnerTestCalls -Context $Context)
    Assert-Equal 2 $tests.Count 'one run and one resume'
    $resumeArguments = '-a ' + $SuiteAlphaTwo + ' -a res://Tests/Beta'
    Assert-That $tests[1].EndsWith($resumeArguments) 'the resume names only the unfinished suite and the Beta folder'
    $resumeLog = Join-Path $Context.Repo 'reports\ci\godot.resume1.stdout.log'
    Assert-That (Test-Path -LiteralPath $resumeLog -PathType Leaf) 'the resume writes its own log'
    Assert-ProcessesGone -Context $Context
}

function Test-RepeatedStallFails {
    param($Context)

    Set-FakePlan -Context $Context -Index 0 -Lines @(
        (Get-SuiteStart $SuiteAlpha), (Get-SuiteStats),
        (Get-SuiteStart $SuiteAlphaTwo),
        ('print ' + $SuiteAlphaTwo + ' > First PASSED'),
        'stall')
    Set-FakePlan -Context $Context -Index 1 -Lines @(
        (Get-SuiteStart $SuiteAlphaTwo),
        'stall')
    $result = Invoke-Runner -Context $Context
    Assert-ExitCode -Result $result -Expected 1 -Message 'a suite that stalls twice fails the run'
    $twiceError = 'RUNNER-ERROR: GdUnit stalled twice in ' + $SuiteAlphaTwo + '; see '
    Assert-Contains $result.Stderr $twiceError 'the second stall names the same suite'
    $tests = @(Get-RunnerTestCalls -Context $Context)
    Assert-Equal 2 $tests.Count 'no third attempt after the repeated stall'
    Assert-ProcessesGone -Context $Context
}

function Test-CrashNoReport {
    param($Context)

    # A crash that leaves no report is an interruption, handled like a stall: the runner names the suite, logs the exit
    # code in hex and resumes the unfinished suites once. The green resume then passes the run.
    Set-FakePlan -Context $Context -Index 0 -Lines @(
        (Get-SuiteStart $SuiteAlpha), (Get-SuiteStats),
        (Get-SuiteStart $SuiteAlphaTwo),
        'exit -1073741795')
    Set-FakePlan -Context $Context -Index 1 -Lines @(
        (Get-SuiteStart $SuiteAlphaTwo), (Get-SuiteStats),
        (Get-SuiteStart $SuiteBeta), (Get-SuiteStats),
        'report r1 0 0',
        'exit 0')
    $result = Invoke-Runner -Context $Context
    $output = Get-FlatRunnerOutput $result
    Assert-ExitCode -Result $result -Expected 0 -Message 'a run that resumes after one crash exits 0'
    $crashWarning = 'WARNING: GdUnit crashed in ' + $SuiteAlphaTwo + ' (exit 0xC000001D, no results report). ' +
        'Last result: Statistics: 0 test cases | 0 errors | 0 failures'
    Assert-Contains $output $crashWarning 'the crash names the unfinished suite and its exit code in hex'
    $resumeLine = 'Resuming with 2 unfinished suite(s): ' + $SuiteAlphaTwo + ' res://Tests/Beta'
    Assert-Line $result.Stdout $resumeLine 'the resume names the unfinished suite and the Beta folder'
    Assert-Line $result.Stdout 'GdUnit passed: 3 of 3 suite files ran.' 'every suite finished across both attempts'
    $passedWarning = 'WARNING: GdUnit passed after 1 interruption(s): crash 0xC000001D in ' + $SuiteAlphaTwo
    Assert-Contains $output $passedWarning 'a passing run still reports its crash'
    $tests = @(Get-RunnerTestCalls -Context $Context)
    Assert-Equal 2 $tests.Count 'one run and one resume'
    $resumeArguments = '-a ' + $SuiteAlphaTwo + ' -a res://Tests/Beta'
    Assert-That $tests[1].EndsWith($resumeArguments) 'the resume names only the unfinished suite and the Beta folder'
}

function Test-CrashAfterEarlierReportResumes {
    param($Context)

    # An earlier attempt's green report must not turn a later crash into an exit after a green report. Attempt 0 writes
    # a report for Alpha and then stalls in AlphaTwo. Attempt 1 finishes AlphaTwo and crashes in Beta without writing a
    # report of its own, so the crash is resumed. Attempt 2 finishes Beta. A crash in AlphaTwo itself would repeat the
    # stall in that suite and fail the run, so this crash comes after AlphaTwo has finished.
    Set-FakePlan -Context $Context -Index 0 -Lines @(
        (Get-SuiteStart $SuiteAlpha), (Get-SuiteStats),
        'report r1 0 0',
        (Get-SuiteStart $SuiteAlphaTwo),
        'stall')
    Set-FakePlan -Context $Context -Index 1 -Lines @(
        (Get-SuiteStart $SuiteAlphaTwo), (Get-SuiteStats),
        (Get-SuiteStart $SuiteBeta),
        'exit -1073741795')
    Set-FakePlan -Context $Context -Index 2 -Lines @(
        (Get-SuiteStart $SuiteBeta), (Get-SuiteStats),
        'report r2 0 0',
        'exit 0')
    $result = Invoke-Runner -Context $Context
    $output = Get-FlatRunnerOutput $result
    Assert-ExitCode -Result $result -Expected 0 -Message 'a crash after an earlier green report resumes and exits 0'
    $tests = @(Get-RunnerTestCalls -Context $Context)
    Assert-Equal 3 $tests.Count 'one run, then one resume after the stall and one after the crash'
    $stallResume = '-a ' + $SuiteAlphaTwo + ' -a res://Tests/Beta'
    Assert-That $tests[1].EndsWith($stallResume) 'the stall resume names AlphaTwo and the Beta folder'
    Assert-That $tests[2].EndsWith('-a res://Tests/Beta') 'the crash resume names only the Beta folder'
    $crashWarning = 'WARNING: GdUnit crashed in ' + $SuiteBeta + ' (exit 0xC000001D, no results report).'
    Assert-Contains $output $crashWarning 'the crash is judged by its own attempt, not by the earlier report'
    Assert-Line $result.Stdout 'Resuming with 1 unfinished suite(s): res://Tests/Beta' 'the crash resumes only Beta'
    Assert-Line $result.Stdout 'GdUnit passed: 3 of 3 suite files ran.' 'every suite finished across the three attempts'
    $passedWarning = 'WARNING: GdUnit passed after 2 interruption(s): stall in ' + $SuiteAlphaTwo +
        '; crash 0xC000001D in ' + $SuiteBeta
    Assert-Contains $output $passedWarning 'the passing run reports both the stall and the crash'
    Assert-ProcessesGone -Context $Context
}

function Test-RepeatedCrashFails {
    param($Context)

    Set-FakePlan -Context $Context -Index 0 -Lines @(
        (Get-SuiteStart $SuiteAlpha), (Get-SuiteStats),
        (Get-SuiteStart $SuiteAlphaTwo),
        'exit -1073741795')
    Set-FakePlan -Context $Context -Index 1 -Lines @(
        (Get-SuiteStart $SuiteAlphaTwo),
        'exit -1073741795')
    $result = Invoke-Runner -Context $Context
    Assert-ExitCode -Result $result -Expected 1 -Message 'a suite that crashes twice fails the run'
    $twiceError = 'RUNNER-ERROR: GdUnit was interrupted twice in ' + $SuiteAlphaTwo +
        ' (crash 0xC000001D, then crash 0xC000001D); see '
    Assert-Contains $result.Stderr $twiceError 'the second crash names the same suite'
    $tests = @(Get-RunnerTestCalls -Context $Context)
    Assert-Equal 2 $tests.Count 'no third attempt after the repeated crash'
}

function Test-StallThenCrashFails {
    param($Context)

    Set-FakePlan -Context $Context -Index 0 -Lines @(
        (Get-SuiteStart $SuiteAlpha), (Get-SuiteStats),
        (Get-SuiteStart $SuiteAlphaTwo),
        'stall')
    Set-FakePlan -Context $Context -Index 1 -Lines @(
        (Get-SuiteStart $SuiteAlphaTwo),
        'exit -1073741795')
    $result = Invoke-Runner -Context $Context
    Assert-ExitCode -Result $result -Expected 1 -Message 'a stall and a crash in the same suite fail the run'
    $repeat = 'RUNNER-ERROR: GdUnit was interrupted twice in ' + $SuiteAlphaTwo +
        ' (stall, then crash 0xC000001D); see '
    Assert-Contains $result.Stderr $repeat 'the crash repeats the stall in the same suite'
    $tests = @(Get-RunnerTestCalls -Context $Context)
    Assert-Equal 2 $tests.Count 'no third attempt after the repeated interruption'
    Assert-ProcessesGone -Context $Context
}

function Test-CrashResumeNoProgressFails {
    param($Context)

    Set-FakePlan -Context $Context -Index 0 -Lines @(
        (Get-SuiteStart $SuiteAlpha), (Get-SuiteStats),
        (Get-SuiteStart $SuiteAlphaTwo),
        'exit -1073741795')
    Set-FakePlan -Context $Context -Index 1 -Lines @('exit -1073741795')
    $result = Invoke-Runner -Context $Context
    Assert-ExitCode -Result $result -Expected 1 -Message 'a resume that crashes without finishing a suite fails the run'
    $noProgress = 'RUNNER-ERROR: GdUnit crashed (exit 0xC000001D) before the first suite again after resuming, ' +
        'without finishing any suite (last finished: ' + $SuiteAlpha + ')'
    Assert-Contains $result.Stderr $noProgress 'the no-progress rule names the crash and the last finished suite'
    $tests = @(Get-RunnerTestCalls -Context $Context)
    Assert-Equal 2 $tests.Count 'no third attempt after a resume that finished nothing'
}

function Test-InterruptionBudget {
    param($Context)

    Set-FakePlan -Context $Context -Index 0 -Lines @(
        (Get-SuiteStart $SuiteAlpha), (Get-SuiteStats),
        (Get-SuiteStart $SuiteAlphaTwo),
        'stall')
    Set-FakePlan -Context $Context -Index 1 -Lines @(
        (Get-SuiteStart $SuiteAlphaTwo), (Get-SuiteStats),
        (Get-SuiteStart $SuiteBeta),
        'exit -1073741795')
    $result = Invoke-Runner -Context $Context -MaxResumes 1
    Assert-ExitCode -Result $result -Expected 1 -Message 'a stall and a crash share the -MaxResumes budget'
    $budget = 'RUNNER-ERROR: GdUnit was interrupted 2 times (limit 1 resumes); last interruption: ' +
        'crash 0xC000001D in ' + $SuiteBeta
    Assert-Contains $result.Stderr $budget 'the second interruption exceeds the shared budget'
    $tests = @(Get-RunnerTestCalls -Context $Context)
    Assert-Equal 2 $tests.Count 'no third attempt past the shared budget'
    Assert-ProcessesGone -Context $Context
}

# Runs one more scenario in its own fake repository, so its folders also sit under the fixture's temporary root.
function Invoke-ExitScenario {
    param($Context, [string]$Name, [string[]]$Lines)

    $scenario = New-ScenarioContext -Name $Name -BuildExe $Context.Fake
    Set-FakePlan -Context $scenario -Index 0 -Lines $Lines
    $result = Invoke-Runner -Context $scenario
    Assert-Equal 1 @(Get-RunnerTestCalls -Context $scenario).Count "$Name runs Godot once"
    return $result
}

function Test-GdUnitExitCodes {
    param($Context)

    # gdUnit's own exit codes keep today's handling, and an abnormal exit after a green report still fails. None of
    # these scenarios resumes, so each one runs Godot exactly once.
    $suites = @(
        (Get-SuiteStart $SuiteAlpha), (Get-SuiteStats),
        (Get-SuiteStart $SuiteAlphaTwo), (Get-SuiteStats),
        (Get-SuiteStart $SuiteBeta), (Get-SuiteStats))
    $result = Invoke-ExitScenario -Context $Context -Name 'Exit100' -Lines ($suites + @('report r1 1 0', 'exit 100'))
    Assert-ExitCode -Result $result -Expected 1 -Message 'exit 100 fails the run'
    $notGreen = 'RUNNER-ERROR: GdUnit run is not green: failures=1, errors=0'
    Assert-Contains $result.Stderr $notGreen 'exit 100 fails from its report'

    $result = Invoke-ExitScenario -Context $Context -Name 'Exit101' -Lines ($suites + @('report r1 0 0', 'exit 101'))
    Assert-ExitCode -Result $result -Expected 0 -Message 'exit 101 with a green report passes'
    $orphanWarning = 'WARNING: GdUnit reported orphan-node warnings (exit code 101)'
    Assert-Contains (Get-FlatRunnerOutput $result) $orphanWarning 'exit 101 keeps its orphan-node warning'

    foreach ($code in @(103, 104)) {
        $result = Invoke-ExitScenario -Context $Context -Name ('Exit' + $code) -Lines @("exit $code")
        Assert-ExitCode -Result $result -Expected 1 -Message "exit $code fails the run"
        Assert-Contains $result.Stderr "(exit=$code)" "exit $code fails without a report and is not resumed"
    }

    $afterGreenLines = $suites + @('report r1 0 0', 'exit -1073741795')
    $result = Invoke-ExitScenario -Context $Context -Name 'ExitAfterGreen' -Lines $afterGreenLines
    Assert-ExitCode -Result $result -Expected 1 -Message 'an abnormal exit after a green report fails the run'
    $afterGreen = 'RUNNER-ERROR: Godot exited with code -1073741795 after a green report'
    Assert-Contains $result.Stderr $afterGreen 'the abnormal exit after a green report is named'
}

function Test-ReportAggregation {
    param($Context)

    # A report left by an earlier run must not be counted.
    $stale = Join-Path $Context.Repo 'reports\ci\old\results.xml'
    Write-FileText -Path $stale -Text $StaleReportXml
    (Get-Item -LiteralPath $stale).LastWriteTime = [datetime]'2000-01-01'
    Set-FakePlan -Context $Context -Index 0 -Lines @(
        (Get-SuiteStart $SuiteAlpha), (Get-SuiteStats -Cases 1 -ErrorCount 1 -FailureCount 0),
        (Get-SuiteStart $SuiteAlphaTwo),
        'stall')
    Set-FakePlan -Context $Context -Index 1 -Lines @(
        (Get-SuiteStart $SuiteAlphaTwo), (Get-SuiteStats),
        (Get-SuiteStart $SuiteBeta), (Get-SuiteStats),
        'report r2 2 0',
        'report r3 0 1',
        'exit 0')
    $result = Invoke-Runner -Context $Context
    Assert-ExitCode -Result $result -Expected 1 -Message 'aggregated failures and errors fail the run'
    $notGreen = 'RUNNER-ERROR: GdUnit run is not green: failures=2, errors=2, reports under'
    Assert-Contains $result.Stderr $notGreen ('the stalled attempt errors and both reports are counted, ' +
        'and the stale report is not')
}

# The runner's suite-detection patterns must match the ones in TestSuiteNamingTest.cs, the C# side that gdUnit's own
# self-check uses. The runner is parsed, not run, so this compares the shipped text.
function Test-PatternLockstep {
    param($Context)

    $tokens = $null
    $parseErrors = $null
    $ast = [System.Management.Automation.Language.Parser]::ParseFile($RealRunner, [ref]$tokens, [ref]$parseErrors)
    Assert-That ($parseErrors.Count -eq 0) 'the runner parses'
    $runnerValues = @{}
    $assignments = $ast.FindAll({
            param($node) $node -is [System.Management.Automation.Language.AssignmentStatementAst]
        }, $true)
    foreach ($assignment in @($assignments)) {
        $name = $assignment.Left.VariablePath.UserPath
        if ($name -ne 'SuiteAttributePattern' -and $name -ne 'CodeNoisePattern') {
            continue
        }
        # ASSUMPTION: Windows PowerShell 5.1, the fixture's shell in CI and locally, parses an assignment's value
        # as a CommandExpressionAst. The value is read from the AST and never executed.
        $literal = $null
        if ($assignment.Right -is [System.Management.Automation.Language.CommandExpressionAst]) {
            $literal = $assignment.Right.Expression
        }
        $isLiteral = $literal -is [System.Management.Automation.Language.StringConstantExpressionAst]
        Assert-That $isLiteral "the runner declares $name as one string literal"
        $runnerValues[$name] = $literal.Value
    }

    $source = [IO.File]::ReadAllText((Join-Path $RepositoryRoot 'Tests\TestSuiteNamingTest.cs'))
    foreach ($name in @('SuiteAttributePattern', 'CodeNoisePattern')) {
        Assert-That $runnerValues.ContainsKey($name) "the runner declares $name"
        $match = [regex]::Match($source, 'internal const string ' + $name + '\s*=\s*@"((?:[^"]|"")*)";')
        Assert-That $match.Success "TestSuiteNamingTest.cs declares $name as a verbatim const"
        $csharpValue = $match.Groups[1].Value.Replace('""', '"')
        Assert-Equal $csharpValue $runnerValues[$name] "$name is identical in the runner and TestSuiteNamingTest.cs"
    }
}

# Every check in run order. -Only must name one of them (letter case does not matter) and then runs alone.
$AllChecks = @(
    'CleanRun', 'Fast', 'StallResumes', 'RepeatedStallFails', 'CrashNoReport', 'CrashAfterEarlierReportResumes',
    'RepeatedCrashFails', 'StallThenCrashFails', 'CrashResumeNoProgressFails', 'InterruptionBudget',
    'GdUnitExitCodes', 'ReportAggregation', 'PatternLockstep')
if ([string]::IsNullOrWhiteSpace($Only)) {
    $checks = $AllChecks
}
else {
    # The canonical spelling comes from $AllChecks, so -Only fast runs Fast.
    $checks = @($AllChecks | Where-Object { $_ -eq $Only })
    if ($checks.Count -eq 0) {
        throw "Unknown check '$Only'. Valid checks: $($AllChecks -join ', ')"
    }
}

# The name column is as wide as the longest check name, so every row lines up whichever checks run.
$nameWidth = [int]($AllChecks | Measure-Object -Property Length -Maximum).Maximum
$passedCount = 0
$setupFailed = $false
$total = [Diagnostics.Stopwatch]::StartNew()
try {
    New-Item -ItemType Directory -Path $FixtureRoot -Force | Out-Null
    $buildExe = New-FakeBuild -Directory (Join-Path $FixtureRoot 'build')
    foreach ($check in $checks) {
        $started = Get-Date
        $context = New-ScenarioContext -Name $check -BuildExe $buildExe
        try {
            & ('Test-' + $check) -Context $context
            $status = 'PASS'
            $detail = ''
            $passedCount++
        }
        catch {
            $status = 'FAIL'
            $detail = $_.Exception.Message
        }
        $seconds = [math]::Round(((Get-Date) - $started).TotalSeconds, 1)
        Write-Host ('{0} {1}  {2,6} s  {3}' -f $check.PadRight($nameWidth), $status, $seconds, $detail)
    }
}
catch {
    $setupFailed = $true
    Write-Host "Runner fixture could not run: $($_.Exception.Message)"
}
finally {
    if ($KeepTemp) {
        Write-Host "Kept temporary fixture: $FixtureRoot"
    }
    elseif (Test-Path -LiteralPath $FixtureRoot) {
        Remove-Item -LiteralPath $FixtureRoot -Recurse -Force -ErrorAction SilentlyContinue
    }
}

$elapsedSeconds = [math]::Round($total.Elapsed.TotalSeconds, 1)
Write-Host ('Runner fixture: {0} of {1} checks passed in {2} s.' -f $passedCount, $checks.Count, $elapsedSeconds)
if ($setupFailed -or $passedCount -ne $checks.Count) {
    exit 1
}
exit 0
