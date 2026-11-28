# Testing: the Runner, CI and Test Helpers

Read this when you change `Scripts/ci/`, a CI workflow, the test helpers or how tests wait, or drive the rendered game.
Moved out of CLAUDE.md, which keeps only the rules most tasks need. Notes about this area belong here.

## Red Daily Runs

A red daily run widens pull-request CI. While the latest finished daily run on `master` is not a success (failed,
cancelled or timed out), every pull request runs every suite, Daily ones included, and passes when they all pass on its
own runner, so a failure that does not happen again there (a flake) does not fail it. That is not a block: a red daily
run still needs its own fix. The wider runs end with the next green daily run; after a flake, `gh run rerun <run-id>
--failed` ends them sooner. `-SkipDaily` leaves a Daily file out as a whole (by name, through gdUnit's `-i`), so the tag
must sit on the suite class: `TestSuiteNamingTest` fails when it sits on a member.

## Test Helpers

**FastClock** (`Tests/TestUtilities/Helpers/FastClock.cs`). A scene test that waits on game time (a customer's walk, a
card settling) wraps the wait in `using (FastClock.Start())`. It raises `Engine.PhysicsTicksPerSecond`,
`Engine.TimeScale` and `Engine.MaxPhysicsStepsPerFrame` by the same factor (16 by default), so game time runs sixteen
times faster while every physics step stays 1/60 s of game time (`FastClockTest` pins that). A process frame then spans
about 0.11 s of game time, so a test that checks an animation stage by stage passes a smaller factor
(`CardContainerTest` uses 4). Do not pass `--fixed-fps` to
Godot instead: it drops the frame sleep that a headless Godot otherwise takes (its low-processor delay, about 6.9 ms, so
about 145 frames a second), and gdUnit then lost suite starts on every run.

**Waiting on frames.** A test that only waits on frames, for a count or for a condition within a frame budget, calls
`BoundedWait` (`Tests/TestUtilities/Helpers/BoundedWait.cs`) and writes no loop of its own for it. `Until` and `Expect`
poll a condition against a wall-clock bound, `PhysicsFrames` waits a count of physics frames against one, and
`ProcessFrames` waits a count of process frames. `SyncPhysicsFrames`, `ProcessFramesUntil` and `SyncPhysicsFramesUntil`
count `physics_frame` or `process_frame` signals, the tick of gdUnit's `ISceneRunner.SyncPhysicsFrame` and
`SyncProcessFrame`, and carry no wall-clock bound; the two `Until` forms stop after a frame budget. A suite may bind a
count it needs to one name (`Settle()` is `SyncPhysicsFrames(SettleFrames)`). Three kinds of wait stay outside it: a
single `await ISceneRunner.SyncPhysicsFrame;` or `SyncProcessFrame` in a test body, a loop that does other work on every
frame (`PlayerWalker.WalkTo` steers the body and tracks its height), and a loop bounded by its own wall-clock budget or
whose counter a failure message reads.

## gdUnit Report Folders

The vendored gdUnit4 is 6.2.3, which cleans a relative `-rd` report folder correctly: `GdUnitFileAccess.delete_directory`
resolves the folder to its absolute path before it deletes, so a direct `GdUnitCmdTool.gd` run with a relative `-rd` (as
`Run-GdUnit.ps1` uses with `-rd reports/ci`) logs no `ERROR: Delete` lines. Without `-rc`, gdUnit keeps the newest 20
`report_<n>` folders in its report folder and removes the older ones. `Run-GdUnit.ps1` and the one-liners below pass no
`-rc`. One run through the runner writes at most `-MaxResumes` + 1 report folders (11 at the limit of 10), so the cleanup
never removes a report of the current run. A run without `-rd` uses gdUnit's absolute default report path.

## Running Godot Without PowerShell

**Without PowerShell.** Start Godot from Bash through node, so no console window opens:

```bash
# One suite, the same arguments as Run-GdUnit.ps1 uses for a single suite (reports/<dir> is relative; gdUnit keeps its newest 20 report folders, see above)
node -e "const r=require('child_process').spawnSync('<godot console exe>',['--headless','--path','.','-s','res://addons/gdUnit4/bin/GdUnitCmdTool.gd','-c','--ignoreHeadlessMode','-rd','reports/<dir>','-a','res://Tests/<path>Test.cs'],{stdio:'inherit',windowsHide:true});process.exit(r.status??1)"

# The .uid import (writes only the sidecars, see Godot .uid Sidecars in CLAUDE.md)
node -e "const r=require('child_process').spawnSync('<godot console exe>',['--headless','--path','.','--import'],{stdio:'inherit',windowsHide:true});process.exit(r.status??1)"
```

These give one suite and the .uid import when powershell.exe cannot start. The one-liners exit with Godot's own status,
and non-zero (1) when Godot cannot start, so a missing exe fails the command. They have no watchdog, no resume and no suite
aggregation, so Run-GdUnit.ps1 stays the supported path for a full or -SkipDaily run.

## CI

CI scope: pull requests and pushes to `master` run the unit tier and the pull-request tier (`-SkipDaily`). Every suite
runs on the daily schedule, on a pull request while the latest daily run is red (see Test tiers in CLAUDE.md), and on a manual
dispatch with `full=true`. A green pull request therefore does not prove the Daily suites; dispatch `full=true` on the
branch (or tag) whose head needs them, e.g. `gh workflow run ci.yml --ref <branch> -f full=true`.

The Godot download in CI goes through `Scripts/ci/Install-Godot.ps1`. It retries a failed download or unzip (an HTTP 5xx such
as the 504 that failed the 2026-11-08 weekly run, a connection error, a truncated archive) for 3 attempts in all, waiting 15 s
and then 30 s, and writes `Godot download attempt <n> of 3 failed: <error>` as a warning for each failure, so the retry shows in
the run log. An archive that unzips without the executable fails at once. `Scripts/ci/Test-InstallGodot.ps1` proves the loop
against a loopback HTTP server (no Godot and no network), and the `runner-fixture` job runs it under pwsh. A daily run
that still fails for a reason like that makes pull requests run every suite until `gh run rerun <run-id> --failed`
turns it green.

## The gdUnit Runner

Tests use the gdUnit4 framework. The suites in `Tests/` require the Godot runtime (`[RequireGodotRuntime]` attribute);
the suites in `UnitTests/` run without it. Both mirror the `Scripts/` structure.

gdUnit finds a C# suite by its file name, so a `[TestSuite]` class must be named after its file (`TestSuiteNamingTest`
enforces this). `Run-GdUnit.ps1` fails when a `[TestSuite]` file never runs. When Godot prints nothing for
`-StallSeconds` (180 by default) it kills Godot, names the suite it stalled in, and reruns only the unfinished suites
(at most `-MaxResumes` times). A Godot crash gets the same treatment: an exit code gdUnit does not return (anything but
0, 100, 101, 103, 104 or 105) from an attempt that wrote no results report is named with its suite, logged in hex (for
example `0xC000001D`), and the unfinished suites are resumed. Stalls and crashes share the `-MaxResumes` budget; a
second interruption in the same suite, or a resume that finishes no suite, fails the run. Exits 100, 101, 103, 104 and 105
keep their own handling and never resume (105 is gdUnit 6.2.3's script errors found during discovery: no report is
written, and the run fails naming the script errors), and an abnormal exit after a green report still fails. Reports and logs go to
`reports/ci`; before the first Godot attempt, stale generated attempt logs directly in that directory matching
`godot.stdout.log`, `godot.stderr.log`, `godot.resume*.stdout.log`, or `godot.resume*.stderr.log` are removed, while
report folders and other files survive. Each attempt then has one log pair (`godot.stdout.log`, then
`godot.resume<n>.stdout.log`). Every `-ReportDirectory` name leaves its own folder directly under `reports/`, so after
its argument checks the runner keeps the 10 newest of those folders by write time, the current run's included, and
removes the rest (`Removed old report folder: reports/<name>` for each; a folder it cannot remove is a warning, not a
failed run). It never touches a file directly under `reports/` (`.gdignore`), `reports/e2e`, or a junction or link. Every stall or crash also appends a line such as `crash 0xC000001D in <suite> (logs: ...)`
to `reports/ci/interruptions.txt` (a marker left by an earlier run is removed at start). CI uploads `reports/ci`
as the `gdunit-reports` artifact on failure, on the
daily and `full=true` runs, on a pull request that runs every suite, and whenever that marker exists, so a crash inside a run that resumed to green keeps its
logs. `Test-RunGdUnit.ps1` runs the real runner against a fake Godot and covers its build wait (a build that leaves a
worker running), build failure, watchdog, resume, repeated-stall, crash, repeated-crash, exit-code, interruption-marker,
report-aggregation and old-report-folder paths, the Daily skip (`-SkipDaily`, on the first attempt and on a resume), and the import skip
(an unchanged project, `-ForceImport`, a missing `.godot/imported`, an edited `.cs`, a new script, a changed resource
and a failed import). It also checks that the suite-detection and Daily patterns in `Run-GdUnit.ps1` match the ones in
`TestSuiteNamingTest.cs`.

