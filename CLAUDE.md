# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project Overview

CardCleaner is a Godot 4.4+ game built with C# (.NET 8.0) featuring a card-based deckbuilder with procedural world generation. Cards have an 8-dimensional "signature" system representing elemental properties that influence gameplay, combat, and loot generation.

## Build & Test Commands

```bash
# Build the game project
dotnet build

# Unit tier, the development loop: plain .NET tests with no Godot process (budget 10 s, not counting the build)
dotnet test UnitTests

# One unit test class
dotnet test UnitTests --filter "FullyQualifiedName~CardSignatureTest"

# Pull-request tier: every gdUnit suite except the Daily ones, as CI runs on pull requests and pushes to master.
# Run it while working on merging a pull request, not during development (Windows PowerShell 5.1 or PowerShell 7)
powershell -NoProfile -File Scripts/ci/Run-GdUnit.ps1 -GodotBinary <path to Godot_v4.7.1-stable_mono_win64.exe> -SkipDaily

# Every gdUnit suite, Daily ones included, as the daily CI run does
powershell -NoProfile -File Scripts/ci/Run-GdUnit.ps1 -GodotBinary <path to Godot exe>

# One suite through the same runner (watchdog and resume included), reports and logs into reports/<name>
powershell -NoProfile -File Scripts/ci/Run-GdUnit.ps1 -GodotBinary <path to Godot exe> -Suite res://Tests/Architecture/DependencyGateTest.cs -ReportDirectory reports/single

# Runner fixture: runs Run-GdUnit.ps1 against a fake Godot, so no Godot is needed (CI runs it too)
powershell -NoProfile -ExecutionPolicy Bypass -File Scripts/ci/Test-RunGdUnit.ps1

# Godot download retry fixture: runs Install-Godot.ps1 against a loopback HTTP server, so no Godot or network is needed (CI runs it too)
powershell -NoProfile -ExecutionPolicy Bypass -File Scripts/ci/Test-InstallGodot.ps1
```

**Test tiers.** Each tier has a time budget, measured without the build:

- **Unit** (`UnitTests/`, budget 10 s): plain .NET suites run by `dotnet test UnitTests`, with no Godot process. Run
  only this tier during development. A suite belongs here only when neither it nor the code it calls needs the engine:
  a Godot native call (a node, a resource, `GD`, `ProjectSettings`) crashes the plain test host. `UnitTests/` has a
  `.gdignore`, so its files get no `.uid` sidecars, and `CardCleaner.csproj` leaves the folder out of the game.
- **Pull request** (`Tests/` without the Daily suites, budget 5 minutes): the gdUnit suites inside Godot, run by
  `Run-GdUnit.ps1 -SkipDaily`. Run it while working on merging a pull request; CI runs it on every pull request and
  push to `master`.
- **Daily**: a suite whose class carries `[TestCategory("Daily")]`. Benchmarks belong here, and so does a check that
  cannot be made fast without testing less. A slow suite first gets faster while checking the same thing (a smaller
  fixture, `FastClock` below, one shared expensive setup); only when that fails does its class get the tag. CI runs
  every suite, Daily ones included, at 03:00 UTC each day on `master`.

