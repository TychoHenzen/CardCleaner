# Fake-Godot integration fixture for Scripts/ci/Run-GdUnit.ps1.
#
# The runner runs against a fake Godot executable, compiled here with the in-box .NET Framework csc.exe, inside a
# temporary fake repository. The build wait (a build that leaves a worker running), a failed build, the import stamp
# (skip, -ForceImport, a new script, a failed import), the watchdog,
# process-tree kill, resume targets, repeated-stall failure, crash handling, interruption marker and report
# aggregation are therefore checked without a real Godot. Windows PowerShell 5.1 runs it locally; CI passes
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
$SuiteGamma = 'res://Tests/Gamma/GammaTest.cs'
$SuiteDelta = 'res://Tests/Delta/DeltaTest.cs'

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
    // One compiled program plays three roles: dotnet (build, which replays build.txt when the scenario writes one),
    // a grandchild started by a stall or a lingering build (sleep), and Godot itself, which replays the next plan file
    // named by FAKE_GODOT_PLAN.
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
                return RunBuild(state, Environment.GetEnvironmentVariable("FAKE_GODOT_PLAN"));
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

    // build.txt in the plan folder, when a scenario writes it: "linger" leaves a worker running after the build exits,
    // as MSBuild node reuse does, and "exit <n>" fails the build with exit code n. Without the file the build succeeds.
    private static int RunBuild(string state, string plan)
    {
        string planPath = Path.Combine(plan, "build.txt");
        if (!File.Exists(planPath))
        {
            return 0;
        }
        foreach (string raw in File.ReadAllLines(planPath))
        {
            string line = raw.TrimEnd();
            if (line == "linger")
            {
                Linger(state);
            }
            else if (line.StartsWith("exit ", StringComparison.Ordinal))
            {
                int code = int.Parse(line.Substring(5));
                if (code != 0)
                {
                    Console.Error.WriteLine("FAKE: build failed");
                    Console.Error.Flush();
                }
                return code;
            }
            else if (line.Length > 0)
            {
                Console.Out.WriteLine("FAKE: unknown build directive " + line);
                Console.Out.Flush();
                return 97;
            }
        }
        return 0;
    }

    // The dotnet copy cannot play sleep (its role is checked first), so the worker is the FakeGodot.exe next to it.
    // The pause first puts the build well past its start, as a real MSBuild node starts mid-build, so a runner that
    // waits on the whole process tree has already begun tracking it.
    // ASSUMPTION: an MSBuild node inherits no handles from the build, so the worker is started through the shell, which
    // passes none; it then cannot hold the runner's or the build's redirected output files open.
    private static void Linger(string state)
    {
        Thread.Sleep(2500);
        string self = Assembly.GetEntryAssembly().Location;
        var psi = new ProcessStartInfo(Path.Combine(Path.GetDirectoryName(self), "FakeGodot.exe"), "sleep");
        psi.UseShellExecute = true;
        psi.WindowStyle = ProcessWindowStyle.Hidden;
        Process child = Process.Start(psi);
        File.AppendAllText(Path.Combine(state, "pids.txt"), child.Id + " FakeGodot" + Environment.NewLine);
    }

    private static int RunGodot(string[] args, string self, string state, string plan)
    {
        Log(state, Path.GetFileName(self), args);
        if (Array.IndexOf(args, "--import") >= 0)
        {
            RecordAttemptLogsBeforeRun(state);
            return RunImport(plan);
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
                WriteReport(ReportRootFor(args), parts[1], int.Parse(parts[2]), int.Parse(parts[3]));
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

    // Every import leaves .godot\imported, as a real one does. import.txt in the plan folder, when a scenario writes
    // it: "create <path>" writes that file (a sidecar the import adds), "exit <n>" fails the import with exit code n.
    private static int RunImport(string plan)
    {
        string repo = Directory.GetCurrentDirectory();
        Directory.CreateDirectory(Path.Combine(repo, Path.Combine(".godot", "imported")));
        string planPath = Path.Combine(plan, "import.txt");
        if (!File.Exists(planPath))
        {
            return 0;
        }
        foreach (string raw in File.ReadAllLines(planPath))
        {
            string line = raw.TrimEnd();
            if (line.StartsWith("create ", StringComparison.Ordinal))
            {
                File.WriteAllText(Path.Combine(repo, line.Substring(7)), "uid://fake");
            }
            else if (line.StartsWith("exit ", StringComparison.Ordinal))
            {
                Console.Error.WriteLine("FAKE: import failed");
                Console.Error.Flush();
                return int.Parse(line.Substring(5));
            }
            else if (line.Length > 0)
            {
                Console.Out.WriteLine("FAKE: unknown import directive " + line);
                Console.Out.Flush();
                return 96;
            }
        }
        return 0;
    }

    private static void RecordAttemptLogsBeforeRun(string state)
    {
        string reportRoot = Path.Combine(Directory.GetCurrentDirectory(), "reports", "ci");
        var leftovers = new StringBuilder();
        if (Directory.Exists(reportRoot))
        {
            foreach (string path in Directory.GetFiles(reportRoot))
            {
                string name = Path.GetFileName(path);
                if (IsAttemptLogName(name))
                {
                    leftovers.AppendLine(name);
                }
            }
        }
        File.WriteAllText(Path.Combine(state, "attempt-logs-before-run.txt"), leftovers.ToString());
    }

    private static bool IsAttemptLogName(string name)
    {
        return name == "godot.stdout.log" || name == "godot.stderr.log" ||
            (name.StartsWith("godot.resume", StringComparison.Ordinal) &&
                (name.EndsWith(".stdout.log", StringComparison.Ordinal) ||
                 name.EndsWith(".stderr.log", StringComparison.Ordinal)));
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

    // The folder the real runner passes after -rd (relative to the current directory); reports/ci when it is absent.
    private static string ReportRootFor(string[] args)
    {
        int index = Array.IndexOf(args, "-rd");
        string root = index >= 0 && index + 1 < args.Length ? args[index + 1] : Path.Combine("reports", "ci");
        return Path.Combine(Directory.GetCurrentDirectory(), root);
    }

    private static void WriteReport(string root, string dir, int failures, int errors)
    {
        string folder = Path.Combine(root, dir);
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
    [switch]$SkipDaily,
    [string[]]$Suite = @(),
    [string]$ReportDirectory = '',
    [switch]$ForceImport
)

$ErrorActionPreference = 'Stop'
try {
    $splat = @{ GodotBinary = $GodotBinary; StallSeconds = $StallSeconds; MaxResumes = $MaxResumes }
    if ($SkipDaily) {
        $splat['SkipDaily'] = $true
    }
    if ($ForceImport) {
        $splat['ForceImport'] = $true
    }
    # The suite list travels as one comma-joined string, because the wrapper is a child process.
    # Empty entries are kept, so the runner is the one that rejects them.
    if ($PSBoundParameters.ContainsKey('Suite')) {
        $splat['Suite'] = @($Suite | ForEach-Object { $_ -split ',' })
    }
    if ($ReportDirectory) {
        $splat['ReportDirectory'] = $ReportDirectory
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

# A Daily suite: -SkipDaily leaves it out.
$DailySuite = @'
[TestSuite]
[TestCategory("Daily")]
public class GammaTest
{
}
'@

# A suite whose only Daily category is commented out: -SkipDaily must still run it.
$CommentedDailySuite = @'
// [TestCategory("Daily")]
[TestSuite]
public class DeltaTest
{
}
'@

# Phantoms: a suite attribute that appears only inside a comment or a string is not a suite, so the runner must not
# expect these files. The block-comment phantom carries the Daily category too, so -SkipDaily would skip it if it
# counted.
$PhantomBlockComment = @'
/*
[TestSuite]
[TestCategory("Daily")]
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
    # The real repository commits this empty file, so Godot and the import stamp both leave reports/ out.
    Write-FileText -Path (Join-Path $repo 'reports\.gdignore') -Text ''
    return $context
}

function Set-FakePlan {
    param($Context, [int]$Index, [string[]]$Lines)

    [IO.File]::WriteAllLines((Join-Path $Context.Plan ('run' + $Index + '.txt')), [string[]]$Lines)
}

# The fake build's own plan: 'linger' leaves a worker running after the build exits, 'exit <n>' fails the build.
function Set-FakeBuildPlan {
    param($Context, [string[]]$Lines)

    [IO.File]::WriteAllLines((Join-Path $Context.Plan 'build.txt'), [string[]]$Lines)
}

# The fake import's own plan: 'create <path>' adds that file, 'exit <n>' fails the import. No lines: it succeeds.
function Set-FakeImportPlan {
    param($Context, [string[]]$Lines = @())

    [IO.File]::WriteAllLines((Join-Path $Context.Plan 'import.txt'), [string[]]$Lines)
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
    param(
        $Context, [switch]$SkipDaily, [int]$MaxResumes = 3, [string[]]$Suite = @(), [string]$ReportDirectory = '',
        [switch]$ForceImport
    )

    $runnerArguments = @(
        '-NoProfile', '-ExecutionPolicy', 'Bypass', '-File', $Context.Wrapper,
        '-RunnerPath', $Context.Runner, '-GodotBinary', $Context.Fake,
        '-StallSeconds', '3', '-MaxResumes', [string]$MaxResumes
    )
    if ($SkipDaily) {
        $runnerArguments += '-SkipDaily'
    }
    if ($ForceImport) {
        $runnerArguments += '-ForceImport'
    }
    if ($Suite.Count -gt 0) {
        $runnerArguments += '-Suite', ($Suite -join ',')
    }
    if ($ReportDirectory) {
        $runnerArguments += '-ReportDirectory', $ReportDirectory
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
    Assert-That ($records.Count -gt 0) 'a fake recorded its process IDs'
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
    throw "Check failed: fake process(es) are still running: $survivors"
}

# Stops every fake process a scenario recorded that is still running. A worker a build leaves running is meant to
# outlive the build, so the runner does not stop it; the scenario does.
function Stop-FakeProcesses {
    param($Context)

    foreach ($record in @(Get-FakeProcessRecords -Context $Context)) {
        $live = Get-Process -Id $record.Id -ErrorAction SilentlyContinue
        if ($null -ne $live -and $live.ProcessName -eq $record.Name) {
            Stop-Process -Id $record.Id -Force -ErrorAction SilentlyContinue
        }
    }
}

function Get-InterruptionMarkerPath {
    param($Context)

    return Join-Path $Context.Repo 'reports\ci\interruptions.txt'
}

# The marker's lines, or an empty array when the runner left no marker.
function Get-InterruptionMarkerLines {
    param($Context)

    $path = Get-InterruptionMarkerPath -Context $Context
    if (-not (Test-Path -LiteralPath $path -PathType Leaf)) {
        return @()
    }
    return @([IO.File]::ReadAllLines($path))
}

function Get-RunnerTestCalls {
    param($Context)

    return @(Get-FakeCalls -Context $Context | Where-Object { $_ -like '*GdUnitCmdTool.gd*' })
}

function Test-CleanRun {
    param($Context)

    $reportRoot = Join-Path $Context.Repo 'reports\ci'
    foreach ($name in @(
            'godot.stdout.log', 'godot.stderr.log',
            'godot.resume1.stdout.log', 'godot.resume1.stderr.log')) {
        Write-FileText -Path (Join-Path $reportRoot $name) -Text 'stale attempt log'
    }
    $unrelatedLog = Join-Path $reportRoot 'godot.resume.log'
    $nestedLog = Join-Path $reportRoot 'report_N\godot.resume99.stdout.log'
    Write-FileText -Path $unrelatedLog -Text 'keep this direct file'
    Write-FileText -Path $nestedLog -Text 'keep this nested file'
    Set-FakePlan -Context $Context -Index 0 -Lines @(
        (Get-SuiteStart $SuiteAlpha), (Get-SuiteStats -Cases 3),
        (Get-SuiteStart $SuiteAlphaTwo), (Get-SuiteStats -Cases 4),
        (Get-SuiteStart $SuiteBeta), (Get-SuiteStats -Cases 5),
        'report r1 0 0',
        'exit 0')
    # A marker left by an earlier run in the same checkout must not make a clean run look interrupted.
    Write-FileText -Path (Get-InterruptionMarkerPath -Context $Context) -Text ('stall in ' + $SuiteBeta)
    $result = Invoke-Runner -Context $Context
    Assert-ExitCode -Result $result -Expected 0 -Message 'a clean run exits 0'
    $snapshotPath = Join-Path $Context.State 'attempt-logs-before-run.txt'
    Assert-That (Test-Path -LiteralPath $snapshotPath -PathType Leaf) 'the fake import inspected attempt logs before the run'
    $leftovers = @([IO.File]::ReadAllLines($snapshotPath))
    Assert-Equal 0 $leftovers.Count 'stale attempt logs are gone before the current run'
    foreach ($name in @('godot.stdout.log', 'godot.stderr.log')) {
        Assert-That (Test-Path -LiteralPath (Join-Path $reportRoot $name) -PathType Leaf) `
            "the clean run writes its current $name"
    }
    Assert-That (-not (Test-Path -LiteralPath (Join-Path $reportRoot 'godot.resume1.stdout.log'))) `
        'a clean run leaves no stale resume stdout log'
    Assert-That (-not (Test-Path -LiteralPath (Join-Path $reportRoot 'godot.resume1.stderr.log'))) `
        'a clean run leaves no stale resume stderr log'
    Assert-That (Test-Path -LiteralPath $unrelatedLog -PathType Leaf) 'an unrelated direct log survives cleanup'
    Assert-That (Test-Path -LiteralPath $nestedLog -PathType Leaf) 'a matching log in report_N survives cleanup'
    $markerGone = -not (Test-Path -LiteralPath (Get-InterruptionMarkerPath -Context $Context))
    Assert-That $markerGone 'a clean run removes the stale interruption marker and writes none'
    $consoleLine = 'Using Godot executable: ' + $Context.Console
    Assert-Line $result.Stdout $consoleLine 'the runner picks the _console variant next to the fake'
    Assert-Line $result.Stdout 'GdUnit passed: 3 of 3 suite files ran.' 'every suite ran once'
    Assert-Line $result.Stdout "Suite result: $SuiteAlpha cases=3 errors=0 failures=0" 'the Alpha suite gets its result line'
    Assert-Line $result.Stdout "Suite result: $SuiteAlphaTwo cases=4 errors=0 failures=0" 'the AlphaTwo suite gets its result line'
    Assert-Line $result.Stdout "Suite result: $SuiteBeta cases=5 errors=0 failures=0" 'the Beta suite gets its result line'
    Assert-That (-not (Get-RunnerOutput $result).Contains('WARNING:')) 'a clean run prints no WARNING'
    $calls = @(Get-FakeCalls -Context $Context)
    Assert-Equal 1 @($calls | Where-Object { $_ -like 'dotnet build *' }).Count 'one solution build'
    Assert-Equal 1 @($calls | Where-Object { $_ -like '* --import *' }).Count 'one project import'
    $tests = @(Get-RunnerTestCalls -Context $Context)
    Assert-Equal 1 $tests.Count 'one test invocation'
    Assert-That $tests[0].EndsWith('-rd reports/ci -a res://Tests') 'a full run starts from the Tests folder'
    Assert-That (-not $tests[0].Contains(' -rc ')) 'a full run leaves the report count to gdUnit, which keeps 20'
}

function Test-SingleSuite {
    param($Context)

    Set-FakePlan -Context $Context -Index 0 -Lines @(
        (Get-SuiteStart $SuiteBeta), (Get-SuiteStats -Cases 2), 'report r1 0 0', 'exit 0')
    $result = Invoke-Runner -Context $Context -Suite @($SuiteBeta) -ReportDirectory 'reports\single'
    Assert-ExitCode -Result $result -Expected 0 -Message 'a single-suite run exits 0'
    Assert-Line $result.Stdout 'GdUnit passed: 1 of 1 suite files ran.' 'only the named suite is expected'
    $tests = @(Get-RunnerTestCalls -Context $Context)
    Assert-Equal 1 $tests.Count 'one test invocation'
    Assert-That $tests[0].EndsWith("-rd reports/single -a $SuiteBeta") 'the call names the suite and the folder'
    Assert-That (Test-Path -LiteralPath (Join-Path $Context.Repo 'reports\single\godot.stdout.log') -PathType Leaf) 'logs go to the chosen folder'
}

function Test-OldReportFoldersPruned {
    param($Context)

    $reports = Join-Path $Context.Repo 'reports'
    $outside = Join-Path $Context.Root 'outside'
    Write-FileText -Path (Join-Path $outside 'sentinel.txt') -Text 'what a junction points at'
    # Eleven earlier run folders: old1 is the newest, old11 the oldest.
    for ($i = 1; $i -le 11; $i++) {
        Write-FileText -Path (Join-Path $reports "old$i\report_1\index.html") -Text 'an earlier report'
    }
    # The oldest folder holds a junction, so its delete must remove the link and leave what it points at.
    New-Item -ItemType Junction -Path (Join-Path $reports 'old11\link') -Target $outside | Out-Null
    # A junction directly under reports/ is no run folder and stays.
    New-Item -ItemType Junction -Path (Join-Path $reports 'toplink') -Target $outside | Out-Null
    Write-FileText -Path (Join-Path $reports 'e2e\stopped\shots\shot.png') -Text 'an e2e screenshot'
    Write-FileText -Path (Join-Path $reports 'notes.md') -Text 'a file directly under reports/'
    Write-FileText -Path (Join-Path $reports 'single\earlier.log') -Text 'from an earlier run in this folder'
    # Writing into a folder updates its write time, so the dates are set after the contents. Every old folder is dated
    # after now, so the junction made just now is older than all of them and only its type keeps it. The current run's
    # folder and reports/e2e are older still, so only their names keep them.
    $now = [DateTime]::UtcNow
    for ($i = 1; $i -le 11; $i++) {
        [IO.Directory]::SetLastWriteTimeUtc((Join-Path $reports "old$i"), $now.AddHours(12 - $i))
    }
    [IO.Directory]::SetLastWriteTimeUtc((Join-Path $reports 'single'), $now.AddDays(-60))
    [IO.Directory]::SetLastWriteTimeUtc((Join-Path $reports 'e2e'), $now.AddDays(-60))
    [IO.File]::SetLastWriteTimeUtc((Join-Path $reports 'notes.md'), $now.AddDays(-60))
    Set-FakePlan -Context $Context -Index 0 -Lines @(
        (Get-SuiteStart $SuiteBeta), (Get-SuiteStats -Cases 2), 'report r1 0 0', 'exit 0')

    $result = Invoke-Runner -Context $Context -Suite @($SuiteBeta) -ReportDirectory 'reports\single'

    Assert-ExitCode -Result $result -Expected 0 -Message 'a run that prunes old report folders exits 0'
    $left = @(Get-ChildItem -LiteralPath $reports -Force | ForEach-Object { $_.Name } | Sort-Object) -join ' '
    $expected = '.gdignore e2e notes.md old1 old2 old3 old4 old5 old6 old7 old8 old9 single toplink'
    Assert-Equal $expected $left 'the current folder and the 9 newest others stay, with every file, e2e and the link'
    Assert-Line $result.Stdout 'Removed old report folder: reports/old10' 'the runner names each folder it removes'
    Assert-Line $result.Stdout 'Removed old report folder: reports/old11' 'the runner names each folder it removes'
    Assert-That (Test-Path -LiteralPath (Join-Path $outside 'sentinel.txt') -PathType Leaf) `
        'removing a folder that holds a junction leaves what the junction points at'
    Assert-That (Test-Path -LiteralPath (Join-Path $reports 'e2e\stopped\shots\shot.png') -PathType Leaf) `
        'the runner leaves e2e sessions to the e2e tool'
    Assert-That (Test-Path -LiteralPath (Join-Path $reports 'single\earlier.log') -PathType Leaf) `
        'the current run folder keeps its earlier files'
    Assert-That (-not (Get-RunnerOutput $result).Contains('WARNING:')) 'a prune that removes every old folder prints no WARNING'
}

function Test-CommaSuiteList {
    param($Context)

    Set-FakePlan -Context $Context -Index 0 -Lines @(
        (Get-SuiteStart $SuiteAlpha), (Get-SuiteStats -Cases 3), (Get-SuiteStart $SuiteBeta), (Get-SuiteStats -Cases 5),
        'report r1 0 0', 'exit 0')
    $result = Invoke-Runner -Context $Context -Suite @($SuiteAlpha, $SuiteBeta)
    Assert-ExitCode -Result $result -Expected 0 -Message 'a comma-joined two-suite list exits 0'
    Assert-Line $result.Stdout 'GdUnit passed: 2 of 2 suite files ran.' 'both named suites are expected'
    $tests = @(Get-RunnerTestCalls -Context $Context)
    Assert-Equal 1 $tests.Count 'one test invocation'
    Assert-That ($tests[0].Contains($SuiteAlpha) -and $tests[0].Contains($SuiteBeta)) 'the call names both suites'
}

function Test-InvalidSuiteParameters {
    param($Context)

    # More earlier run folders than a run keeps, so a prune before the argument checks would remove one.
    for ($i = 1; $i -le 10; $i++) {
        Write-FileText -Path (Join-Path $Context.Repo "reports\old$i\report_1\index.html") -Text 'an earlier report'
    }
    foreach ($case in @(
            @{ Suite = @('res://Tests/Nope/NopeTest.cs'); Dir = ''; Expect = 'Unknown -Suite value(s): res://Tests/Nope/NopeTest.cs' },
            @{ Suite = @($SuiteAlpha, 'res://Tests/Nope/NopeTest.cs'); Dir = ''; Expect = 'Unknown -Suite value(s): res://Tests/Nope/NopeTest.cs' },
            @{ Suite = @(''); Dir = ''; Expect = 'Empty -Suite value' },
            @{ Suite = @(' '); Dir = ''; Expect = 'Empty -Suite value' },
            @{ Suite = @(); Dir = 'reports\..\escape'; Expect = '-ReportDirectory must be a relative folder below reports/' },
            @{ Suite = @(); Dir = 'elsewhere\ci'; Expect = '-ReportDirectory must be a relative folder below reports/' })) {
        $result = Invoke-Runner -Context $Context -Suite $case.Suite -ReportDirectory $case.Dir
        Assert-ExitCode -Result $result -Expected 1 -Message 'an invalid parameter fails the run'
        Assert-Contains $result.Stderr $case.Expect 'the error names the problem'
    }
    $result = Invoke-Runner -Context $Context -Suite @($SuiteAlpha) -SkipDaily
    Assert-ExitCode -Result $result -Expected 1 -Message '-Suite with -SkipDaily fails the run'
    Assert-Contains $result.Stderr '-Suite and -SkipDaily cannot be combined' 'the error names the conflict'
    Assert-Equal 0 @(Get-FakeCalls -Context $Context).Count 'neither the build nor Godot started'
    $oldFolders = @(Get-ChildItem -LiteralPath (Join-Path $Context.Repo 'reports') -Directory -Filter 'old*')
    Assert-Equal 10 $oldFolders.Count 'a run refused for its arguments removes no report folder'
}

function Test-SingleSuiteStall {
    param($Context)

    Set-FakePlan -Context $Context -Index 0 -Lines @((Get-SuiteStart $SuiteBeta), 'stall')
    Set-FakePlan -Context $Context -Index 1 -Lines @((Get-SuiteStart $SuiteBeta), 'stall')
    $result = Invoke-Runner -Context $Context -Suite @($SuiteBeta)
    Assert-ExitCode -Result $result -Expected 1 -Message 'a single suite that hangs fails the run'
    Assert-Contains $result.Stderr ('RUNNER-ERROR: GdUnit stalled twice in ' + $SuiteBeta) 'the hung suite is named'
    Assert-ProcessesGone -Context $Context
}

function Test-BuildLeavesWorkerRunning {
    param($Context)

    # A build that exits 0 but leaves a worker running (MSBuild node reuse) must not hold the runner until that worker
    # exits: the runner waits on dotnet itself. The fixture's 120 s runner timeout turns a tree wait into a failure.
    Set-FakeBuildPlan -Context $Context -Lines @('linger', 'exit 0')
    Set-FakePlan -Context $Context -Index 0 -Lines @(
        (Get-SuiteStart $SuiteAlpha), (Get-SuiteStats),
        (Get-SuiteStart $SuiteAlphaTwo), (Get-SuiteStats),
        (Get-SuiteStart $SuiteBeta), (Get-SuiteStats),
        'report r1 0 0',
        'exit 0')
    try {
        $result = Invoke-Runner -Context $Context
        $passMessage = 'a build that leaves a worker running still lets the run pass'
        Assert-ExitCode -Result $result -Expected 0 -Message $passMessage
        Assert-Line $result.Stdout 'GdUnit passed: 3 of 3 suite files ran.' 'every suite ran once'
        $calls = @(Get-FakeCalls -Context $Context)
        Assert-Equal 1 @($calls | Where-Object { $_ -like 'dotnet build *' }).Count 'one solution build'
        Assert-Equal 1 @($calls | Where-Object { $_ -like '* --import *' }).Count 'one project import'
        $records = @(Get-FakeProcessRecords -Context $Context)
        Assert-Equal 1 $records.Count 'the build recorded the worker it left running'
        $worker = Get-Process -Id $records[0].Id -ErrorAction SilentlyContinue
        $workerAlive = $null -ne $worker -and $worker.ProcessName -eq $records[0].Name
        Assert-That $workerAlive 'the worker outlived the run, so the import started while it was still running'
    }
    finally {
        Stop-FakeProcesses -Context $Context
    }
    Assert-ProcessesGone -Context $Context
}

function Test-BuildFails {
    param($Context)

    Set-FakeBuildPlan -Context $Context -Lines @('exit 3')
    Set-FakePlan -Context $Context -Index 0 -Lines @('exit 0')
    $result = Invoke-Runner -Context $Context
    Assert-ExitCode -Result $result -Expected 1 -Message 'a failed build fails the run'
    $buildError = 'RUNNER-ERROR: Godot test solution build failed with exit code 3: FAKE: build failed'
    Assert-Contains $result.Stderr $buildError 'the build failure names its exit code and the tail of its stderr'
    $calls = @(Get-FakeCalls -Context $Context)
    Assert-Equal 1 @($calls | Where-Object { $_ -like 'dotnet build *' }).Count 'one solution build'
    Assert-Equal 0 @($calls | Where-Object { $_ -like '* --import *' }).Count 'no project import after a failed build'
    Assert-Equal 0 @(Get-RunnerTestCalls -Context $Context).Count 'no test run after a failed build'
}

$ImportFirstLine = 'Godot import runs: no earlier import in this checkout.'
$ImportChangedLine = 'Godot import runs: files it reads changed since the last import.'
$ImportSkippedLine = 'Godot import skipped: no file it reads changed since the last import (-ForceImport imports).'

function Get-ImportCallCount {
    param($Context)

    return @(Get-FakeCalls -Context $Context | Where-Object { $_ -like '* --import *' }).Count
}

function Get-ImportStampPath {
    param($Context)

    return Join-Path $Context.Repo '.godot\gdunit-import.stamp'
}

# One passing run of the Beta suite. Each Godot test run replays the next plan file, so the caller names its index.
function Invoke-PassingRun {
    param($Context, [int]$Index, [switch]$ForceImport)

    Set-FakePlan -Context $Context -Index $Index -Lines @(
        (Get-SuiteStart $SuiteBeta), (Get-SuiteStats -Cases 1), ('report r' + $Index + ' 0 0'), 'exit 0')
    $result = Invoke-Runner -Context $Context -Suite @($SuiteBeta) -ForceImport:$ForceImport
    Assert-ExitCode -Result $result -Expected 0 -Message "passing run $Index exits 0"
    return $result
}

# The first run in a checkout imports and leaves a stamp, a run with nothing changed skips the import, -ForceImport
# imports anyway, and a checkout whose imported files are gone imports again.
function Test-ImportStamp {
    param($Context)

    $first = Invoke-PassingRun -Context $Context -Index 0
    Assert-Line $first.Stdout $ImportFirstLine 'a checkout without a stamp imports'
    $stamped = Test-Path -LiteralPath (Get-ImportStampPath $Context) -PathType Leaf
    Assert-That $stamped 'a successful import leaves a stamp'
    $second = Invoke-PassingRun -Context $Context -Index 1
    Assert-Line $second.Stdout $ImportSkippedLine 'a run with nothing changed skips the import'
    Assert-Equal 1 (Get-ImportCallCount $Context) 'the skipped run starts no import'
    $forced = Invoke-PassingRun -Context $Context -Index 2 -ForceImport
    Assert-Line $forced.Stdout 'Godot import runs: -ForceImport.' '-ForceImport says why it imports'
    Assert-Equal 2 (Get-ImportCallCount $Context) '-ForceImport imports despite a matching stamp'
    # The fake import leaves the folder empty, so this removes one empty folder.
    Remove-Item -LiteralPath (Join-Path $Context.Repo '.godot\imported') -Force
    $wiped = Invoke-PassingRun -Context $Context -Index 3
    Assert-Line $wiped.Stdout $ImportFirstLine 'a checkout whose imported files are gone imports again'
    Assert-Equal 3 (Get-ImportCallCount $Context) 'the run after the wipe imports'
}

# Only a change the import reads imports again: a script's code does not, a new script does, the .uid sidecar that
# import adds is not a change after it, and a changed resource imports.
function Test-ImportInputs {
    param($Context)

    $resource = Join-Path $Context.Repo 'Data\Item.tres'
    Write-FileText -Path $resource -Text '[gd_resource format=3]'
    $null = Invoke-PassingRun -Context $Context -Index 0
    Add-Content -LiteralPath (Join-Path $Context.Repo 'Tests\Beta\BetaTest.cs') -Value '// edited'
    $edited = Invoke-PassingRun -Context $Context -Index 1
    Assert-Line $edited.Stdout $ImportSkippedLine 'editing the code of a script skips the import'
    Write-FileText -Path (Join-Path $Context.Repo 'Scripts\NewThing.cs') -Text 'internal sealed class NewThing { }'
    Set-FakeImportPlan -Context $Context -Lines @('create Scripts\NewThing.cs.uid')
    $added = Invoke-PassingRun -Context $Context -Index 2
    Assert-Line $added.Stdout $ImportChangedLine 'a new script imports'
    $sidecar = Test-Path -LiteralPath (Join-Path $Context.Repo 'Scripts\NewThing.cs.uid') -PathType Leaf
    Assert-That $sidecar 'the fake import added the sidecar'
    Set-FakeImportPlan -Context $Context
    $afterSidecar = Invoke-PassingRun -Context $Context -Index 3
    Assert-Line $afterSidecar.Stdout $ImportSkippedLine 'the sidecar the import added is not a change'
    Write-FileText -Path $resource -Text '[gd_resource format=3 uid="uid://changed"]'
    $changed = Invoke-PassingRun -Context $Context -Index 4
    Assert-Line $changed.Stdout $ImportChangedLine 'a changed resource imports'
    $message = 'imports ran for the first run, the new script and the changed resource'
    Assert-Equal 3 (Get-ImportCallCount $Context) $message
}

# A failed import fails the run before any test and leaves no stamp, even under -ForceImport with a stamp that matched,
# so the next run imports again.
function Test-ImportFails {
    param($Context)

    $null = Invoke-PassingRun -Context $Context -Index 0
    Set-FakeImportPlan -Context $Context -Lines @('exit 3')
    $result = Invoke-Runner -Context $Context -Suite @($SuiteBeta) -ForceImport
    Assert-ExitCode -Result $result -Expected 1 -Message 'a failed import fails the run'
    $importError = 'RUNNER-ERROR: Godot project import failed with exit code 3: FAKE: import failed'
    Assert-Contains $result.Stderr $importError 'the import failure names its exit code and the tail of its stderr'
    Assert-Equal 1 @(Get-RunnerTestCalls -Context $Context).Count 'no test run after a failed import'
    $stampGone = -not (Test-Path -LiteralPath (Get-ImportStampPath $Context))
    Assert-That $stampGone 'a failed import leaves no stamp'
    Set-FakeImportPlan -Context $Context
    $retry = Invoke-PassingRun -Context $Context -Index 1
    Assert-Line $retry.Stdout $ImportFirstLine 'the run after a failed import imports again'
}

function Write-DailyFixtures {
    param($Context)

    Write-FileText -Path (Join-Path $Context.Repo 'Tests\Gamma\GammaTest.cs') -Text $DailySuite
    Write-FileText -Path (Join-Path $Context.Repo 'Tests\Delta\DeltaTest.cs') -Text $CommentedDailySuite
}

function Test-SkipDaily {
    param($Context)

    Write-DailyFixtures -Context $Context
    Set-FakePlan -Context $Context -Index 0 -Lines @(
        (Get-SuiteStart $SuiteAlpha), (Get-SuiteStats),
        (Get-SuiteStart $SuiteAlphaTwo), (Get-SuiteStats),
        (Get-SuiteStart $SuiteBeta), (Get-SuiteStats),
        (Get-SuiteStart $SuiteDelta), (Get-SuiteStats),
        'report r1 0 0',
        'exit 0')
    $result = Invoke-Runner -Context $Context -SkipDaily
    Assert-ExitCode -Result $result -Expected 0 -Message 'a -SkipDaily run exits 0'
    Assert-Line $result.Stdout ('Skipping 1 Daily suite(s): ' + $SuiteGamma) 'the run names the Daily suite it skips'
    Assert-Line $result.Stdout 'GdUnit passed: 4 of 4 suite files ran.' 'every suite but the Daily one is expected'
    $tests = @(Get-RunnerTestCalls -Context $Context)
    Assert-Equal 1 $tests.Count 'one test invocation'
    Assert-That $tests[0].EndsWith('-a res://Tests -i ' + $SuiteGamma) 'the run starts at the Tests folder and skips the Daily suite'
}

# A resume names its own targets, so it must pass the Daily skip again.
function Test-SkipDailyResumes {
    param($Context)

    Write-DailyFixtures -Context $Context
    Set-FakePlan -Context $Context -Index 0 -Lines @(
        (Get-SuiteStart $SuiteAlpha), (Get-SuiteStats),
        (Get-SuiteStart $SuiteAlphaTwo),
        'stall')
    Set-FakePlan -Context $Context -Index 1 -Lines @(
        (Get-SuiteStart $SuiteAlphaTwo), (Get-SuiteStats),
        (Get-SuiteStart $SuiteBeta), (Get-SuiteStats),
        (Get-SuiteStart $SuiteDelta), (Get-SuiteStats),
        'report r1 0 0',
        'exit 0')
    $result = Invoke-Runner -Context $Context -SkipDaily
    Assert-ExitCode -Result $result -Expected 0 -Message 'a -SkipDaily run that resumes after one stall exits 0'
    Assert-Line $result.Stdout 'GdUnit passed: 4 of 4 suite files ran.' 'the resumed run finishes every non-Daily suite'
    $tests = @(Get-RunnerTestCalls -Context $Context)
    Assert-Equal 2 $tests.Count 'two test invocations'
    Assert-That $tests[1].EndsWith('-i ' + $SuiteGamma) 'the resume skips the Daily suite too'
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
    $stallWarning = 'WARNING: GdUnit stalled in ' + $SuiteAlphaTwo + ' (no output for 3 s). Last result: ' +
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
    $marker = @(Get-InterruptionMarkerLines -Context $Context)
    $stallLine = 'stall in ' + $SuiteAlphaTwo + ' (logs: godot.stdout.log, godot.stderr.log)'
    Assert-Equal 1 $marker.Count 'the stall writes one interruption marker line'
    Assert-Equal $stallLine $marker[0] 'the marker names the stalled suite and its logs'
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
    $marker = @(Get-InterruptionMarkerLines -Context $Context)
    $crashLine = 'crash 0xC000001D in ' + $SuiteAlphaTwo + ' (logs: godot.stdout.log, godot.stderr.log)'
    Assert-Equal 1 $marker.Count 'the crash writes one interruption marker line'
    Assert-Equal $crashLine $marker[0] 'the marker names the crashed suite, its exit code in hex and its logs'
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
    $marker = @(Get-InterruptionMarkerLines -Context $Context)
    Assert-Equal 2 $marker.Count 'each interruption appends its own marker line'
    $stallLine = 'stall in ' + $SuiteAlphaTwo + ' (logs: godot.stdout.log, godot.stderr.log)'
    Assert-Equal $stallLine $marker[0] 'the first marker line is the stall of the first attempt'
    $crashLine = 'crash 0xC000001D in ' + $SuiteBeta + ' (logs: godot.resume1.stdout.log, godot.resume1.stderr.log)'
    Assert-Equal $crashLine $marker[1] 'the second marker line is the crash of the first resume'
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

    # Exit 105 is gdUnit's script-errors-at-discovery code. Invoke-ExitScenario asserts one Godot run, so no resume.
    $result = Invoke-ExitScenario -Context $Context -Name 'Exit105' -Lines @('exit 105')
    Assert-ExitCode -Result $result -Expected 1 -Message 'exit 105 fails the run'
    Assert-Contains $result.Stderr 'detected script errors while discovering the test suites (exit code 105)' 'exit 105 names the script errors'
    Assert-That (-not ((Get-FlatRunnerOutput $result) -match 'crash 0x')) 'exit 105 is not treated as a crash'

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
    Assert-Line $result.Stdout 'FAILED fake > fake: fake failure' 'a failing case is named with its message'
    Assert-Line $result.Stdout 'FAILED fake > fake: fake error' 'an erroring case is named with its message'
    Assert-Line $result.Stdout "Suite result: $SuiteAlpha cases=1 errors=1 failures=0" 'a suite finished before a stall is still reported'
}

function Test-GateAndSummary {
    param($Context)

    # The gate line is printed by the stalled attempt and again by the resume; it is printed once. The summary spans
    # both attempts: the stalled suite counts only when the resume finishes it.
    Set-FakePlan -Context $Context -Index 0 -Lines @(
        (Get-SuiteStart $SuiteAlpha), (Get-SuiteStats -Cases 2),
        'print dependency gate: gated: up_edges=7',
        (Get-SuiteStart $SuiteAlphaTwo),
        'stall')
    Set-FakePlan -Context $Context -Index 1 -Lines @(
        (Get-SuiteStart $SuiteAlphaTwo), (Get-SuiteStats -Cases 3),
        (Get-SuiteStart $SuiteBeta), (Get-SuiteStats -Cases 4),
        'print dependency gate: gated: up_edges=7',
        'report r1 0 0',
        'exit 0')
    $result = Invoke-Runner -Context $Context
    Assert-ExitCode -Result $result -Expected 0 -Message 'a resumed run that finishes green passes'
    Assert-Line $result.Stdout 'dependency gate: gated: up_edges=7' 'the gate line is printed'
    Assert-Equal 1 @($result.Stdout -split "`r?`n" | Where-Object { $_.Trim() -eq 'dependency gate: gated: up_edges=7' }).Count 'one gate line'
    Assert-Line $result.Stdout 'Run summary: suites=3 cases=9 errors=0 failures=0 attempts=2' ('the summary spans the ' +
        'stalled and the resumed attempt')
}

# The runner's suite-detection patterns must match the ones in TestSuiteNamingTest.cs, the C# side that gdUnit's own
# self-check uses. The runner is parsed, not run, so this compares the shipped text.
$LockstepPatterns = @('SuiteAttributePattern', 'CodeNoisePattern', 'DailyCategoryPattern')

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
        if ($LockstepPatterns -notcontains $name) {
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
    foreach ($name in $LockstepPatterns) {
        Assert-That $runnerValues.ContainsKey($name) "the runner declares $name"
        $match = [regex]::Match($source, 'internal const string ' + $name + '\s*=\s*@"((?:[^"]|"")*)";')
        Assert-That $match.Success "TestSuiteNamingTest.cs declares $name as a verbatim const"
        $csharpValue = $match.Groups[1].Value.Replace('""', '"')
        Assert-Equal $csharpValue $runnerValues[$name] "$name is identical in the runner and TestSuiteNamingTest.cs"
    }
}

# Every check in run order. -Only must name one of them (letter case does not matter) and then runs alone.
$AllChecks = @(
    'CleanRun', 'BuildLeavesWorkerRunning', 'BuildFails', 'ImportStamp', 'ImportInputs', 'ImportFails',
    'SkipDaily', 'SkipDailyResumes', 'StallResumes',
    'RepeatedStallFails',
    'CrashNoReport', 'CrashAfterEarlierReportResumes', 'RepeatedCrashFails', 'StallThenCrashFails',
    'CrashResumeNoProgressFails', 'InterruptionBudget', 'GdUnitExitCodes', 'ReportAggregation', 'GateAndSummary',
    'PatternLockstep', 'SingleSuite', 'OldReportFoldersPruned', 'CommaSuiteList', 'InvalidSuiteParameters',
    'SingleSuiteStall')
if ([string]::IsNullOrWhiteSpace($Only)) {
    $checks = $AllChecks
}
else {
    # The canonical spelling comes from $AllChecks, so -Only skipdaily runs SkipDaily.
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