**The import skip.** Before the tests, `Run-GdUnit.ps1` runs Godot's editor import (`--headless --editor
--recovery-mode --import`), which cost 16 to 24 s of every local run even when nothing had changed. It now runs only
when a file the import reads changed. The runner fingerprints the Godot executable and every project file Godot's own
scan visits (it skips names starting with `.` and folders holding a `.gdignore`) by path, size and write time; a `.cs`
or `.md` file counts by its path only, because the import reads no C# or Markdown (editing a `.cs` body or removing a
`[GlobalClass]` changed no imported output). When the fingerprint matches `.godot/gdunit-import.stamp` and
`.godot/imported` exists, the log says `Godot import skipped`; otherwise the import runs and the log says why. A new
`.cs` file still triggers the import, and `-ForceImport` always runs it. A failed import leaves no stamp. CI starts with
no `.godot` folder, so it always imports and the saving is local: one small suite through the runner went from a median
25.7 s to 9.2 s. A test that rewrites a project file with the same bytes still changes its write time and costs the next
run an import, so a test writes its files under `user://`, never into the project (`TileEditorWiringSmokeTest` compiles
its atlas there and fails when the committed atlas in `Data/CompiledAtlas` is rewritten; a `-SkipDaily` run leaves
every project file untouched). `reports/.gdignore` is committed (with `git add -f`, since
`.gitignore` ignores `reports`), so neither Godot's scan nor the fingerprint reads the reports.

