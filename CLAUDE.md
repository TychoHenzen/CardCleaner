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

# Run the Godot import even though no file it reads changed (the runner skips it then, see the import skip below)
powershell -NoProfile -File Scripts/ci/Run-GdUnit.ps1 -GodotBinary <path to Godot exe> -SkipDaily -ForceImport

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

`Run-GdUnit.ps1` and the one-liners below pass no `-rc`, so gdUnit keeps its newest 20 `report_<n>` folders in a report
folder. The vendored gdUnit4 (6.2.3) cleans a relative `-rd` folder correctly, so `-rd reports/ci` logs no `ERROR: Delete`
lines.

**Project patches in the vendored addon.** `addons/gdUnit4/src/dotnet` carries two. `SuiteStartWatchdog` (issue 448,
gdUnit4Net issue 397) starts a suite again when its start event is lost, three attempts in all; no `[Before]` hook may
await, because gdUnit sends a suite's first event after that hook. `SuiteDiscoveryCache` answers C# test discovery from
one read of the test assembly instead of one Mono.Cecil read per script (about 17 s a run). Reapply both files, and the
call in `GdUnit4CSharpApi.DiscoverTests`, after updating the addon or the `gdUnit4.api` package. `SuiteStartGuardTest` and
`SuiteDiscoveryGuardTest` fail when a patch is dropped; the Daily `SuiteDiscoveryEquivalenceTest` compares the cache with
the package for every script.

**asset_manager is a synced copy of our fork** (`TychoHenzen/Asset-Manager`, upstream `Kivylius/Asset-Manager`). Every
change to `addons/asset_manager`, ours included, is made in the fork first, then copied with
`node tools/asset-manager/sync.mjs` (`--source=<dir>` for the local clone, `--commit=<ref>` for another commit). The copy
is staged and `tools/asset-manager/pin.json` records the commit and every file's hash; the sync refuses while the addon
differs from the old pin. `node tools/asset-manager/sync.test.mjs` runs it against a throwaway fork. `AssetManagerVendorPinTest`
and `AssetManagerLazyOpenTest` fail when the addon drifts from the pin or a fork update drops one of our edits.

**Without PowerShell.** Start Godot from Bash through node, so no console window opens:

```bash
# One suite, the same arguments as Run-GdUnit.ps1 uses for a single suite (reports/<dir> is relative; gdUnit keeps its newest 20 report folders, see above)
node -e "const r=require('child_process').spawnSync('<godot console exe>',['--headless','--path','.','-s','res://addons/gdUnit4/bin/GdUnitCmdTool.gd','-c','--ignoreHeadlessMode','-rd','reports/<dir>','-a','res://Tests/<path>Test.cs'],{stdio:'inherit',windowsHide:true});process.exit(r.status??1)"

# The .uid import (writes only the sidecars, see Godot .uid Sidecars)
node -e "const r=require('child_process').spawnSync('<godot console exe>',['--headless','--path','.','--import'],{stdio:'inherit',windowsHide:true});process.exit(r.status??1)"
```

These give one suite and the .uid import when powershell.exe cannot start. The one-liners exit with Godot's own status,
and non-zero (1) when Godot cannot start, so a missing exe fails the command. They have no watchdog, no resume and no suite
aggregation, so Run-GdUnit.ps1 stays the supported path for a full or -SkipDaily run.

**Engine settings the tests pin.** Change none of these without reading its pinning test:

- Physics runs on the main thread (`physics/3d/run_on_separate_thread=false`, `PhysicsThreadSettingTest`). On Godot 4.7.1
  the separate physics thread froze the whole process (godotengine/godot issue 123654). Turn it back on only on an engine
  version that contains the fix (pull request 124990).
- Navigation maps build synchronously (`navigation/world/map_use_async_iterations=false`, `NavigationMapSyncSettingTest`),
  so the sync that applies a rebaked region also builds the map from it.
- Cards rest on top of the floor (`physics/jolt_physics_3d/simulation/penetration_slop=0.001`, `CardFloorRestTest`). The
  card's `CardSleepEnforcer` freezes a card only after `RestSeconds` (0.5 s) below its thresholds, or at once when Jolt puts
  it to sleep (`ACardJoltPutsToSleepIsFrozen`).