A red daily run widens pull-request CI. While the latest finished daily run on `master` is not a success (failed,
cancelled or timed out), every pull request runs every suite, Daily ones included, and passes when they all pass on its
own runner, so a failure that does not happen again there (a flake) does not fail it. That is not a block: a red daily
run still needs its own fix. The wider runs end with the next green daily run; after a flake, `gh run rerun <run-id>
--failed` ends them sooner. `-SkipDaily` leaves a Daily file out as a whole (by name, through gdUnit's `-i`), so the tag
must sit on the suite class: `TestSuiteNamingTest` fails when it sits on a member.

**FastClock** (`Tests/TestUtilities/Helpers/FastClock.cs`). A scene test that waits on game time (a customer's walk, a
card settling) wraps the wait in `using (FastClock.Start())`. It raises `Engine.PhysicsTicksPerSecond`,
`Engine.TimeScale` and `Engine.MaxPhysicsStepsPerFrame` by the same factor (16 by default), so game time runs sixteen
times faster while every physics step stays 1/60 s of game time (`FastClockTest` pins that). A process frame then spans
about 0.11 s of game time, so a test that checks an animation stage by stage passes a smaller factor
(`CardContainerTest` uses 4). Do not pass `--fixed-fps` to
Godot instead: it drops the frame sleep that a headless Godot otherwise takes (its low-processor delay, about 6.9 ms, so
about 145 frames a second), and gdUnit then lost suite starts on every run.

The vendored gdUnit4 is 6.2.3, which cleans a relative `-rd` report folder correctly: `GdUnitFileAccess.delete_directory`
resolves the folder to its absolute path before it deletes, so a direct `GdUnitCmdTool.gd` run with a relative `-rd` (as
`Run-GdUnit.ps1` uses with `-rd reports/ci`) logs no `ERROR: Delete` lines. Without `-rc`, gdUnit keeps the newest 20
report folders and removes the older ones. `Run-GdUnit.ps1` and the one-liners below still pass `-rc 100000`, so every
report folder of a run is kept; that is a choice, not a requirement of the cleanup. A run without `-rd` uses gdUnit's
absolute default report path.

**Project patch in the vendored addon (issue 448).** gdUnit4.api runs each C# suite on a thread-pool thread, and the
suite first awaits Godot's `process_frame` through a `SignalAwaiter`, which is not thread-safe. When the frame lands
between the awaiter's completion check and its continuation registration, the suite never starts and the run stalls
between suites (gdUnit4Net issue 397, closed upstream as a Godot bug; the package keeps that code internal).
`addons/gdUnit4/src/dotnet/GdUnit4CSharpApi.cs` therefore runs the bridge through `SuiteStartWatchdog` (same folder): an
attempt that sends no event within 30 s is cancelled, its late events are dropped, and the suite starts again, three
attempts in all. Each abandoned attempt prints `gdUnit4 suite start lost:` to stderr. gdUnit sends a suite's first event
after its `[Before]` hook, so no `[Before]` hook may await. `SuiteStartGuardTest` fails when an addon update drops the
patch or a `[Before]` hook returns a task; reapply both files after updating the addon.

**Project patch in the vendored addon (discovery cache).** gdUnit discovers C# tests one script at a time, and the
package's per-script discovery reads the whole test assembly and its symbols with Mono.Cecil on every call, about 33 ms
each: 17 s for the 518 scripts under `res://Tests` at the start of every run. Outside the editor, `GdUnit4CSharpApi.cs`
therefore asks `SuiteDiscoveryCache` (same folder) first. It reads the assembly once through the package's own
whole-assembly discovery (the internal `TestCaseDiscoverer.Discover`, reached by reflection) and files each suite's tests
under its script, so a script that is no `[TestSuite]` class gets no tests without a read. When the cache cannot be built
it prints `gdUnit4 discovery cache unavailable` to stderr and every script goes to the package as before.
`SuiteDiscoveryGuardTest` fails when the cache is unavailable or differs from the package on a sample of scripts, and
`SuiteDiscoveryEquivalenceTest` (Daily) compares the two for every script; reapply `SuiteDiscoveryCache.cs` and the call
in `GdUnit4CSharpApi.DiscoverTests` after updating the addon or the `gdUnit4.api` package.

**Project patch in the vendored asset_manager.** The headless editor import (`Run-GdUnit.ps1`, CI) starts every enabled
editor plugin even with `--recovery-mode`, and Godot compiles everything a plugin script preloads. So
`addons/asset_manager/plugin.gd` returns at once in a headless editor and loads its main screen and context menu at run
time, which took about 1.1 s off each import. `AssetManagerLazyOpenTest` fails when an addon update drops the patch.

**Without PowerShell.** Start Godot from Bash through node, so no console window opens:

```bash
# One suite, the same arguments as Run-GdUnit.ps1 uses for a single suite (reports/<dir> is relative; -rc 100000 keeps every report folder, see above)
node -e "const r=require('child_process').spawnSync('<godot console exe>',['--headless','--path','.','-s','res://addons/gdUnit4/bin/GdUnitCmdTool.gd','-c','--ignoreHeadlessMode','-rc','100000','-rd','reports/<dir>','-a','res://Tests/<path>Test.cs'],{stdio:'inherit',windowsHide:true});process.exit(r.status??1)"

# The .uid import (writes only the sidecars, see Godot .uid Sidecars)
node -e "const r=require('child_process').spawnSync('<godot console exe>',['--headless','--path','.','--import'],{stdio:'inherit',windowsHide:true});process.exit(r.status??1)"
```

These give one suite and the .uid import when powershell.exe cannot start. The one-liners exit with Godot's own status,
and non-zero (1) when Godot cannot start, so a missing exe fails the command. They have no watchdog, no resume and no suite
aggregation, so Run-GdUnit.ps1 stays the supported path for a full or -SkipDaily run.

**Physics runs on the main thread** (`physics/3d/run_on_separate_thread=false`, pinned by `PhysicsThreadSettingTest`).
With the separate thread on, Godot 4.7.1 froze the whole process, in tests and in the game alike. A main-thread physics
call during the physics frame (here a `CollisionObject3D` notification) found the physics command queue mid-flush,
queued a sync and waited, while the physics thread finished its flush and refused to flush again until the frame ended.
Native stacks of two captured freezes showed exactly that (godotengine/godot issue 123654). The upstream fix is pull
request 124990, merged to master on 2026-10-31 and marked for a 4.7 backport. Turn the setting back on only on an
engine version that contains it.

**Navigation maps build synchronously** (`navigation/world/map_use_async_iterations=false`, pinned by
`NavigationMapSyncSettingTest`). With async map builds, a build that included a rebaked region could finish in the same
navigation sync that applied the region, which from outside looks the same as a stale build finishing there, so
`ShopNavigationRig.WaitForBakeSync` could wait for a map change that never came. Built synchronously, the sync that
applies a region iteration also builds the map from it.

**Cards rest on top of the floor** (`physics/jolt_physics_3d/simulation/penetration_slop=0.001`, checked by
`CardFloorRestTest` on the real card scene). Jolt lets resting bodies sink into each other by the slop, 0.02 m by
default, and a card is 5 mm thick, so the default hid cards under the shop floor. The card's `CardSleepEnforcer` also
freezes a card only after it has stayed below its thresholds for `RestSeconds` (0.5 s, the rest time Jolt waits before it
lets a body sleep): freezing on the first slow frame froze a landing card inside the floor, before Jolt had pushed it out. It also freezes
a card as soon as Jolt puts it to sleep (`SleepingStateChanged`). A sleeping body gets no more `IntegrateForces` calls,
so when Jolt's own half second ran out first, the enforcer's count never finished and the card lay unfrozen, which
failed `CardFloorRestTest` on some CI runs and not others (`ACardJoltPutsToSleepIsFrozen` pins it).

**C# resources stay loaded** (`PinnedResource`, `Scripts/Core/Resources`). Every C# class that derives from `Resource`
derives from `PinnedResource` instead (`PinnedResourceTest` fails otherwise), which keeps an instance referenced for the
rest of the run once a load has given it a path. Godot 4.7.1 holds only a weak handle to a C#-scripted resource that
nothing but its managed wrapper references. When the collector took that wrapper and a scene or a `GD.Load` took the
same path from the resource cache before the wrapper's finalizer ran, the finalizer stopped a debug build with `FATAL:
Condition "gchandle.is_released()" is true` (exit `0xC000001D`, godotengine/godot issue 83762). The same crash, with the
same finalizer backtrace, stopped `RestockerCarryTest` on full CI runs, since each of its cases loads the shop again.
`ScriptedResourceRevivalTest` parks the finalizer thread so the race would happen on every run. An instance made with
`new` has no path and is collected as usual. The pin checks the path at the end of the frame, so nothing may load on a
worker thread (`ResourceLoader.LoadThreadedRequest`), which `PinnedResourceTest` also checks. A loaded resource is never
unloaded: leaving the shop keeps its catalogs and the card visuals (about 3.6 MB of PNG textures) in memory. Drop the
base class only on an engine version that contains a fix for that issue.

CI scope: pull requests and pushes to `master` run the unit tier and the pull-request tier (`-SkipDaily`). Every suite
runs on the daily schedule, on a pull request while the latest daily run is red (see Test tiers), and on a manual
dispatch with `full=true`. A green pull request therefore does not prove the Daily suites; dispatch `full=true` on the
branch (or tag) whose head needs them, e.g. `gh workflow run ci.yml --ref <branch> -f full=true`.

The Godot download in CI goes through `Scripts/ci/Install-Godot.ps1`. It retries a failed download or unzip (an HTTP 5xx such
as the 504 that failed the 2026-11-08 weekly run, a connection error, a truncated archive) for 3 attempts in all, waiting 15 s
and then 30 s, and writes `Godot download attempt <n> of 3 failed: <error>` as a warning for each failure, so the retry shows in
the run log. An archive that unzips without the executable fails at once. `Scripts/ci/Test-InstallGodot.ps1` proves the loop
against a loopback HTTP server (no Godot and no network), and the `runner-fixture` job runs it under pwsh. A daily run
that still fails for a reason like that makes pull requests run every suite until `gh run rerun <run-id> --failed`
turns it green.

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
`godot.resume<n>.stdout.log`). Every stall or crash also appends a line such as `crash 0xC000001D in <suite> (logs: ...)`
to `reports/ci/interruptions.txt` (a marker left by an earlier run is removed at start). CI uploads `reports/ci`
as the `gdunit-reports` artifact on failure, on the
daily and `full=true` runs, on a pull request that runs every suite, and whenever that marker exists, so a crash inside a run that resumed to green keeps its
logs. `Test-RunGdUnit.ps1` runs the real runner against a fake Godot and covers its build wait (a build that leaves a
worker running), build failure, watchdog, resume, repeated-stall, crash, repeated-crash, exit-code, interruption-marker
and report-aggregation paths, and the Daily skip (`-SkipDaily`, on the first attempt and on a resume). It also checks
that the suite-detection and Daily patterns in `Run-GdUnit.ps1` match the ones in `TestSuiteNamingTest.cs`.

The runner prints its results to the job log, so no artifact download is needed for them: one `Suite result: <suite> cases=<n> errors=<e> failures=<f>` line per finished suite (also for suites finished in an attempt that later stalled or crashed), a `FAILED <suite> > <case>: <message>` line for each failing case before it throws, every `dependency gate:` line from all attempt logs, and a `Run summary:` line. `-Suite` takes res:// paths of `[TestSuite]` files and cannot be combined with `-SkipDaily`. `-ReportDirectory` is relative and must lie below `reports/` (default `reports\ci`, which CI uses). A suite that stalls twice fails the run and is named.

### Driving the Rendered Game (e2e)

`node tools/e2e/e2e.mjs` runs the real, rendering game on a hidden Win32 desktop (no window ever reaches the user's
screen) and drives it over a local socket: keys, mouse, `walk_to`, `look_at`, `use`, scene inspection and
screenshots into `reports/e2e/<session>/shots/`. Use it to look at a change in-game; see `tools/e2e/README.md`. The
in-game side is the `Automation` autoload (`Scripts/Features/Automation`), dormant unless a debug build gets
`-- --automation-port=<n>`. In that mode `CursorOwner` never captures the mouse, because a captured mouse clips the
user's real cursor even from a hidden desktop.

## Code Style

The project uses `.editorconfig` for code style enforcement with Godot-specific adaptations:

- **Naming Conventions**: Interfaces prefixed with `I`, private fields with `_camelCase`, public members use `PascalCase`
- **Nullable Reference Types**: Enabled project-wide at warning level (CS8600-CS8625 series)
- **Modern C# Features**: File-scoped namespaces, pattern matching, primary constructors encouraged
- **Godot Lifecycle Exceptions**:
  - CA1051 disabled (allows public fields for `[Export]` attributes)
  - CA2000/CA1816 relaxed (Godot manages node lifecycle)
  - CA1031 lenient (game code exception handling)
- **Tests/Addons**: Tests have relaxed null checking; addons folder has all analyzers disabled

### [Tool] Attribute Usage

The `[Tool]` attribute causes Godot to execute scripts in the editor. **Only use `[Tool]` on scripts that genuinely need editor functionality:**

**WHEN TO USE `[Tool]`:**
- Resources with `[GlobalClass]` that are edited in the inspector
- Nodes with editor preview features (e.g., `GeneratePreview` button)
- Editor plugins and custom inspectors

**WHEN NOT TO USE `[Tool]`:**
- Runtime-only nodes (interaction, physics, game logic)
- Components that only run during gameplay
- Services and controllers

**The Problem:** `[Tool]` scripts run `_Ready()` in the editor, which populates private fields. Godot then serializes these runtime values to `.tscn` files, causing:
- Private fields like `_rng`, `_originalPosition` appearing in scene files
- Version control noise from random state changes
- Potential deserialization bugs

**Legitimate preview exception:** `ShopArtSlot` is a `[Tool]` so the editor shows the real art. It sets a node property the scene file would otherwise save (the placeholder's `Visible`), so it restores that property on `NotificationEditorPreSave` and re-applies it on `NotificationEditorPostSave`; `ShopArtSlotEditorSaveTest` fails if any scene stores a hidden placeholder. Copy that pattern for any `[Tool]` preview that mutates other nodes.

**The Fix:** Remove `[Tool]` from runtime-only scripts. If `[Tool]` is truly needed, guard `_Ready()`:

```csharp
public override void _Ready()
{
    if (Engine.IsEditorHint()) return;
    // Runtime initialization...
}
```

### Signs and World Text

Text in the world is printed on something, never floating in the air or turned to the camera. `SignBoard`
(`Scripts/Features/Signage`) is a board with its text printed flat on the face: fixed to a wall, hung on two rods
(`Mount` Hanging, `MountLength` up to the ceiling) or stood on a post, one- or two-sided, and as a `Screen` a dark
panel whose text glows (the register display, the OPEN/CLOSED sign). Setting `Text` or `TextColor` repaints both
faces; the other exports rebuild it. It builds its board, faces and mount as internal children, which no scene stores,
so a scene holds only its exports; it is a `[Tool]` so the editor shows it, and it changes no saved node.
`NoFloatingTextSceneTest` fails on a billboard `Label3D` in any scene outside `Scenes/Debug`.

### Godot .uid Sidecars

Godot 4.4+ gives every script and shader a `.uid` sidecar, and scenes reference them by that UID. Sidecars under
`Scripts/`, `Tests/` and `Shaders/` are committed (`.gitignore` negates `*.uid` for those roots), so a fresh clone keeps
the same UIDs; `addons/` sidecars stay ignored. Move or rename a source together with its `.uid` (both in one `git mv`,
or through the editor's FileSystem dock). A headless gdUnit run does not create a new script's `.uid`; run `<godot console exe> --headless --path . --import`, which writes only the sidecar, and commit it with the script. A new folder of scripts or shaders outside those roots needs its own negation
in `.gitignore`. A `.uid` whose source is gone is removed, not ignored: this is an exception to the rule of adding an
ignore entry for a generated file, because no workflow needs a sidecar without its source. `ExtResourceUidTest` fails
when a scene's `ext_resource` UID does not match its target.

### Overriding Nodes Inside an Instanced Scene

An outer scene must not override properties of nodes that belong to an instanced scene (a `[node name="X"
parent="Some/Instance"]` stanza with no `type=` or `instance=`) unless that instance is marked editable (`[editable
path="Some/Instance"]`). Without the flag the scene loads fine, but the editor silently drops those values on the next
save (this deleted the workshop `ReturnSeam` wiring from `ShopScene.tscn` twice). Put the node in the outer scene
instead: add a new instance under the instance's root and set its exports on that node, as the backoffice `Seam` and
`ReturnSeam` do. `SceneEditorRoundTripSceneTest` packs every scene under `Scenes/` the way the editor does and fails
when a stored node value is lost, or when a `[node]` stanza or `[editable]` line of the file's canonical save (the
loaded file saved again unchanged) is missing from the editor-style save (`SceneStanzaCheck`). A dropped override loses
its whole stanza, so this also catches an override that changes no stored property, such as a group-only override. The
test does not compare the contents of inline sub-resources.

### Export Default Preservation (Inspector Reset Button)

The `_PropertyCanRevert()` pattern enables the "reset to default" button in the Godot inspector. Note: this does NOT prevent serialization - it only affects the inspector UI.

```csharp
public partial class MyNode : Node3D
{
    private const float DefaultSpeed = 5.0f;
    [Export] public float Speed = DefaultSpeed;

    public override bool _PropertyCanRevert(StringName property)
    {
        return property.ToString() == nameof(Speed) || base._PropertyCanRevert(property);
    }

    public override Variant _PropertyGetRevert(StringName property)
    {
        return property.ToString() == nameof(Speed)
            ? DefaultSpeed
            : base._PropertyGetRevert(property);
    }
}
```

**When to use**: For exported properties where you want the inspector's reset button to work correctly.

## Architecture

### Dependency Injection

The project uses a custom DI container with no static service state: an instance-owned host and a composition root:

- **ServiceHost** (`Scripts/Core/DependencyInjection/ServiceHost.cs`): a plain class that implements
  `IServiceResolver` and owns the container and the one pending-callback queue (`Container`, `Get<T>()`, `Has<T>()`,
  `Get<T>(callback)`, `ExecutePendingCallbacks()`, `Reset()`). It exists from construction, `Reset()` replaces its
  container and clears its queue but keeps the host, and it names no feature or composition type;
  `DependencyInjectionBoundaryTest` enforces that. It does not touch `ILog.Sink`.
- **CompositionRoot** (`Scripts/Composition/CompositionRoot.cs`): the "Services" autoload. It owns one `ServiceHost`
  (`Host`) and composes it in two scopes. The **app scope** (the plain `[Service]` classes of `CardCleaner.Scripts`,
  registered lazily, then the core services) is composed synchronously in the autoload's `_Ready`, before the main
  scene's `_Ready`, and lives for the whole run: the main menu resolves it directly, and the slot it activates on
  `ActiveSaveSlot` reaches the shop. The **scene scope** (every node in the `service_providers` group) is registered
  in the deferred flush after startup and on every `SceneTree.SceneChanged`, then the pending callbacks run. When the
  current scene leaves the tree (`ChangeSceneToFile` removes it at once), `ServiceHost.LeaveScene` drops its
  registrations, restores app registrations they replaced, forgets callbacks its nodes queued, and makes every
  scanned class built from a scene service (such as `DailyBillBook`) build again in the next scene
  (`ServiceContainer.SealAppScope` / `DropSceneScope`). A scene node that needs a provider-registered service still
  uses `Get<T>(callback)`; a scanned `[Service]` must never stand in for a node a scene provides, since a scene node
  could resolve it before the provider registers (`InstantCheckout` is therefore not scanned). It also sets
  `ILog.Sink` (a static property, because `ILog`'s static helpers have no instance) when it registers `ILog`;
  production never clears it, and only `TestServices.Reset` sets it back to null. Only that autoload composes and injects; any other instance logs an error
  and composes nothing.
- **ILog** (`Scripts/Core/Interfaces/ILog.cs`) names no Godot type: `ILog.Print` / `Warning` / `Error` go to
  `ILog.Sink` (the `LoggingService` the root installs, which prints through `GD`) and fall back to `Console.Out` /
  `Console.Error` while no sink is installed. Domain code logs through `ILog`, never `GD`. The node export check is
  the UI-layer static `NodeExports.ExportCheck` (`Scripts/Core/Utilities`).
- **CoreMath** (`Scripts/Core/Numerics/CoreMath.cs`): engine-free static math for domain code. Each member copies
  the GodotSharp 4.7.1 `Mathf` body of the overload callers bind to, and `CoreMathTest` compares each with
  `Godot.Mathf`. Domain classes call `CoreMath`, not `Mathf`; add a member only together with its first caller (an
  unused public member is a `dead-export` finding). It lives in `Core/Numerics`, not `Core/Math`, so the namespace
  does not shadow `System.Math`.
  A data struct that must stay pure data in the dependency gate (`CardSignature`) uses `System.Math` / `MathF`, not
  `CoreMath`, because a static function class is not data there.
- **ServiceContainer**: singleton registrations, plus constructor injection. `Construct<T>()` and
  `RegisterSingleton<TInterface, TImplementation>()` build a plain class through its public constructor with the most
  parameters, resolving each parameter from the container; a missing one throws `InvalidOperationException` naming
  the class and the service (`Cannot construct Foo: service IBar not registered`).
- **Lazy [Service] scan** (`ServiceClassScan.PlainServiceClassesIn` finds them, `RegisterServiceClasses` registers
  them): a non-abstract `[Service]` class that is not a Godot object is registered under its `[Service]` types (its interfaces when it names none). It counts as registered only once every
  parameter of its constructor is registered, so callbacks waiting for it stay pending until then; it is built once,
  on its first resolve, and shared by all its types. An explicit registration of the same type replaces it. `[Service]`
  nodes are never built by the container; `AutoServiceProvider` registers the live node.
- **Session factories**: `GameSessionService` is an `IDependent` that builds its per-session parts from two stateless
  registered factories, `ISessionFactory` (world and exploration session) and `ICombatSystemFactory` (one fight), found
  by the `[Service]` scan. UI code names the contracts `ISessionWorld`, `IExplorationSession` and `ICombatSystem`, never
  the concrete classes. The gate counts a static factory class as a concrete crossing, so none is used.
- **Tile catalog**: the map generators take an `ITileCatalog` (`Scripts/Features/Deckbuilder/Tiles`), which extends
  `IWfcTileCatalog`. The only concrete implementation is `TileCatalogData`, an immutable snapshot of the tiles in
  registry insertion order and their variation groups. `GridMapGenerator` and `MeshMapGenerator` are `[Service]`
  classes that the scan builds; `MeshMapGenerator`'s constructor that takes the ring count is internal.
  `TileCatalogData` is also a `[Service]`, with a public `TileCatalogData(ITileRegistry)` constructor that the scan
  builds once from the registered registry. `CompositionRoot` builds only the registry (`TileCatalogLoad.Load`) and
  the data it registers by instance. `TileCatalogConsumersTest` fails if a file under
  `Scripts/Features/Deckbuilder/Services` or `Scripts/Features/Worldgen/IrregularMesh` names `TileCatalogData`.
  The compiled atlas is loaded by the stateless
  `TileVisualLoader`, which returns a `TileVisualCatalog` that `TileRegistry` exposes as `Visuals` (UI users read the
  tileset path, atlas flag and `TilesetConfig` there). `AtlasMapping` holds no static cache: `Load` reads its file on
  every call, and `WorldMapComponents` passes the loaded mapping to `TerrainTransitions.Create`. The compiled
  atlas's TileSet (`CompiledAtlasLoader`, one per mapping file) creates its 25,700 transition tiles only when a cell
  first shows one (`CompiledAtlasTiles`), because creating them all took about 18 s: every cell painted on a world-map
  layer goes through `AtlasCells.Set`, never `TileMapLayer.SetCell`. `MeshStatistics` (a
  readonly struct) and `AutoTileFormatDefinition` (a sealed record) are immutable: every property is set once.
- **IServiceProvider**: Nodes implementing this interface and added to the `service_providers` group will have their services registered at startup
- **Tree injection**: a node implementing `IDependent` (`Scripts/Core/Interfaces`) gets `Inject(IServiceResolver)`
  from the autoload on `SceneTree.NodeAdded`: after the node's `_EnterTree`, before its `_Ready`, and again whenever it
  re-enters the tree (reparent). `Inject` only stores the resolver; the node does its wiring in `_Ready` through it.
  Nodes already in the tree when the autoload starts are injected once by a startup pass
  (`CompositionRoot.InjectExistingNodes`). The main scene has entered but is not ready then, so its nodes are
  injected before their `_Ready`; an autoload ordered before Services is already ready, so an `IDependent` must
  never be one.
  The injected resolver is the autoload's `ServiceHost`, which reads its current container at call time, so a
  resolver injected before a reset reads the replacement container and its `Get<T>(callback)` shares the one
  pending-callback queue.
- Services can use async resolution via `IServiceResolver.Get<T>(callback)` for services not yet registered

Nodes reach services only through the `IServiceResolver` they are injected with (`Get<T>()`, `Has<T>()`,
`Get<T>(callback)`); nothing in `Scripts/` looks a container up statically. Tests go through
`Tests/TestUtilities/TestServices`, which reads the autoload's host: `Container`, `Resolver` (the resolver the root
injects), `Reinitialize()` (composes the autoload again), `Reset()` (empties the container and the queue
and clears `ILog.Sink`), `RegisterGameServiceClasses(container)` (the root's `[Service]` scan into a container),
`ExecutePendingCallbacks()` (runs the queued `Get<T>(callback)` lookups, as the root does after composing) and
`Inject(node)` for a node built outside the tree. A unit test of the host itself builds one with `new ServiceHost()`.

### Dependency Gate

`Tests/Architecture/DependencyGateTest.cs` (in the pull-request tier, so every pull request runs it) measures `Scripts/` as a
layered architecture on every run. Its code is test code under `Tests/Architecture/DependencyGate/`: Roslyn extracts
the class graph of every top-level type in `Scripts/` (nested types fold into their outer type; `addons/` except
gdUnit4 is compiled alongside but not counted), plus one member-type reference per NodePath between two scripted
nodes in the `.tscn` files of `Scenes/` and `Data/` (merged with the code's own member-type references on that pair).
Three anchors are each one module: UI on top (Godot types and engine-API users, plus interfaces only UI uses), pure
data at the bottom, and pure contracts (interfaces naming only data and contracts) above it. The middle is clustered
with a deterministic Louvain consensus (seeds 0 to 49). A class referenced from 6 or more homes (`Features/<X>`,
`Core/<X>`) is omnipresent: it is left out of clustering and joins the module it references most, or stays loose
when it references none. A class whose summed clustering weight to other middle classes is below 4 stays loose, and
so does every class of a community under 4 classes. Modules are ordered Data, Contracts, middle, UI; inside each
layer every loop of up to 20 modules takes the order with the fewest up edges, and a bigger loop is ordered greedily
(the report names it).

The test prints a report (`dependency gate:` lines): counts, the module order, the loose classes, every up edge with
its class pairs (heaviest first), every loop, and every leaky or missing seam with the concrete classes it crosses.
It measures the class graph built once per test run (`RepositoryClassGraph.Shared`, which `SignatureLayerTest` reads
too). The Daily suite `DependencyGateRepeatabilityTest` builds a second graph on a worker thread at the same time and
fails if its report differs from the shared graph's; it is Daily because the second Roslyn compilation costs several
seconds. The gate then ratchets four gated numbers (`up_edges`,
`biggest_loop`, `seams_leaky_or_missing`, `refs_from_non_ui_to_ui`) against
`Tests/Architecture/dependency-baseline.json` in that priority order (#299). The label of one seam can flip between
leaky and missing when Louvain membership changes, so the report still prints both counts but only their sum,
`seams_leaky_or_missing`, is gated (#278). The first number that differs from its baseline decides; if no number
differs, the test passes. If it went up, the test fails naming it (every other rise is
listed after it). If it went down, the step is accepted, and a rise in a lower-priority number is accepted with it:
each such rise is named in the failure and printed as a `dependency gate:` line (`<name> is <m>, above the baseline
<b>: accepted because <deciding> went down`). An accepted step still fails until the baseline is re-recorded with the
switch set:

```bash
CARDCLEANER_UPDATE_DEPENDENCY_BASELINE=1 <godot console exe> --headless --path . -s res://addons/gdUnit4/bin/GdUnitCmdTool.gd -c --ignoreHeadlessMode -rc 100000 -rd reports/ci -a res://Tests/Architecture/DependencyGateTest.cs
```

The switch rewrites the file only when the deciding number went down (it still prints the accepted rises), refuses
and leaves the file unchanged when the deciding number went up, leaves it untouched when no number differs, and a
missing or malformed baseline fails with its path whether the switch is set or not and is never written. A first
baseline is created by committing the file. CI prints the report lines (`Show the dependency gate report` step and
the job summary) on every run, pull requests included. Every step of plan v4 (#239) ends with this test green and
the baseline re-recorded to what the step achieved. The fixture suites beside it
(`LayerAnchorsTest`, `LouvainClusteringTest`, `ModuleOrderTest`, `SeamRaterTest`, `GateMetricsTest`,
`ClassGraphExtractorTest`, `SceneLinkReaderTest`, `DependencyBaselineTest`) pin each rule on small graphs.

The baseline also records four class-name lists behind the gated numbers: `up_edge_pairs` (for `up_edges`),
`biggest_loop_classes` (for `biggest_loop`), `seam_classes` (for `seams_leaky_or_missing`) and `ui_ref_pairs` (for
`refs_from_non_ui_to_ui`), each sorted and written as "A -> B" pairs or class names. The lists never fail the gate. When
a number is refused or lowered, the failure text names the classes joined and left in its list. When no number differs
but a list changed, the gate passes and prints a `dependency gate:` note with the joined and left classes. The switch
records the lists when no number differs and they are missing or changed; it still never writes after a refused rise.
Recovery when a step moves a gated number: run the gate on the base as a control, and try at most three placements of
the new class before recording a seam. Leaky and missing are already one gated sum (`GateMeasurement.Gated`), so a
missing-to-leaky relabel is not a rise.
The Louvain visit order is sorted by a hash of the seed, level and class name, so adding a class no longer reshuffles
every seed; the total weight and the termination threshold stay global, so stability under every change is not promised.

The baseline records `seams_leaky_or_missing` 0 with an empty `seam_classes` list. The WFC constraint
`BiomeAffinityConstraint` reads biome strengths through `IBiomeStrengthField` (`Scripts/Features/Worldgen/Wfc`), which
`BiomeStrengthField` implements.

### Quality Gate

The `quality-guard` pull-request check (`.github/workflows/ci.yml`, dod-guard pinned by SHA) scans the `Scripts` files a
pull request changes at their merge base and at HEAD. A high finding that exists only at HEAD blocks (for line length
that means above 120 columns, as in `.editorconfig`); a finding that was already there never blocks. New medium findings
do not block either, but they are counted and listed. The pull request must also remove at least one medium/high finding
that was already in those files, so medium cleanups count toward the quota. Every pre-existing finding is listed in the
`quality-gate-report` artifact, linked from the pull request's single quality gate comment, so none of them needs an
individual fix. Tighten the policy with the flags on the gate step (`--block-on=medium`,
`--rule-block-on=<rule>:<severity>`, `--fix-medium-high`, `--fix-low`). After moving the dod-guard pin, check that
`.github/quality-guard-godot.patch` still applies; the job's own patch step fails when it does not.

Run the same gate locally with `node .github/quality-gate-local.mjs [--base=<sha>]` on the committed head before pushing a
change under `Scripts/`. It reads the dod-guard pin, root and block-on setting from the `quality-guard` job in
`.github/workflows/ci.yml`, keeps a clone of that pin in the system temp directory, applies
`.github/quality-guard-godot.patch`, and writes `reports/quality-gate-report.md`. The gate diffs a base against HEAD, so
the command refuses a dirty working tree: commit or revert first. `.github/quality-gate-parse.mjs` reads `ci.yml`,
`.github/quality-gate-cache.mjs` keeps the clone (it resets and reapplies the patched file on every run, so a stale or
hand-edited cache is repaired), and `.github/quality-gate-local.test.mjs` pins the reading of `ci.yml` and runs in the
`quality-guard` job.

### Card Signature System

The core mechanic uses `CardSignature` - an 8-dimensional vector with values clamped to [-1, 1] representing elemental properties:

| Index | Name | Description |
|-------|------|-------------|
| 0 | Solidum | Solidity (air-rock) |
| 1 | Febris | Temperature (water-fire) |
| 2 | Ordinem | Orderedness (entropy-order) |
| 3 | Lumines | Luminance (dark-light) |
| 4 | Varias | Manifold (time-space) |
| 5 | Inertiae | Density (heavy-light) |
| 6 | Subsidium | Helpfulness (harmful-helpful) |
| 7 | Spatium | Distance (nearby-distant) |

Signatures influence card generation, combat calculations, map generation parameters, and loot drops.

### Card Mesh

The card body is a committed mesh, `Data/Card/CardMesh.tres`, shown by the `OuterBox_Baked` node in `Scenes/Components/CardShader.tscn`; no card builds CSG at runtime. Its source is `Tests/Features/Card/Components/CardMeshRecipe.cs` (test code: a CSG tree for a 0.32 x 0.45 x 0.005 m card with 0.032 m rounded corners, baked and given the card's UV layout). `CardMeshBakeTest.CommittedMeshMatchesRecipe` fails when the file and the recipe differ, and `CardShaderSceneTest` fails when the `Designer` dimensions or the `OutlineBox` size (mesh plus 0.01 m) no longer match the mesh. To change the shape, edit the recipe (and the size constants in `CardMeshBakeTest`), then regenerate with the switch set:

```bash
CARDCLEANER_REGENERATE_CARD_MESH=1 <godot console exe> --headless --path . -s res://addons/gdUnit4/bin/GdUnitCmdTool.gd -c --ignoreHeadlessMode -rc 100000 -rd reports/ci -a res://Tests/Features/Card/Components/CardMeshBakeTest.cs
```

The drift test can fail in that run because it may compare against the old file; run the suite again without the switch. For a size change, also update the `Designer` `Width`/`Height`/`Thickness`, the `CardCollision` box and the `OutlineBox` mesh size in `CardShader.tscn`. Never edit the `.tres` by hand.

### Card Art Effects

`SignatureCardGenerator` gives every spawned card a rarity effect (embossed, glow, glossy, foil) and a condition effect (worn, shiny) on its art region: `CardEffectMapping` maps the rarity and the signature's intensity tier (mean element magnitude, so the all-zero signature of an ordinary pack card is Dormant and looks worn) to shader ids, and `CardMaterialManager.SetArtEffects` passes them with a per-card seed (`CardEffectSeed.For`, from the signature alone, so the same signature always gets the same scratches) and the baked bevel map (`CardEffectNormalMapCache`, baked once per art texture) to `Shaders/card_layers.gdshader`. The bevel map is baked off the main thread (a couple of art readbacks a frame, the bake on the thread pool), so the first card to show a new art texture renders its condition effect only and switches its rarity effect on when the map is ready, a few frames later; tests call `CardEffectNormalMapCache.ResetForTesting()` in their teardown. `CardMaterialManager.SetArtEffects` returns the number of its request, and the bevel map is delivered with that number through `SetArtNormalMap`, so a map for a request that a newer generation has superseded is ignored. To compare all five rarities (columns) by the three tiers (rows) side by side, open `Scenes/Debug/CardEffectComparison.tscn` in the editor and press F6 (run current scene); it is not the main scene, spawns real cards through the normal spawning service and fits the grid to the window. In that scene key 2 adds 10 cards, key 3 adds 100, key 0 removes them and key E sets `CardShaderRenderer.ArtEffectsEnabled` on every card (each card's material manager holds the on/off state, so a bevel map that arrives while the effects are off waits until they are switched back on), while the top-left readout shows the frame time and the GPU time, so the cost of the effects can be compared; the `CardSpawner` random-card keys 1 to 4 are switched off there (`SpawnKeys`).

### Container Meshes and Opening

The booster (`Scenes/Shop/Items/Booster.tscn`), pack (`Pack.tscn`) and cardboard box (`CardboardBox.tscn`) are built
like the card: committed meshes in `Data/Packs/Meshes/` with their materials in `Data/Packs/` (the foil wrapper
`Shaders/booster_wrapper.gdshader`, the printed board and kraft carton `Shaders/printed_board.gdshader`). Their source
is `Tests/Features/Packs/Components/ContainerMeshRecipe.cs` (test code), and `ContainerMeshBakeTest` fails when a
committed mesh differs from the recipe. To change a shape, edit the recipe, regenerate with the switch set, then run the
suite again without it:

```bash
CARDCLEANER_REGENERATE_CONTAINER_MESHES=1 <godot console exe> --headless --path . -s res://addons/gdUnit4/bin/GdUnitCmdTool.gd -c --ignoreHeadlessMode -rc 100000 -rd reports/ci -a res://Tests/Features/Packs/Components/ContainerMeshBakeTest.cs
```

Never edit the mesh `.tres` files by hand. The words printed on a container (brand, count, THIS SIDE UP) are flat,
shaded `Label3D`s lying on its faces, and `StockItemLook` copies them with the meshes, so a checkout prop keeps its
print. The parts that move when a container opens (the booster's tear strip, the pack's lid, the box's four flaps) are
`OpeningPart` nodes: each turns by `OpenRotationDegrees` about its hinge and moves by `OpenOffset` over its share of
the unseal time (`StartAt` to `EndAt`), and one that `VanishWhenOpen` (the torn-off strip) shrinks away.

`CardContainer.Open` plays the opening over real time (`ContainerOpening`, timed by the Godot-free
`ContainerOpeningPlan`). The container stops colliding and freezes, drops onto the floor under it when it was opened in
the air (`ContainerOpening.SettledPose`, a free-fall time capped at 0.7 s), unseals, then lets one item out every
`ReleaseStep`, at most one new item a frame. Each item rises to its spot (`SpawnOffset`: one column above a pack or
booster, two columns side by side above a box, starting `LiftHeight` above the container's base). The items hang
frozen at their spots while the empty wrapper shrinks away and drop together once it is freed, so none falls back into
the wrapper, which no longer collides. Every opening ends within three seconds (`ContainerOpeningPlanTest`).
`FinishOpening` ends one at once (every item at its spot, the wrapper freed). `GameSaveService.Save` first finishes
every opening container (`SavedContainers.FinishOpenings`), so a save made mid-opening stores the eight items, never a
half-open container. Tests wait for an opening with `ContainerOpeningWait` (bounded in wall-clock time through
`BoundedWait`, because a headless frame is not paced at the game's 60 frames a second), or end it with `FinishOpening` when only the contents
matter.

Sizes: booster 0.35 x 0.045 x 0.53 m, pack 0.38 x 0.37 x 0.55 m, box 0.80 x 0.60 x 0.86 m. The product shelf's seats
sit at z 0.055, 4.5 cm further back than before, so a pack fits on its 0.671 m deep boards. That is under the 5 cm a
saved container may lie from its seat (`SeatAt`), so older saves need no migration step.

### World Generation

> **Architecture Document**: See `docs/WORLDGEN_ARCHITECTURE.md` for the complete worldgen architecture, including the Soft WFC approach, weight modifier system, and data definition guides.

**Active System**: Two-Phase WFC via **GridMapGenerator** (`Scripts/Features/Deckbuilder/Services/GridMapGenerator.cs`)

The map generator uses a **two-phase Wave Function Collapse** approach with dual-layer terrain:

1. **Background Layer (Phase 1)**: WFC generates simple (non-auto-tile) terrain tiles
   - Gap tiles fill the entire map with biome-influenced probability
   - Creates the base visual layer seen through gaps in foreground terrain

2. **Foreground Layer (Phase 2)**: WFC generates auto-tile terrain with gap constraints
   - `AutoTileGapConstraint` enforces 1-tile gaps between different auto-tile types (8-way check)
   - Gap tiles (non-auto-tiles) separate auto-tile regions, eliminating bitmask conflicts
   - Dual-grid rendering samples 4 data corners per visual tile; gap constraint ensures at most one auto-tile type per 2x2 window

3. **Merge**: Foreground takes precedence where present; background shows through gaps

**Key Files** (all in `Scripts/Features/Worldgen/Wfc/`):
- `WfcMapGenerator.cs`: High-level generator that configures and runs WFC
- Outside this folder, code holds `IWfcMapGenerator`, `IWfcTerrainSolver` or the factory interfaces `IWfcMapGeneratorFactory` and `IWfcTerrainSolver.IFactory`, never `WfcMapGenerator` or `WfcAdjacencyRules`; the production factory is the internal `WfcMapGenerator.Factory` (one class for both factory interfaces), found by the `[Service]` scan. `WfcMapGenerator` implements `IWfcTerrainSolver` itself (RuleTileIds snapshot, GenerateBackground, SolveGraphWithCatalog). `WfcSolveRunner` times the retrying solve and builds the `WfcGenerationResult`.
- `WfcSolver.cs`: Core WFC algorithm (cell selection, collapse, propagation)
- `WfcPropagator.cs`: Constraint propagation using adjacency rules
- `WfcTileSelector.cs`: Weighted tile selection with soft constraints
- `Constraints/AutoTileGapConstraint.cs`: Prevents adjacent different auto-tiles (hard constraint)
- `Constraints/SpatialCoherenceConstraint.cs`: Encourages contiguous tile regions

Map generators and `BiomeStrengthGrid` take a signature sampler, a `Func<Vector2I, Vector2I, CardSignature>`
(position, map size), not a gradient. The map builders create the gradient for a map: `GridMapGenerator` for the grid
path and `TerrainGeneratorFactory.CardSampler` for the mesh path, each from one seeded rng, so the draw order and the
seeded maps stay the same. `BiomeDistributionCalculator` also builds one, per simulation run, to estimate biome
shares. A null sampler on the mesh path means fallback terrain.

**CRITICAL - WFC Soft Constraints and Entropy**:
- **Tiles are NOT removed from possibility sets** - constraints set weights to 0 instead
- `cell.GetPossibleTiles().Count` is MEANINGLESS for entropy - it includes 0-weight tiles
- Entropy MUST be calculated using weighted probabilities from constraint evaluation
- **DO NOT** try to optimize cell selection by counting tiles - this breaks WFC completely
- Any entropy optimization must preserve full constraint weight calculation

**Dual-Grid Auto-Tiling**:
- Visual tiles offset by half a cell from data grid
- Each visual tile samples 4 data corners -> 4-bit bitmask (Corner16 format)
- The gap constraint ensures each visual tile sees at most ONE auto-tile type
- `Dominance` property on tiles determines which terrain renders "on top"

**Gradient Systems**:
- `CardBasedGradient`: Creates gradients from input cards (sphere/capsule/Bezier)
- `RadialGradient`: Center-to-edge signature blending
- `NoiseGradient`: FastNoiseLite-based variation
- Gradients influence biome selection via `BiomeAffinityConstraint`

### Game Session Flow

The `GameSessionService` manages the game loop through states: `WaitingForCards` -> `GeneratingMap` -> `Exploring` ->
`InCombat` -> `GeneratingLoot` -> `SessionComplete`. Key supporting systems:

- **GridMapGenerator**: Creates tile-based maps influenced by card signatures
- **ExplorationAI**: A* pathfinding for autonomous exploration
  - Operates in two modes: `FrontierExploration` and `PathToEnemy`
  - Emits events for path updates, visited tiles, player movement, and enemy encounters
- **FrontierExplorationBehavior**: Intelligent exploration using frontier expansion
  - Tracks seen vs. visited tiles using visibility checking
  - Automatically marks trivially visible tiles as visited
- **IVisibilityChecker**: Interface for line-of-sight calculations with configurable vision range (default 5)
- **SimpleCombatSystem**: Turn-based combat using card signatures

**Debug Visualization**: Overlay layer system allows non-destructive visualization of exploration data (current path, target tile, visited tiles).

**Key Exploration Events**: `PathUpdated`, `VisitedCellsUpdated`, `PlayerMoved`, `EnemyEncountered`, `EnemySpotted`

### Persistence

Uses the Saveable addon (`addons/saveable/`) with Newtonsoft.Json. Implement `ISaveable` interface with `UniqueID`, `Load(NodeSave)`, and `Save(NodeSave)` methods. The shop's `GameSaveService` autosaves the active slot on a timer and on the pause menu's Save button (`ISaveGameCommand.ManualSave`).

Save slots (`Scripts/Features/SaveSlots`) live under `user://saves/<slot>/save.json` (`SaveSlotLayout`). Each file is a `SaveEnvelope` at version 1. `SlotStore` writes `save.json.tmp` first and renames it into place, so a crash leaves the previous save intact. `SlotStore` also implements `ISaveSlotCatalog`, and a slot without a readable `save.json` is listed as unreadable. Slot names (`SaveSlotName`) are 1 to 32 characters of letters, digits, spaces, `-` and `_`, and cannot be `con`, `prn`, `aux` or `nul` (compared without case). `ActiveSaveSlot` starts on the `default` slot until one is selected. `LegacySaveImporter` copies the old single-file `user://game_save.json` into a slot named `imported`, and runs only when no slot exists yet and that file holds a JSON object. A first save that fails removes the slot it started. A container still opening when the game saves is finished first, so its items are saved instead of it. A `CardContainer` draws a content seed only when a save asks for one (`SeedForSave`); the seed is saved with the slot, so the same seed opens the same cards after a reload, and an unseeded box opens from the shared random source as before. Each later stateful slice adds its own saved state to the envelope and a round-trip test that saves, reloads and compares it. Prices live with the stock, never with a price tag: the booster and pack prices are saved by the scene's `ShopPriceBook` (UniqueID `prices`, only the kinds the player priced), and a card's own price (`CardController.ShopPrice`) is saved with the card (`price` in its record) and comes back with it into its slot. A save that still holds an old tag markup (an id starting `slot_price:`) loads with that entry ignored, so its stock asks the market price, and a corrupt price logs a warning and falls back to the market price.

Furniture saves under UniqueID `furniture` (`FurnitureSaveService`) as one JSON string of `FurnitureRecord` entries: every
placed piece with its pose and node name, the scene's starting pieces included, and unopened furniture boxes with their
position. Every save sets `whole_layout`. A load of such a save makes the furniture match the records: a standing piece
whose record has its name and item is kept and moved to the saved pose (so its price tags and stock load onto the same
nodes), a record with no such piece makes a new one, and a standing piece no record keeps is freed. An older save without
`whole_layout` leaves the starting pieces standing and adds its records beside them. Boxes are always replaced, and the
navigation mesh is rebaked once. A save without the key leaves the scene as authored, and a record with an unknown id or a
non-finite coordinate is skipped with an error. A record whose pose no longer fits (a piece that grew since the save, a
lot not owned, an overlap) logs an error and comes back as its box on a free cell of the delivery area
(`FurnitureBoxSpawn.OnDeliveryArea`), so no update deletes a piece the player bought. `FurnitureSaveService` is an `ILoadsFirst`
(`addons/saveable`), so `SaveSystem` loads it before the rest of the tree and the restored shelves are in the tree when
their price tags and cards load. A restored piece keeps its saved name under `World/Furniture`
(`PlacedFurniture.StandUnder`; a new piece takes the first free `Furniture<n>`), which keeps those node-path save ids
stable, and restored boxes are tracked by the `DeliveryMarker` so a new order avoids them. Stock on a placed shelf
(cards and containers) is saved by `GameSaveService`, not in the furniture record, at the seat it is locked on.

A card or container saved on a slot's seat (`DeckSlot.SeatAt`, the nearest seat within 5 cm) is locked back into that
slot while the save loads (`SavedContainers.Restore`, `SavedCardRespawner`), before any physics step, lower seats first.
The slot's trigger cannot do it: a seat can lie inside its shelf's solid collider (the product shelf's does), which
pushes a free body off the seat, and out of the trigger, on the first step.
`ShelfStockSaveTest.StockIsBackInItsSlotsBeforeAnyPhysicsStep` pins it. The load first takes the scene's own cards and
containers out of the tree at once (`RemoveChild`, then `QueueFree`), and a slot releases any locked body that leaves the
tree, so a card-shelf slot, which holds one card, is free again before the saved copy of its card respawns
(`GameSaveServiceLoadTest.ReloadingPutsTheSavedCopyOfAShelvedCardIntoItsOneCardSlot`).

The starting product shelf and storage rack used to stand under `World/Storefront/ProductShelf` and
`World/RightStorage/StorageRack`, so a save from that layout keys their price tags by those paths and keeps their stock
at the old seats. `GameSaveService.LoadGame` passes every payload through `LegacyShelfSaveMigration.Migrate` first:
when the payload holds a tag price id under an old path (which only a save from that layout has), a container saved
within 0.25 m of an old seat moves to the same slot's seat on the moved shelf, which locks it on load. The old price
ids stay in the payload, and the load ignores them. A payload with no such id loads unchanged. Moving or renaming a saved node again needs the same kind of step.

### Feature Organization

Code is organized by feature under `Scripts/Features/`:
- **Card/**: Card models, controllers, components, and services (spawning, generation)
- **Deckbuilder/**: Game session management, map generation, combat, exploration
- **Worldgen/**: Procedural generation support (auto-tiling, biomes, gradients, blob generation)
- **Player/**: Player controller and interaction system
- **Packs/**: Card containers the player opens (box of 8 packs, pack of 8 boosters, booster of 8 cards; see Container Meshes and Opening); `CardPackGenerator` makes each card special with probability 1/200 (about 92% of boxes hold at least one) and special cards show their signature along the card edges
- **Conveyor/**: Conveyor belt mechanics
- **Portal/**: Backoffice wall seam that opens to a doorway and teleports the player (pure rules in `Models/`)
- **Pause/**: Pause menu controller

Core utilities and interfaces are in `Scripts/Core/`.

## Canonical dod-guard Workflow

- `/add-backlog-idea` creates an issue in **Backlog**.
- `/refine-backlog-item` researches the issue and moves it to **Todo** when it is ready.
- `/next-ticket` implements exactly one issue on one issue branch.
- `/submit-draft-pr` publishes the branch as a draft pull request.
- Review remains read-only until the user explicitly accepts the findings or requests remediation.
- `/complete-pr` owns the ready-to-merge check, merge, linked-issue confirmation, and remote branch deletion.

### Private Assets Submodule

All art and models live in the private repo `TychoHenzen/cardcleaner-assets`, a git submodule mounted at `Assets/` (so `res://Assets/...` resolves there). It holds the former `Graphics/` folder as `Assets/Graphics/`, the Synty files in `Assets/Synty/`, the card, terrain and material resources, and the Godot `.import` files (tracked there so UIDs stay stable). It does not use Git LFS (about 75 MB, no file over 5 MB); revisit if a single file passes 50 MB.

- Clone with `git clone --recurse-submodules`, or run `git submodule update --init` in an existing clone. An empty `Assets/` means the submodule is not initialised.
- Changing an asset is two commits: commit and push inside `Assets/` first, then commit the updated `Assets` pointer in this repo. Pushing the pointer before the submodule commit breaks CI.
- CI fetches the submodule with the read-only deploy key in the `ASSETS_DEPLOY_KEY` secret (`.github/actions/checkout-assets`). The project cannot load without `Assets/`, so the step fails when the key is unavailable; fork and Dependabot pull requests cannot read the secret and fail there.
- `Z:\Tycho\Projects\Assets` is our internal asset collection and the Asset Manager workspace. Whenever new assets are needed, look there first. Assets are added with the Asset Manager editor tab (`addons/asset_manager`): it exports to `res://Assets/<Type>/` (export roots under `[asset_manager]` in `project.godot`). Export the individual assets you need; never copy a whole source pack into the repo. The Synty packs are under `Z:\Tycho\Projects\Assets\_extracted\3d` (one `POLY_*` folder per pack, each a converted Godot project; the source FBX files of the NewYork and Megapolis packs sit in its `.godot\unidot_temp`, and several packs hold only imported `.scn` caches).

### Main Menu and Options

`Scenes/UI/MainMenu.tscn` (`MainMenuController`) lists the save slots and starts the shop on one of them. New game opens `NewGameDialog`, which asks for the slot name with the field focused; Enter in it confirms, as OK does. The slot menu names only `ISaveSlotCatalog` (List, TryCreate, Delete over `SaveSlotInfo`), `ILegacySaveImporter` and `IActiveSaveSlot` (Activate; the shop's `GameSaveService` then loads the active slot). Options live in `user://settings.cfg`, separate from the slots. `GameOptions` is the pure model. `OptionsApplier.Apply(options, window)` drives the `Master`, `Music` and `SFX` audio buses and the window (engine-global, in any scene). It sends the window mode, size and vsync to the engine as a `DisplayPlan` (`OptionsApplier.DisplayFor`) through a display sink whose test constructor is internal, so fullscreen, resolution and vsync are proven headless by recording the plan. Options code never touches `user://saves` (`OptionsSlotIsolationTest`). `OptionsApplier.ApplyMouse(options, IGameSettings)` sets the per-scene mouse values, and the shop's `MouseOptionsStartup` node applies the saved mouse options to the shop's `GameSettings` when the shop starts. The pause menu's Options button opens the same options screen (`Scenes/UI/OptionsMenu.tscn`), and its Quit to menu button saves the active slot and then changes to the main menu.

### Shop Scene and Licensed Assets

`Scenes/Gameplay/ShopScene.tscn` is the graybox shop (a storefront at x 0..16, z 0..12; one storage and backoffice room along its back and right sides, 24 m wide at the back; a road door in the right arm at x 18.05 that joins the room to the street; and a locked expansion lot to the left of the storefront, sealed by the single wall at x = 0). `Scenes/UI/MainMenu.tscn` is the main scene, so the game starts at the main menu, which opens `ShopScene.tscn` on a save slot (see Main Menu and Options below). `Scenes/Gameplay/StartScene.tscn` is a legacy scene and is no longer the main scene. Wall, floor, door and counter art comes from the licensed Synty packs. Only the Synty files exported through the Asset Manager are committed, and only in the private submodule, never in this repo:

- To add Synty files, export them through the Asset Manager tab (export roots are in `project.godot`), open the project in Godot so it imports the FBX files, then commit the new files and their `.import` files inside `Assets/` first and the `Assets` pointer here second.
- Each shop issue exports its own assets.
- `ShopArtSlot` nodes load a pack mesh at runtime and hide their graybox placeholder. Without the import the scene still loads and shows the placeholders. The script is a `[Tool]`, so the editor viewport shows the real art too; the placeholder is restored around every editor save (`NotificationEditorPreSave` / `PostSave`) so no scene ever stores it hidden, and the loaded `Art` child has no owner and is never saved. Changing `ArtPath` or `ArtScale` in the inspector reloads the preview. The pack FBX files have no usable texture and are single-sided, so a slot applies its pack's colour atlas on a double-sided material (`PackMaterialCatalog` maps a pack folder to its atlas, both the `res://Assets/Synty/` packs and the Asset Manager's exports under `res://Assets/Models/`, which keep the library's folder layout; add a new pack there, and `ShopArtMaterialSceneTest` fails when a Synty model in the shop is in no known folder). A slot's `Tint` is multiplied into that atlas (the shop's walls are cream and its floor tiles warm); slots of one pack and tint share one material. `ArtOffset` moves a pack model authored far from its origin back onto the slot (it is not scaled by `ArtScale`). The storage rack shows `SM_Rack_B` (PolygonSurvivalWorkshop), the product shelf `SM_Food_Shelf_02` (PolygonSurvivalConstruction) and a furniture box `SM_Cardboard_Box_01_Closed` (PolygonSurvivalTools). Collision is hand-authored and independent of the art.

**Roof, lights and sign.** `Scenes/Shop/ShopRoof.tscn` (instanced as `World/Roof`) closes the building: a ceiling of world-space tiles (`Shaders/ceiling_tiles.gdshader`) at 3.5 m, the top of the wall art, so no gap shows above a wall; a roof slab; a fascia with the shop name over the street door; and the ceiling light fixtures (an emissive panel and one shadowless `OmniLight3D` each) that light every room, the expansion lot included. The roof covers the whole footprint x -8..24, z -8..12; a room or lot added outside it needs the ceiling and a fixture grid extended. The ceiling collides on the default layer but is not in `shop_navigation`, so the navigation bake never walks on it. `ShopRoofSceneTest` fails when a floor tile lies outside the ceiling. The shop sign (`World/Storefront/ShopSign`) is a lit two-sided board on a post by the door (a screen `SignBoard`) that reads OPEN in green or CLOSED in red; the click prompt says what a click does. Every aim highlight (sign, counter, PC, price tags, boxes, packs, boosters) uses `Data/Ui/AimHighlightMaterial.tres`, a box drawn as glowing edges (`Shaders/highlight_edges.gdshader`), so the object inside stays readable.

**Street and neighbourhood.** `Scenes/Shop/Neighbourhood.tscn` is visual only: it has no collision. Its `ShopArtSlot` nodes load the POLYGON Mini City pack from `Assets/Synty/PolygonMiniCity`, and each slot keeps a `BoxMesh` placeholder that shows only while the pack file is missing, so the scene loads and the layout holds without the pack. The kerb is the one box primitive that is not a slot (the sky is set in `ShopScene.tscn`). The road, kerb, far sidewalk and facades lie across the road from the storefront, a neighbour flanks each side, and the scenery sits outside the street bounds and the workshop footprint. `NeighbourhoodSceneTest`, `NeighbourhoodArtSceneTest` and `NeighbourhoodScenerySceneTest` enforce this.

**Furniture.** The PC desktop's Furniture app (a second `StockApp` titled Furniture, catalog
`Data/Shop/FurnitureOrderCatalog.tres`) sells four pieces: `shelf_unit` (card shelf), `product_shelf`, `storage_rack` and
`bulk_packing_station`. Each is delivered as a `FurnitureBox` (`ShelfBox.tscn`, `ProductShelfBox.tscn`,
`StorageRackBox.tscn`, `PackingStationBox.tscn`; group `furniture_boxes`). Interacting with a box hands it to
`FurniturePlacementController`, which shows a `FurnitureGhost`. The keys T (turn), E (confirm), C (cancel), G (pick an
aimed piece up to move it) and B (pack an aimed piece back into its box where it stood, `FurnitureBoxSpawn`) work the
furniture; G and B refuse a shelf that still holds stock. A confirmed piece becomes a `PlacedFurniture` under
`World/Furniture`, and `FurnitureProbe` holds its physics checks. Its aim ray excludes `CameraRay.OwnBodies(camera)`
(`Scripts/Core/Utilities`), the bodies the camera hangs under, because the player's capsule is on the furniture layer
and the camera sits on its top; the e2e view's `looking_at` ray (`AutomationView.LookHit`) excludes them the same way. A piece stands only on the shop's floor rooms
(`FurnitureFloor.Shop`, a `FurnitureFloorArea` of the storefront, the back room and the right arm, widened by every owned
expansion lot). The starting card shelf, product shelf and storage rack are `PlacedFurniture` too
(`World/Furniture/CardShelf`, `ProductShelf` and `StorageRack`), so they move, pack and save like bought pieces. The
product shelf stands against the left wall at x 1, facing into the room, clear of the checkout queue lane
(`ShopSceneStockContractTest.NoStartingFurnitureStandsInTheCheckoutQueue`). Each
change rebakes `ShopNavigationRegion`. Its first bake waits for the scene root's `Ready`, because a placed piece
builds its footprint box in its own `_Ready`, after the region's (`TheFirstBakeLeavesNoFloorUnderTheStartingFurniture`). A piece being moved stays in the tree, hidden, off the collision layer and out of
the physics space (`PlacedFurniture.SetCarried` sets `ProcessMode` to Disabled, which removes it and every body, slot
and price tag under it from the space), so the ghost can go back down on the spot the piece left and a save during the
move keeps it at its old pose. A delivered box is lifted the same way for its placement (`FurnitureBox.SetCarried`), so
its piece can stand where the box stood; a cancel sets the box down again, a confirm frees it. `FurnitureSaveCodec` skips a
record it cannot read with a logged error and keeps the rest. The Stock app sells only boxes, packs and boosters.

### Aim HUD and Control Hints

The player scene (`Scenes/Components/player.tscn`) has an `AimHud` CanvasLayer (layer 5) holding `InteractionHud`
(`Scripts/Features/Player/Ui`): a crosshair dot, a gold ring while something is aimed at, the click prompt under it
(`LMB` plus `IInteractable.Prompt` of `InteractionSystem.Target`) and a row of key chips at the bottom of the screen.
It hides while any other owner holds `ICursorOwner` (a menu, the PC, the payment screen). `InteractionSystem.Target`
is only an interactable a click would use now: it is judged again on every 0.05 s scan (alive, `CanInteract`, within
`InteractionRange`), so the prompt never offers a click that does nothing. An interactable names its click in
`Prompt` (default "Use"); a card's reach is 4.5 m. The key chips come from `IControlHints` (`ControlHintBoard`, a
Godot-free `[Service]` in `Scripts/Core/Services`): each source publishes its own list (`Publish(source, hints)`, an
empty list or `Clear(source)` removes it) and the board shows them in publishing order. The sources are
`CardHighlighter` (cards in hand, `CardHandHints`), `ContainerHolder` (a held box, pack or booster, `ContainerHints`,
which says "Unpack" only when `IUnpackTarget.CanUnpack` takes that container) and `FurniturePlacementController`
(`PlacementHints`). A new held-item mode publishes its keys the same way and clears them in `_ExitTree`.

While any owner holds a cursor claim, `CursorOwner` sets `IInputService.ActionsSuspended`, so a key or click made on a
menu, the PC or the payment screen never also acts in the world behind it (no card is thrown, no piece placed). Releases
still reach mouse actions, and raw events and the polled movement are not gated. `CursorOwner` clears the flag when it
leaves the tree, because the input service lives in the app scope and outlasts the scene.

### Backoffice PC Ordering

The PC in the shop backoffice (`World/Markers/PcLocation/PcTerminal`, a child of the PC marker) is a `ScreenTerminal`: it opens and closes the PC, claims the cursor, freezes the player (`PlayerController.ControlEnabled` is false while it is open) and closes on Escape or the taskbar Close button, which restores both. It has a `PcDesktopLink` child and no ordering screen of its own. Ordering happens in the Stock app, a `StockApp` Control under `PcDesktopViewport` that lists the `OrderCatalog` assigned to its `Catalog` export (`Data/Shop/BackofficeCatalog.tres`) as cards, and delivers to its `DeliveryPoint` export. A card shows the item thumbnail (a placeholder tile when it has none), name and price. An Add button puts one unit in the cart (`OrderCart`); an item above the shop level (`IShopProgression.RequiredLevel`) reads `Level n` and is disabled, and an item with no scene or no positive price reads `Unavailable` and logs a warning. An empty or missing catalog shows `Nothing to order.`. Checkout (`CheckoutPlan`) refuses an empty cart, a locked line or a total above the balance and charges nothing then. Otherwise it orders every unit through `IOrderingService` with the app's `DeliveryPoint`, or with none, which delivers to the ordering service's own marker, so the item scenes (`Scenes/Shop/Items/*.tscn`) appear in that marker's grid of slots that never overlap. A `DeliveryMarker` sets its own grid (`Columns` along X, `Rows` along Z, `CellSize`, and `MaxStackHeight` under the ceiling); one that sets none gets 3 x 2 cells of 2.3 x 1.5 m, sized for shelves. In the shop the Stock app delivers to `World/Markers/StockDeliveryPoint` (4 x 3 cells of 0.9 m, stacks up to 1.5 m) and the Furniture app, which sets none, to `World/Markers/DeliveryPoint`; a `Sign` board on the wall above each names it. An item is stacked on earlier deliveries only where, seen from above, every one of its shapes lies on a shape whose top is the stack top (`DeliveryMarker.Supported`, 0.05 m tolerance), so a shelf box never rests on a booster and nothing hangs in the air; when no slot is left the order fails with `OrderStatus.DeliveryFull`, which the app reads as "The delivery area is full. Move delivered items off it first." A unit that is not delivered stops the run, leaves the rest in the cart and keeps Checkout enabled for a retry. Ordering uses `IMoneyService` (balance, `TrySpend`) and `IOrderingService`, both nodes under the scene's `Services` provider. `StockApp` is split into `StockCardGrid` (the cards), `StockCartPanel` (cart rows, total, message, Checkout) and `StockOutcomeText` (the sentences). The starting balance is `MoneyService.StartingBalance`; the balance is saved with the slot (`MoneyService` is an `ISaveable` that stores it under `balance`), so a reload restores it.

The shop PC is a desktop used fullscreen. The `PcTerminal` in `Scenes/Gameplay/ShopScene.tscn` has a `PcDesktopLink` child that holds the `PcDesktop` (`Scenes/Shop/PcDesktop.tscn`, drawn in the 1280 x 720 `PcDesktopViewport` SubViewport that the 3D monitor also shows), the `ScreenCamera` and the `PcScreenOverlay` sibling (a CanvasLayer at layer 8, below the pause menu). On `Opened` the link swings the `ScreenCamera` from the player's view to the monitor (`PcDesktopLink.FocusSeconds`) and activates the overlay, which draws the SubViewport over the whole window (scaled to fit, centred, black bars where the aspect differs) and pushes every mouse and key event into it at the matching pixel (`PcScreenOverlay.TryMapToPixel`); Escape is left for the terminal, which closes the PC. On `Closed` it hides the overlay and gives the player camera back. The link turns the terminal's `OverlayUi` off. Every Control in the game uses `Data/Ui/GameTheme.tres` (`gui/theme/custom` in `project.godot`). `PcDesktop` has one export per app, `StockApp`, `FurnitureApp` and `BillsApp`, and one icon per app; an icon whose app is not assigned stays disabled, and the taskbar Home button returns to the icons. `BillsApp` lists the day's bills from `IDailyBillBook.LinesFor(IShopDayService.Day)`. To sell other items on the desktop, add a catalog and a `StockApp` node; to add an app, add one export and one row in `PcDesktop.IconRows`, which the icon wiring and `ResetToDesktop` both read, so Home closes the new app with no other edit. The workshop terminal is still an `OrderTerminal` (a `ScreenTerminal` subclass) with its own overlay `OrderTerminalUi` and no `PcDesktopLink`, so it lists its `Catalog` as buttons; for another overlay terminal add an `OrderTerminal`, an `OrderTerminalUi` and an `OrderCatalog`.
### Checkout

The shop's checkout counter (`World/Storefront/CheckoutCounter`, a `CheckoutCounter` that is also the production `ICheckoutHandoff`) takes customers' baskets: a `Customer` (`ICheckoutCustomer`) hands over its basket at the counter and waits in one of the four `QueueSlot` markers. The front customer's items appear as scannable props on the `ItemSpot` markers, which sit on the counter's deck (0.62 m), and the player scans each by interacting with it. A scanned item goes into the bag: it shrinks away and stops colliding at once, and the items stacked on it settle into its place. The counter collides as three boxes that follow its art: the 0.64 m deck (`CollisionShape3D`, the shape the checkout tests aim at), the register (`RegisterShape`) and the display case (`DisplayShape`). A customer that implements `IBasketLooks` keeps a still copy of each item's meshes when it hands the basket over (`StockItemLook`: no script, collision or physics, a flat item laid on its widest face), so each prop shows the real card, booster or pack being sold; a customer without it gets plain stand-in props. Then the payment screen (`CheckoutPaymentUi`, which claims the cursor through `ICursorOwner`) takes the card total on a keypad (`CheckoutKeypadPanel`, whose readout shows the typed amount beside the total due), or the cash change by a denomination picker (`CheckoutChangePanel`, whose readout shows the amount due, the cash handed over, the change owed and the change picked so far). A correct entry adds the basket total to `IMoneyService` once, frees the props and tells the customer to leave; Escape closes the screen without paying, and interacting with the counter reopens it. The counter is on the interaction layer only while a scanned sale waits for the player's payment (`CheckoutCounter.CanInteract`), so at any other time the aim passes through it to the scan props and to anything lying on it. Its register display (`RegisterDisplay`, a two-sided screen `SignBoard` on a pole on the register) shows the sale as a till does (`CheckoutCounterText`: `Items 1 of 3` with the scanned subtotal, `Card payment` with the total, `Cash payment` with the total and the cash given, `Cashier serving`) with the last message under it, and nothing while the counter is idle; the balance is on the `ShopHud`, which sits on a dark backing so it reads against the lit ceiling. A customer whose plan is empty (nothing on the sales shelves it could take) walks to a shelf slot (`ShelfOfferIndex.BrowsePoints`, taken in turn), looks for `Customer.BrowseSeconds`, complains "nothing in stock" (`RefusalReason.NothingInStock`) and leaves; a customer who leaves empty-handed for any reason complains once. `InstantCheckout` is the instant stand-in that the customer test suites register, and `CheckoutSession` and `PaymentRules` are the Godot-free rules. The card shelf (`Scenes/Shop/Items/Shelf.tscn`, a `CardShelf`) is a two-board display case (`SI_Prop_Shelf_09` of the SIMPLE Shop Interiors pack at `ArtScale` 60) with eight one-card `DeckSlot`s: four card stands (`Scenes/Shop/Items/CardStand.tscn`) on its top, each holding a card upright and leaning back 25 degrees, and four card rests (`Scenes/Shop/Items/CardRest.tscn`) on its middle board, each holding a card leaning back 78 degrees on an acrylic wedge. The case's opaque top covers its whole depth, so a card standing inside it was hidden from a standing player; the bottom compartment holds no slot for the same reason. A card shows the top of its texture at its local -Z, so each slot is turned with its -Z pointing up and back. Each slot's acrylic parts sit under an `Upright` node that undoes the slot's turn, so the base lies level. `ShelfSeatTest` reads the card's top edge from the card mesh (its smallest UV v), fails a card shown upside down, and fails when a shelf collider or price tag blocks the line from a standing eye (1.8 m high, 1.2 m in front) to a card's centre or the middle of its top edge. A slot seats what it locks at `DeckSlot.SeatPosition`: `PositionOffset`, plus `StackOffset` for each body below it, both in the slot's own frame, and takes the slot's rotation, so stock stays put on a turned shelf. Every shelf collides as boxes that follow its model's parts (boards, sides, posts), not one block, and each slot sits on a board of the model: `ShelfSeatTest` reads the up-facing faces of the loaded art mesh and fails when stock or a stand floats above its board or sinks into it, and `FurnitureCatalogTest` fails when a shelf's `FurnitureItem` size differs from its colliders. A card saved under a slot is locked back into that slot at the seat the slot has now (`SavedCardRespawner`), so a shelf whose seats moved still gets its cards back on load; containers still match a seat by position (`SeatAt`), so a product-shelf or rack seat must not move more than 5 cm without a migration step. A slot claims only free stock: `CanTake` refuses a body another slot holds, and a body is in the slot's list before it is reparented. A reparent reports the body entering every trigger it overlaps again at once, so before this rule two slots whose triggers both reached a container took it from each other without end, until Godot's deferred-call queue overflowed and the process crashed (`DeckSlotContainerTest.ABoosterInsideTwoSlotTriggersIsLockedByOneSlotOnly`). Aim at a free slot with a card in hand (or a held container the shelf takes) and click to stock it, or drop it on the slot; click the stock to take it back. `CardShelf` builds one `ShelfSlotTarget` per slot over the slot's trigger box; it is on the interaction layer only while its slot has room and the shelf is shown, and its click moves the hand's offer (the held container, else the top card) through `IStockHand`, which the player's `CardHighlighter` provides. `StockMarketValue` gives a card its `CardValue` (rarity x intensity tier, x `SpecialMultiplier` for a special card, never below `MinimumCardPrice`) and a booster or pack its `ProductMarketPrices` value. Each slot has a `SlotPricing` price tag (`Scenes/Shop/Items/PriceTag.tscn`, a paper strip on the shelf's front face that prints the asking price of the slot's stock). A click opens the `PricingScreen` (`IPricingScreen`, a scene service under `Services`, an `IModalScreen` that holds the cursor and the player's controls) for that stock (`IPricedStock`): it names the item and what the price covers, shows the market price, the wholesale cost and profit per sale for bought-in stock, and the share of customers who buy at the entered price (`PricingSheet`, from `BuyDecision`), and only Set price changes anything; Cancel and Escape close it unchanged. A card's price is its own (`CardStockPrice`, stored on `CardController.ShopPrice`), so it travels with the card to any shelf. A booster or pack asks the shop's price for its kind (`ProductStockPrice` over `IPriceBook`, an immutable `ProductPriceList` inside `ShopPriceBook`, wholesale costs from `BackofficeCatalog.tres`), so one new booster price repaints every booster tag. A box is never sold and has no price. Prices stay within `ShopPrice.Min` and `ShopPrice.Max` (1 to 9999). `DeckSlot` only locks the bodies its shelf kind accepts (see Shelf kinds below), so a box on a card shelf is ignored. The shop scene has the card services and a `CardSpawner` (`World/Cards`, over open storefront floor away from the counter; keys 1, 2, 3 spawn random cards, key 4 spawns one special card) so cards can be spawned for testing.

**Shelf kinds.** `CardShelf` has a `Kind` (`ShelfKind`, in `Scripts/Features/Shop/Models/ShelfKind.cs`; the per-kind stock table is `ShelfPlacementRule.Accepts`): a card shelf (`Shelf.tscn`, the default), a product shelf (`ProductShelf.tscn`, six slots on its two middle boards, `Slot1`-`Slot3` low and `Slot4`-`Slot6` high, that take a booster or a pack) or a storage rack (`StorageRack.tscn`, four slots on its two lower levels that take a box, a pack or a booster). `Kind` sets which containers each slot accepts and which group the shelf joins: a card shelf and a product shelf join `sales_shelves`, and a rack joins `storage_racks` only. Customers buy only from `sales_shelves` (`ShelfOfferIndex`), so rack stock is never sold. Q on a carried box or pack, while facing a product shelf or storage rack within `ContainerHolder.UnpackReach` (the hand's ray hits areas too, and every shelf has an `UnpackAim` trigger on the unpack layer over its colliders' bounds, so the ray finds the shelf between its boards; the trigger is in `DeliveryMarker.FootprintGroup`, and it is an area, not a body, because the navigation bake parses static bodies even on a carried shelf), unpacks what it holds (a box's packs, a pack's boosters) into the shelf's free slots through `IUnpackTarget` (`CardShelf.TryUnpack`, only when `ShelfPlacementRule.Accepts` takes that stock) and spills the rest; otherwise, for example at a card shelf or beyond `UnpackReach`, it opens loose. Each slot's price tag (`Scenes/Shop/Items/PriceTag.tscn`) is in the `DeliveryMarker.NoFootprintGroup` group, so its solid shape never counts toward a delivered shelf's footprint (the opposite of `DeliveryMarker.FootprintGroup`, which adds a trigger to it) and the next layer rests on the shelf box, not on its tags.

### Shop Progression

XP comes from completed customer checkouts: `CheckoutXp.For` gives max(1, paid / 10) for each paid basket. `ShopLevelTable.Default` holds the level thresholds 0, 50, 150, 300 and 500 XP. `UnlockCatalog.Default` maps an unlock id to its required level, and an id with no row is locked. `IShopProgression.IsUnlocked` and `RequiredLevel` are what the stock app, expansions and staff read. `OrderingService` refuses a locked item with `OrderStatus.Locked` before anything is charged. The XP total is saved per slot. A new unlockable adds its row to `UnlockCatalog.Default` and a test row for it.

### Day Cycle

`ShopDayService` (`Scripts/Features/Shop/Services/ShopDayService.cs`) runs the shop's day. It implements `IShopDayService` and `IComplaintLog` (complaints are counted for the day and reported at close), and it is saved as the `ISaveable` with UniqueID `shop_day`. `ShopClock` owns the day counter and the open or closed state. Time moves only while the shop is open, and reaching closing time closes it once through the service.

- Settlement: `DailyBills.Settle` is the settlement rule. The carried debt is charged first, then the day's lines. Lines with a non-positive amount are dropped, and only what the balance covers is paid.
- Sources: `DailyBillBook` is the one aggregate of bill sources. Its constructor takes `RentBillSource`, `ExpansionRentBillSource` (one "Rent: [lot]" line per owned expansion lot, the only path that charges lot rent) and `StaffWageBillSource` (one "[Role] wage" line per hired role, the only path that charges wages). A later source is one more constructor parameter there, and nothing enumerates `IDailyBillSource`.
- Carried debt: unpaid bills become the day's debt. The debt is charged first at the next close, and nothing else carries it.
- Close order: the settlement is paid, the debt is updated, and the day advances with `ShopClock.StartNextDay`. Only then is the game saved through `ISaveGameCommand.ManualSave`, because the save serializes the whole tree. A save that fails or is unavailable logs a warning and does not roll the day back. `Closed` is raised either way.
- Report: `DayReport` is the plain record of a close. `DayReportSheet` turns it into the lines a reader sees: the title, a column of signed figures (takings with a plus; the carried debt and each bill with a minus; the unpaid debt only when there is some; the day's net) and one line per complaint reason. `DayReportPanel` shows that sheet when `Closed` is raised, claims the cursor and holds player control until Continue is pressed, then releases both. Before it shows, it closes every open `IModalScreen` (`Scripts/Features/Shop/Ui`, group `modal_screens`): the shop and workshop PCs (`ScreenTerminal`) and the payment screen (`CheckoutPaymentUi`). The PC overlay draws above the report and takes every click, and each screen gives the player's controls back as it closes, so they must close before the report freezes the player. A new screen that claims the cursor and freezes the player implements `IModalScreen` and joins the group in `_EnterTree`.
- Arrivals: `ArrivalSchedule.Build` cuts the day from opening to half an hour before closing into one equal slice per customer and draws each arrival inside its own slice, so the first customer comes soon after opening and arrivals never bunch up.
- Load: a saved `shop_day` (day and debt) restores the clock closed at opening time, so a load never reopens the shop or shows the report again. A save with no `shop_day` entry loads as day 1 with no debt. Missing or malformed values log an error and fall back to day 1 with no debt. Out-of-range values are clamped and logged as a warning. `DayState` does the parsing.
- Godot-free models: `ShopClock`, `DailyBills`, `DayReport`, `DayState`, `DailyBillBook`, `RentBillSource` and the interfaces name no Godot or Node type. `ShopDayService`, `ShopSign`, `ShopHud` and `DayReportPanel` are the Godot-facing parts.

### Storefront Expansion

The storefront grows by buying lots, rows of `ExpansionLot` in `Data/Shop/ExpansionLots.tres`, bought in `Order`. `ExpansionPlan` is the Godot-free rule: it holds the lots and the owned ids, `Next` is the first unowned lot, and `Decide` returns a `PurchaseOutcome` (a lot needs its `UnlockId` unlocked in `IShopProgression`, enough money, and every earlier lot owned). `ShopExpansionService` is `IShopExpansion` (UniqueID `expansions`): `TryBuy` spends through `IMoneyService` only on a bought outcome, then raises `Opened`, on which `ExpansionLotView` turns the lot's wall off, hides the wall's buy notice (`Sign`) and rebakes the navigation mesh. Its save stores the owned ids, and a load ignores unknown ids. The Expansions app (`ExpansionsApp`, opened by the PC desktop's `ExpansionsIcon`) lists the lots with their price, rent, extra customers and state, and offers Buy only on the next lot; a refusal shows its reason in the app's message line. `FurnitureSaveService` and `FurniturePlacementController` read the floor furniture may stand on through `IShopExpansion` (`ExpansionFloor.For`); the save service resolves it from its injected resolver when it loads, so the expansion's own save entry has to load first (it sits before `FurnitureSave` in the Services provider).

### Staff

`StaffRole`, `StaffDefaults`, `StaffRoster`, `StaffResult` and `CashierPlan` are the Godot-free rules. `StaffService` is `IStaffService` (UniqueID `staff`): it reads the roles from `Data/Shop/StaffCatalog.tres`, refuses a role whose unlock id is not unlocked in `IShopProgression` (the cashier's `staff_cashier` row in `UnlockCatalog.Default` needs shop level 2), charges the hire fee through `IMoneyService` only after the roster accepts the hire, and saves the hired role ids. A load drops unknown and repeated ids with a warning and keeps the valid ones. `StaffWageBillSource` supplies one wage line per hired role to `DailyBillBook`. `CashierWorker` (a child of `World/Storefront/CheckoutCounter`) shows `CashierFigure` while the cashier is hired. While the shop is open and the tree runs, it claims the front waiting customer through `CheckoutCounter.TryClaim`, scans on `CashierPlan`'s timings and pays through the counter's own `PayCard` and `PayCash`, so money and XP follow the player's path. A claim makes the counter refuse player scans with a message and skip `PaymentReady`; a customer who leaves or a shop that closes ends it, and a dismissal lets the claimed customer finish. The Staff app is the `StaffApp` export and `StaffIcon` row of `PcDesktop`; it shows each role's fee, wage and level lock, and a refused hire shows its reason in the message line.

The restocker is the second role in `Data/Shop/StaffCatalog.tres`, hired from the same Staff app at shop level 3 (`staff_restocker`), paid by the same `StaffWageBillSource` ("Restocker wage") and saved in the same roster. The `Restocker` node (`World/RightStorage/Restocker`, `Scenes/Shop/Restocker.tscn`) works only while the role is hired, the shop is open and the tree is not paused; its start position is its home. `RestockPlanner` and `RestockerJob` (Godot-free) decide, `Restocker` drives with `CustomerNavigator`, and `RestockerScan` gathers the stock and free slots. It moves unopened packs and boosters from storage racks and from the floor near a shelf to free product-shelf slots, and unpacks a box at a product shelf (`CardShelf.TryUnpack`) only when `BoxUnpackThreshold` product-shelf slots are free. The default is 8, which one 6-slot product shelf never reaches, so the shipped shop leaves boxes on the rack until the player adds product shelves or lowers the export. Ids handed to the planner are stable per node (`RestockerIds`), so the job's blocked pairs stay correct across scans. A target that is lost or unreachable blocks that pair for `RestockerJob.BlockSeconds`. A container that nothing can use (a box below the threshold, a pack with no free slot) goes back to its rack slot, or another empty rack slot, or is set down loose. It never takes stock the player holds. A container it carries is off every collision layer, as a player-held one is, so a slot's Area cannot lock it back (`DeckSlot` also ignores a queued entry for a body on no layer). A container it carries at save time is saved as a loose container, and the restocker reloads at home with empty hands.

**People.** Customers, the cashier and the restocker are drawn by a `HumanFigure` node named `Figure` (`Scripts/Features/Shop/Components`). It wears one look from `Data/Shop/PeopleLooks.tres` (a `FigureLooks`: the model paths, the names of the idle, walk, pick-up, hand-over and refuse clips in them, and the yaw that turns a model to face the body's -Z), scaled to `Height` (1.7 m) with its feet on the node's origin. A customer draws its look from the briefing's seeded random source after every other draw (`CustomerBriefing`), so a seed still plans the same visit; the restocker and the cashier wear the fixed `Look` set in their scenes. The body's owner calls `Follow` every physics frame: the body turns toward its walk, or, standing, toward the point it was told to face, at most `TurnDegreesPerSecond`, and the figure loops idle or walk, the walk played at the body's speed (`FigureLooks.WalkClipSpeed`). `Act` plays a one-shot clip and returns its length: a customer stands still that long to pick an item up, hand its basket over or refuse a price (`Customer.ActThen`), and the cashier acts on each scan and payment without waiting. The rules are the Godot-free `FigureMotion`. A look whose model file is missing logs a warning and keeps the capsule `Placeholder`, so the scenes still load without the assets.

### Workshop

The workshop (`Scenes/Components/Workshop.tscn`, instanced in `ShopScene.tscn`) is one 40 x 40 m warehouse: the cabinet room in the centre, a 3.2 m hallway or hall leaving each of its sides, ordering rooms (order office, delivery bay) to the west and build rooms to the east. The backoffice seam (a `PortalDoorway`, `Scenes/Portal/PortalDoorway.tscn`) only shows while the player stands in the backoffice holding a special card (press 4 in the shop to spawn one) and leads to `WorkshopEntry`; the always-open `ReturnSeam` on the cabinet room's south wall leads back to `World/Markers/ShopReturn`. `ReturnSeam` is a node of `ShopScene.tscn` (a `PortalDoorway` instance under `World/Workshop`), not of `Workshop.tscn`, because the shop decides where the door leads.

The walls are a 0.2 m `GridMap` (`WorkshopGrid`) baked from the spec in `Scripts/Features/Workshop/Models/WorkshopHallLayout.cs`. To change the layout, edit the spec, then press "Bake Layout" on the `WorkshopGrid` node in the inspector and save the scene; `WorkshopGridSceneContractTest` fails while the baked cells differ from the spec. Section markers under `WorkshopGrid/Sections` carry each room's sign (a hanging two-sided `SignBoard` that faces along the doorway the player enters through from the cabinet room) and light.

The cabinet, holders, button and wires are `Scenes/Workshop/CabinetAssembly.tscn`, shared by the workshop's cabinet and the orderable `ArcadeCabinet`. The map screens stay on the workshop cabinet only, because they are wired to its `DeckbuilderController`.