The runner prints its results to the job log, so no artifact download is needed for them: one `Suite result: <suite> cases=<n> errors=<e> failures=<f>` line per finished suite (also for suites finished in an attempt that later stalled or crashed), a `FAILED <suite> > <case>: <message>` line for each failing case before it throws, every `dependency gate:` line from all attempt logs, and a `Run summary:` line. `-Suite` takes res:// paths of `[TestSuite]` files and cannot be combined with `-SkipDaily`. `-ReportDirectory` is relative and must lie below `reports/` (default `reports\ci`, which CI uses). A suite that stalls twice fails the run and is named.

## Driving the Rendered Game (e2e)

`node tools/e2e/e2e.mjs` runs the real, rendering game on a hidden Win32 desktop (no window ever reaches the user's
screen) and drives it over a local socket: keys, mouse, `walk_to`, `look_at`, `use`, scene inspection and
screenshots into `reports/e2e/<session>/shots/`. Use it to look at a change in-game; see `tools/e2e/README.md`.
Starting a session keeps 5 session folders, the started one included, and removes the older stopped ones with their
save data and screenshots; it never removes a running session or the launcher in `reports/e2e/bin`
(`node tools/e2e/e2e.test.mjs`, run by the `runner-fixture` CI job, checks that). The
in-game side is the `Automation` autoload (`Scripts/Features/Automation`), dormant unless a debug build gets
`-- --automation-port=<n>`. In that mode `CursorOwner` never captures the mouse, because a captured mouse clips the
user's real cursor even from a hidden desktop.