- Every C# class that derives from `Resource` derives from `PinnedResource` instead (`Scripts/Core/Resources`,
  `PinnedResourceTest`), which keeps a loaded resource referenced for the rest of the run (godotengine/godot issue 83762
  stopped debug builds in a finalizer). Nothing may load on a worker thread (`ResourceLoader.LoadThreadedRequest`). Drop the
  base class only on an engine version that contains a fix for that issue.

CI scope: pull requests and pushes to `master` run the unit tier and the pull-request tier (`-SkipDaily`). Every suite
runs on the daily schedule, on a pull request while the latest daily run is red (see Test tiers), and on a manual
dispatch with `full=true`. A green pull request therefore does not prove the Daily suites; dispatch `full=true` on the
branch (or tag) whose head needs them, e.g. `gh workflow run ci.yml --ref <branch> -f full=true`.

CI downloads Godot through `Scripts/ci/Install-Godot.ps1`, which retries a failed download or unzip for 3 attempts in all
and warns `Godot download attempt <n> of 3 failed: <error>` for each failure. A daily run that still fails for such a
reason makes pull requests run every suite until `gh run rerun <run-id> --failed` turns it green.

Tests use the gdUnit4 framework. The suites in `Tests/` require the Godot runtime (`[RequireGodotRuntime]`); the suites in
`UnitTests/` run without it. Both mirror the `Scripts/` structure. gdUnit finds a C# suite by its file name, so a
`[TestSuite]` class must be named after its file (`TestSuiteNamingTest`).

**The runner.** `Run-GdUnit.ps1` is the supported way to run gdUnit suites. It fails when a `[TestSuite]` file never runs.
When Godot prints nothing for `-StallSeconds` (180 by default) it kills Godot, names the suite, and reruns only the
unfinished suites (at most `-MaxResumes` times); a Godot crash (an exit code gdUnit does not return) is handled the same
way, and a second interruption in one suite fails the run. Reports and logs go to `reports/ci`, or to `reports/<name>`
for `-ReportDirectory` (relative, below `reports/`; the runner keeps the 10 newest such folders). `-Suite` takes
`res://` paths of `[TestSuite]` files and cannot be combined with `-SkipDaily`. The job log carries a `Suite result:` line
per suite, a `FAILED <suite> > <case>:` line per failing case and a `Run summary:` line, so no artifact download is needed
to read results. `Test-RunGdUnit.ps1` runs the real runner against a fake Godot: edit the runner together with that fixture.

**The import skip.** Before the tests the runner runs Godot's editor import (`--headless --editor --recovery-mode
--import`), but only when a file the import reads changed: it fingerprints the project files Godot scans by path, size
and write time (a `.cs` or `.md` file by path only). The log says `Godot import skipped`, or why the import ran;
`-ForceImport` always runs it. A test therefore writes its files under `user://`, never into the project, because
rewriting a project file with the same bytes changes its write time and costs the next run an import.
`reports/.gdignore` is committed with `git add -f`, since `.gitignore` ignores `reports`.

### Driving the Rendered Game (e2e)

`node tools/e2e/e2e.mjs` runs the real, rendering game on a hidden Win32 desktop (no window ever reaches the user's
screen) and drives it over a local socket: keys, mouse, `walk_to`, `look_at`, `use`, scene inspection and
screenshots into `reports/e2e/<session>/shots/`. Use it to look at a change in-game; see `tools/e2e/README.md`.
Starting a session keeps 5 session folders, the started one included, and removes the older stopped ones with their
save data and screenshots; it never removes a running session or the launcher in `reports/e2e/bin`
(`node tools/e2e/e2e.test.mjs`, run by the `runner-fixture` CI job, checks that). The
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
- Nodes with editor preview features (for example `ShopArtSlot`, see the preview exception below)
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

Text in the world is printed on something, never floating in the air or turned to the camera. Use `SignBoard`
(`Scripts/Features/Signage`): a board with its text printed flat on the face, fixed to a wall, hung or stood on a post,
or as a `Screen` whose text glows. It builds its board, faces and mount as internal children that no scene stores, so a
scene holds only its exports. `NoFloatingTextSceneTest` fails on a billboard `Label3D` in any scene outside `Scenes/Debug`.

### Godot .uid Sidecars

Godot 4.4+ gives every script and shader a `.uid` sidecar, and scenes reference them by that UID. Sidecars under
`Scripts/`, `Tests/` and `Shaders/` are committed (`.gitignore` negates `*.uid` for those roots), so a fresh clone keeps
the same UIDs; `addons/` sidecars stay ignored, except `addons/asset_manager`'s, which the fork commits and its sync
tool force-adds with the rest of the copy. Move or rename a source together with its `.uid` (both in one `git mv`,
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

The project uses a custom DI container with no static service state: `ServiceHost` (`Scripts/Core/DependencyInjection`)
owns the container and the pending-callback queue and names no feature or composition type
(`DependencyInjectionBoundaryTest`), and the `CompositionRoot` autoload ("Services", `Scripts/Composition`) composes it. Nodes
reach services only through the `IServiceResolver` they are injected with (`Get<T>()`, `Has<T>()`, `Get<T>(callback)`);
nothing in `Scripts/` looks a container up statically.

- **Scopes.** The app scope (the plain `[Service]` classes, then the core services) is composed in the autoload's `_Ready`
  and lives for the whole run. The scene scope (every node in the `service_providers` group, implementing
  `IServiceProvider`) is registered after startup and on every `SceneTree.SceneChanged`, and dropped when the scene leaves
  the tree. Only the autoload composes and injects.
- **Injection.** A node implementing `IDependent` gets `Inject(IServiceResolver)` after its `_EnterTree` and before its
  `_Ready`, and again when it re-enters the tree. `Inject` only stores the resolver; wire in `_Ready`. An `IDependent` must
  never be an autoload ordered before Services.
- **Scene services.** A node that needs a provider-registered service uses `Get<T>(callback)`. A scanned `[Service]` class
  must never stand in for a node a scene provides, since a scene node could resolve it before the provider registers.
- **Plain `[Service]` classes** are found by `ServiceClassScan` and built once, on first resolve, through the public
  constructor with the most parameters, each resolved from the container. `[Service]` nodes are never built by the
  container; `AutoServiceProvider` registers the live node. The dependency gate counts a static factory class as a
  concrete crossing, so build per-session parts from registered factories (`ISessionFactory`, `ICombatSystemFactory`).
- **Logging and math.** Domain code logs through `ILog`, never `GD`, and calls `CoreMath`, not `Mathf`. Add a `CoreMath`
  member only together with its first caller (`CoreMathTest` compares each with `Godot.Mathf`). `CardSignature` stays pure
  data in the gate, so it uses `System.Math` / `MathF`.
- **Names.** UI code names the contracts `ISessionWorld`, `IExplorationSession` and `ICombatSystem`, never the concrete
  classes. Map generators take an `ITileCatalog`, never `TileCatalogData` (`TileCatalogConsumersTest`). Outside
  `Scripts/Features/Worldgen/Wfc`, code holds `IWfcMapGenerator` or `IWfcTerrainSolver` (and their factory interfaces),
  never `WfcMapGenerator` or `WfcAdjacencyRules`.
- **Tests** reach the autoload's host through `Tests/TestUtilities/TestServices` (`Container`, `Resolver`,
  `Reinitialize()`, `Reset()`, `RegisterGameServiceClasses(container)`, `ExecutePendingCallbacks()`, `Inject(node)`). A unit
  test of the host itself builds one with `new ServiceHost()`.

### Dependency Gate

`Tests/Architecture/DependencyGateTest.cs` (pull-request tier) measures `Scripts/` as a layered architecture on every run:
UI on top, pure contracts and pure data below, the middle clustered by a deterministic Louvain consensus. It prints
`dependency gate:` lines (module order, loose classes, up edges, loops, leaky or missing seams) and ratchets four gated
numbers (`up_edges`, `biggest_loop`, `seams_leaky_or_missing`, `refs_from_non_ui_to_ui`) against
`Tests/Architecture/dependency-baseline.json`, in that priority order. The first number that differs decides. If it went
up, the test fails naming it. If it went down, the step is accepted, and a rise in a lower-priority number is accepted with
it, but the test still fails until the baseline is re-recorded with the switch set:

```bash
CARDCLEANER_UPDATE_DEPENDENCY_BASELINE=1 <godot console exe> --headless --path . -s res://addons/gdUnit4/bin/GdUnitCmdTool.gd -c --ignoreHeadlessMode -rd reports/ci -a res://Tests/Architecture/DependencyGateTest.cs
```

The switch rewrites the file only when the deciding number went down, and refuses when it went up. A missing or malformed
baseline fails the test and is never written; a first baseline is created by committing the file. A change that moves a
gated number ends with this test green and the baseline re-recorded to what it achieved. When a step moves a number, run
the gate on the base as a control, and try at most three placements of the new class before recording a seam. A
missing-to-leaky relabel is not a rise: only `seams_leaky_or_missing`, their sum, is gated. The baseline also records the
class lists behind each number (`up_edge_pairs`, `biggest_loop_classes`, `seam_classes`, `ui_ref_pairs`); the lists never
fail the gate, and a failure names the classes that joined or left. The baseline records `seams_leaky_or_missing` 0 with an
empty `seam_classes` list. The fixture suites beside the test (`LayerAnchorsTest`, `LouvainClusteringTest`,
`ModuleOrderTest`, `SeamRaterTest`, `GateMetricsTest`, `ClassGraphExtractorTest`, `SceneLinkReaderTest`,
`DependencyBaselineTest`) pin each rule on small graphs.

### Quality Gate

The `quality-guard` pull-request check (`.github/workflows/ci.yml`, dod-guard pinned by SHA) scans the `Scripts` files a
pull request changes, at their merge base and at HEAD. A high finding that exists only at HEAD blocks (for line length that
means above 120 columns, as in `.editorconfig`); a finding already at the merge base never blocks, and new medium findings
are counted and listed, not blocked. The pull request must also remove at least one medium or high finding that was
already in those files, so medium cleanups count toward the quota. The findings are listed in the `quality-gate-report`
artifact, linked from the pull request's single quality gate comment. Tighten the policy with the flags on the gate step
(`--block-on=medium`, `--rule-block-on=<rule>:<severity>`, `--fix-medium-high`, `--fix-low`).

Run the same gate locally with `node .github/quality-gate-local.mjs [--base=<sha>]` on the committed head before pushing a
change under `Scripts/`. It reads the dod-guard pin, root and block-on setting from the `quality-guard` job in `ci.yml`,
applies `.github/quality-guard-godot.patch` to a clone of that pin kept in the system temp directory, and writes
`reports/quality-gate-report.md`. It refuses a dirty working tree: commit or revert first. The patch makes a class marked
`[GlobalClass]` or `[Service]` count as reached (no dead-export finding) and treats a C# `using` directive as an import for
duplicate detection. After moving the dod-guard pin, check that the patch still applies (the job's own patch step fails
when it does not); a patch that changes another scanner file needs that file added to the list in the job's patch step and
in `.github/quality-gate-cache.mjs`. `.github/quality-guard-godot.test.mjs` and `.github/quality-gate-local.test.mjs` pin
the patch and the reading of `ci.yml`, and run in the `quality-guard` job.

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

### Terms

The player reads a `Box` as a cardboard box, a `Pack` as a booster box and a `Booster` as a booster pack
(`CardContainerKindText.NameOf`, display only). The id of a kind (`ProductIds.IdOf`: `cardboard_box`, `pack`, `booster`) is
its catalog item id and its wholesale-cost key, so renaming what the player reads never touches a catalog row or a save.

### Regenerating Committed Meshes

The card body mesh `Data/Card/CardMesh.tres` and the container meshes in `Data/Packs/Meshes/` (booster, pack, cardboard
box) are committed, baked from recipes that are test code (`CardMeshRecipe`, `ContainerMeshRecipe`). Never edit a mesh
`.tres` by hand: edit the recipe, regenerate with the switch set, then run the suite again without it (the drift test can
fail in the regenerating run because it may compare against the old file). `CardMeshBakeTest.CommittedMeshMatchesRecipe`
and `ContainerMeshBakeTest` fail when a committed mesh differs from its recipe.

For the card mesh (change the size constants in `CardMeshBakeTest` too; for a size change also update the `Designer`
`Width`/`Height`/`Thickness`, the `CardCollision` box and the `OutlineBox` mesh size in `CardShader.tscn`, which
`CardShaderSceneTest` checks):

```bash
CARDCLEANER_REGENERATE_CARD_MESH=1 <godot console exe> --headless --path . -s res://addons/gdUnit4/bin/GdUnitCmdTool.gd -c --ignoreHeadlessMode -rd reports/ci -a res://Tests/Features/Card/Components/CardMeshBakeTest.cs
```

For the container meshes:

```bash
CARDCLEANER_REGENERATE_CONTAINER_MESHES=1 <godot console exe> --headless --path . -s res://addons/gdUnit4/bin/GdUnitCmdTool.gd -c --ignoreHeadlessMode -rd reports/ci -a res://Tests/Features/Packs/Components/ContainerMeshBakeTest.cs
```

### World Generation

> **Architecture Document**: See `docs/WORLDGEN_ARCHITECTURE.md` for the complete worldgen architecture, including the Soft WFC approach, weight modifier system, and data definition guides.

**CRITICAL - WFC Soft Constraints and Entropy**:
- **Tiles are NOT removed from possibility sets** - constraints set weights to 0 instead
- `cell.GetPossibleTiles().Count` is MEANINGLESS for entropy - it includes 0-weight tiles
- Entropy MUST be calculated using weighted probabilities from constraint evaluation
- **DO NOT** try to optimize cell selection by counting tiles - this breaks WFC completely
- Any entropy optimization must preserve full constraint weight calculation

### Persistence

Uses the Saveable addon (`addons/saveable/`) with Newtonsoft.Json. Implement `ISaveable` interface with `UniqueID`, `Load(NodeSave)`, and `Save(NodeSave)` methods. The shop's `GameSaveService` autosaves the active slot on a timer and on the pause menu's Save button (`ISaveGameCommand.ManualSave`).

Save slots live under `user://saves/<slot>/save.json` (`SaveSlotLayout`); a save is written to `save.json.tmp` first and
renamed into place, so a crash leaves the previous save intact. Each later stateful slice adds its own saved state to the
envelope.

The game is not released, so no player has an old save to protect. A later change that moves or renames a saved node,
or moves a seat by more than 5 cm, gets no migration step: a save from before it may load that stock loose, or not at
all. New saved state still gets its round-trip test. `LegacyShelfSaveMigration` stays as it is.

### Feature Organization

Code is organized by feature under `Scripts/Features/<Feature>/`. Engine-free utilities and interfaces are in
`Scripts/Core/`.

### Extension Recipes and Test Helpers

- A new unlockable adds its row to `UnlockCatalog.Default` and a test row for it; an id with no row is locked.
- A new daily bill source is one more constructor parameter of `DailyBillBook`; nothing enumerates `IDailyBillSource`.
- A new PC app is one export and one row in `PcDesktop.IconRows` (the icon wiring and `ResetToDesktop` both read it);
  another item list for sale on the desktop is a catalog plus a `StockApp` node.
- A new screen that claims the cursor and freezes the player implements `IModalScreen` and joins the `modal_screens` group
  in `_EnterTree`, so the day report can close it first.
- A new held-item mode publishes its key chips through `IControlHints` (`Publish(source, hints)`) and clears them in
  `_ExitTree`.
- A new pack folder shown by a `ShopArtSlot` needs a `PackMaterialCatalog` row (`ShopArtMaterialSceneTest` fails when a Synty
  model in the shop is in no known folder).
- The workshop walls are baked from `WorkshopHallLayout` (`Scripts/Features/Workshop/Models`): edit the spec, press "Bake
  Layout" on the `WorkshopGrid` node and save the scene (`WorkshopGridSceneContractTest` fails while they differ).
- Tests that render card art effects call `CardEffectNormalMapCache.ResetForTesting()` in their teardown. To compare all
  rarities and tiers side by side, open `Scenes/Debug/CardEffectComparison.tscn` in the editor and press F6.
- In the shop scene, the `CardSpawner` (`World/Cards`) spawns random cards on keys 1, 2 and 3 and one special card on key
  4; the backoffice seam to the workshop shows only while the player holds a special card there.
- Tests open containers in a test hand through `InHandOpening` and wait for a booster's opening with
  `ContainerOpeningWait` (bounded in wall-clock time through `BoundedWait`), or end it with `FinishOpening` when only the
  cards matter.
- A pile costs about 0.65 ms a frame whatever its size, because it draws at most `FacesShown` faces (an empty view took
  0.59 ms, 4 piles 3.7 ms and 9 piles 6.2 ms, with vsync off). `Tests/Features/CardStacks/Probe/PileFrameCostProbe.tscn`
  measures that through the e2e driver (`call Build [piles, cards]`, `call Measure [frames]`, then `get FrameMs` and
  `GpuMs` once `Done`).

## Canonical dod-guard Workflow

- `/add-backlog-idea` creates an issue in **Backlog**.
- `/refine-backlog-item` researches the issue and moves it to **Todo** when it is ready.
- `/next-ticket` implements exactly one issue on one issue branch.
- `/submit-draft-pr` publishes the branch as a draft pull request.
- Review remains read-only until the user explicitly accepts the findings or requests remediation.
- `/complete-pr` owns the ready-to-merge check, merge, linked-issue confirmation, and remote branch deletion.

`openspec/` holds only a template config (`openspec/config.yaml`) and no specs, so no spec file is a source of truth.
Change folders under `openspec/changes/` (archives included) are ignored local scratch and never a source of truth; the
GitHub issue owns the plan. How-to and workflow guidance stays in this file, not in `openspec/`.

### Private Assets Submodule

All art and models live in the private repo `TychoHenzen/cardcleaner-assets`, a git submodule mounted at `Assets/` (so `res://Assets/...` resolves there). It holds the former `Graphics/` folder as `Assets/Graphics/`, the Synty files in `Assets/Synty/`, the card, terrain and material resources, and the Godot `.import` files (tracked there so UIDs stay stable). It does not use Git LFS (about 75 MB, no file over 5 MB); revisit if a single file passes 50 MB.

- Clone with `git clone --recurse-submodules`, or run `git submodule update --init` in an existing clone. An empty `Assets/` means the submodule is not initialised.
- Changing an asset is two commits: commit and push inside `Assets/` first, then commit the updated `Assets` pointer in this repo. Pushing the pointer before the submodule commit breaks CI.
- CI fetches the submodule with the read-only deploy key in the `ASSETS_DEPLOY_KEY` secret (`.github/actions/checkout-assets`). The project cannot load without `Assets/`, so the step fails when the key is unavailable; fork and Dependabot pull requests cannot read the secret and fail there.
- `Z:\Tycho\Projects\Assets` is our internal asset collection and the Asset Manager workspace. Whenever new assets are needed, look there first. Assets are added with the Asset Manager editor tab (`addons/asset_manager`): it exports to `res://Assets/<Type>/` (export roots under `[asset_manager]` in `project.godot`). Export the individual assets you need; never copy a whole source pack into the repo. The Synty packs are under `Z:\Tycho\Projects\Assets\_extracted\3d` (one `POLY_*` folder per pack, each a converted Godot project; the source FBX files of the NewYork and Megapolis packs sit in its `.godot\unidot_temp`, and several packs hold only imported `.scn` caches).

### Licensed Synty Assets

Wall, floor, door and counter art comes from the licensed Synty packs. Only the Synty files exported through the Asset
Manager are committed, and only in the private submodule, never in this repo:

- To add Synty files, export them through the Asset Manager tab (export roots are in `project.godot`), open the project in
  Godot so it imports the FBX files, then commit the new files and their `.import` files inside `Assets/` first and the
  `Assets` pointer here second.
- Each shop issue exports its own assets.
- `ShopArtSlot` nodes load a pack mesh at runtime and hide their graybox placeholder, so a scene still loads without the
  import. A new pack folder needs a `PackMaterialCatalog` row (see Extension Recipes).
