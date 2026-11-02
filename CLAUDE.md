# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project Overview

CardCleaner is a Godot 4.4+ game built with C# (.NET 8.0) featuring a card-based deckbuilder with procedural world generation. Cards have an 8-dimensional "signature" system representing elemental properties that influence gameplay, combat, and loot generation.

## Build & Test Commands

```bash
# Build the project
dotnet build

# Run all tests (requires Godot runtime)
dotnet test

# Run tests with verbose output
dotnet test --logger "console;verbosity=detailed"

# Run a specific test class
dotnet test --filter "FullyQualifiedName~CardSignatureTest"

# Run the whole gdUnit suite the way CI does (Windows PowerShell 5.1 or PowerShell 7)
powershell -NoProfile -File Scripts/ci/Run-GdUnit.ps1 -GodotBinary <path to Godot_v4.7.1-stable_mono_win64.exe>

# Only the [TestCategory("Unit")] suites, as CI runs on pull requests and pushes to master
powershell -NoProfile -File Scripts/ci/Run-GdUnit.ps1 -GodotBinary <path to Godot exe> -Fast

# One suite through the same runner (watchdog and resume included), reports and logs into reports/<name>
powershell -NoProfile -File Scripts/ci/Run-GdUnit.ps1 -GodotBinary <path to Godot exe> -Suite res://Tests/Architecture/DependencyGateTest.cs -ReportDirectory reports\single

# Runner fixture: runs Run-GdUnit.ps1 against a fake Godot, so no Godot is needed (CI runs it too)
powershell -NoProfile -ExecutionPolicy Bypass -File Scripts/ci/Test-RunGdUnit.ps1
```

A direct `GdUnitCmdTool.gd` run that passes a relative `-rd` (as `Run-GdUnit.ps1` does with `-rd reports/ci`) must
also pass `-rc 100000`, or gdUnit's report cleanup doubles every delete path and floods the log with `ERROR: Delete`
lines. A run without `-rd` uses gdUnit's absolute default report path, where cleanup works and needs no `-rc`.

CI scope: pull requests and pushes to `master` run only the Unit suites (`-Fast`). The full suite runs on the weekly
schedule (Sunday 03:00 UTC) and on a manual dispatch with `full=true`, for example `gh workflow run ci.yml --ref
<branch> -f full=true`. PR CI also fails when the latest scheduled run on `master` is not green. So a green PR or merge
run does not prove the full suite; dispatch `full=true` on the branch (or tag) whose head needs it, e.g.
`gh workflow run ci.yml --ref <branch> -f full=true`.

Tests use gdUnit4 framework and require the Godot runtime (`[RequireGodotRuntime]` attribute). Test files are in `Tests/` mirroring the `Scripts/` structure.

gdUnit finds a C# suite by its file name, so a `[TestSuite]` class must be named after its file (`TestSuiteNamingTest`
enforces this). `Run-GdUnit.ps1` fails when a `[TestSuite]` file never runs. When Godot prints nothing for
`-StallSeconds` (180 by default) it kills Godot, names the suite it stalled in, and reruns only the unfinished suites
(at most `-MaxResumes` times). A Godot crash gets the same treatment: an exit code gdUnit does not return (anything but
0, 100, 101, 103 or 104) from an attempt that wrote no results report is named with its suite, logged in hex (for
example `0xC000001D`), and the unfinished suites are resumed. Stalls and crashes share the `-MaxResumes` budget; a
second interruption in the same suite, or a resume that finishes no suite, fails the run. Exits 100, 101, 103 and 104
keep their own handling and never resume, and an abnormal exit after a green report still fails. Reports and logs go to
`reports/ci`; before the first Godot attempt, stale generated attempt logs directly in that directory matching
`godot.stdout.log`, `godot.stderr.log`, `godot.resume*.stdout.log`, or `godot.resume*.stderr.log` are removed, while
report folders and other files survive. Each attempt then has one log pair (`godot.stdout.log`, then
`godot.resume<n>.stdout.log`). Every stall or crash also appends a line such as `crash 0xC000001D in <suite> (logs: ...)`
to `reports/ci/interruptions.txt` (a marker left by an earlier run is removed at start). CI uploads `reports/ci`
as the `gdunit-reports` artifact on failure, on the
weekly and `full=true` runs, and whenever that marker exists, so a crash inside a run that resumed to green keeps its
logs. `Test-RunGdUnit.ps1` runs the real runner against a fake Godot and covers its build wait (a build that leaves a
worker running), build failure, watchdog, resume, repeated-stall, crash, repeated-crash, exit-code, interruption-marker
and report-aggregation paths. It also checks that the suite-detection patterns in `Run-GdUnit.ps1` match the ones in
`TestSuiteNamingTest.cs`.

The runner prints its results to the job log, so no artifact download is needed for them: one `Suite result: <suite> cases=<n> errors=<e> failures=<f>` line per finished suite (also for suites finished in an attempt that later stalled or crashed), a `FAILED <suite> > <case>: <message>` line for each failing case before it throws, every `dependency gate:` line from all attempt logs, and a `Run summary:` line. `-Suite` takes res:// paths of `[TestSuite]` files and cannot be combined with `-Fast`. `-ReportDirectory` is relative and must lie below `reports/` (default `reports\ci`, which CI uses). A suite that stalls twice fails the run and is named.

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
  (`Host`) and registers the plain `[Service]` classes of `CardCleaner.Scripts` lazily into it, then the core
  services, then every node in the `service_providers` group, then runs the pending callbacks. It also sets
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
  every call, and `WorldMapComponents` passes the loaded mapping to `TerrainTransitions.Create`. `MeshStatistics` (a
  readonly struct) and `AutoTileFormatDefinition` (a sealed record) are immutable: every property is set once.
- **IServiceProvider**: Nodes implementing this interface and added to the `service_providers` group will have their services registered at startup
- **Tree injection**: a node implementing `IDependent` (`Scripts/Core/Interfaces`) gets `Inject(IServiceResolver)`
  from the autoload on `SceneTree.NodeAdded`: after the node's `_EnterTree`, before its `_Ready`, and again whenever it
  re-enters the tree (reparent). `Inject` only stores the resolver; the node does its wiring in `_Ready` through it.
  Nodes already in the tree when the autoload starts are injected once by a startup pass
  (`CompositionRoot.InjectExistingNodes`), which runs after their `_Ready`, so an `IDependent` must never be an
  autoload ordered before Services.
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

`Tests/Architecture/DependencyGateTest.cs` (a Unit suite, so `-Fast` and PR CI run it) measures `Scripts/` as a
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
It measures twice and fails if the two reports differ. It then ratchets four gated numbers (`up_edges`,
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

Uses the Saveable addon (`addons/saveable/`) with Newtonsoft.Json. Implement `ISaveable` interface with `UniqueID`, `Load(NodeSave)`, and `Save(NodeSave)` methods. The `GameSaveService` handles auto-saving of game state.

### Feature Organization

Code is organized by feature under `Scripts/Features/`:
- **Card/**: Card models, controllers, components, and services (spawning, generation)
- **Deckbuilder/**: Game session management, map generation, combat, exploration
- **Worldgen/**: Procedural generation support (auto-tiling, biomes, gradients, blob generation)
- **Player/**: Player controller and interaction system
- **Packs/**: Card containers the player opens (box of 8 packs, pack of 8 boosters, booster of 8 cards); `CardPackGenerator` makes each card special with probability 1/200 (about 92% of boxes hold at least one) and special cards show their signature along the card edges
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
- `Z:\Tycho\Projects\Assets` is our internal asset collection and the Asset Manager workspace. Whenever new assets are needed, look there first. Assets are added with the Asset Manager editor tab (`addons/asset_manager`): it exports to `res://Assets/<Type>/` (export roots under `[asset_manager]` in `project.godot`). Export the individual assets you need; never copy a whole source pack into the repo. The Synty packs are under `Z:\Tycho\Projects\Assets\CardCleanerAssets` (mostly zips).

### Shop Scene and Licensed Assets

`Scenes/Gameplay/ShopScene.tscn` is the graybox shop (a storefront at x 0..16, z 0..12; one storage and backoffice room along its back and right sides, 24 m wide at the back; a road door in the right arm at x 18.05 that joins the room to the street; and a locked expansion lot to the left of the storefront, sealed by the single wall at x = 0). `ShopScene.tscn` is the main scene, so the game starts here. Wall, floor, door and counter art comes from the licensed Synty packs. Only the Synty files exported through the Asset Manager are committed, and only in the private submodule, never in this repo:

- To add Synty files, export them through the Asset Manager tab (export roots are in `project.godot`), open the project in Godot so it imports the FBX files, then commit the new files and their `.import` files inside `Assets/` first and the `Assets` pointer here second.
- Each shop issue exports its own assets.
- `ShopArtSlot` nodes load a pack mesh at runtime and hide their graybox placeholder. Without the import the scene still loads and shows the placeholders. The script is a `[Tool]`, so the editor viewport shows the real art too; the placeholder is restored around every editor save (`NotificationEditorPreSave` / `PostSave`) so no scene ever stores it hidden, and the loaded `Art` child has no owner and is never saved. Changing `ArtPath` or `ArtScale` in the inspector reloads the preview. The pack FBX files have no usable texture and are single-sided, so a slot applies its pack's colour atlas on a double-sided material (`PackMaterialCatalog` maps pack folder to atlas; add a new pack there). Collision is hand-authored and independent of the art.

**Street and neighbourhood.** `Scenes/Shop/Neighbourhood.tscn` is visual only: it has no collision. Its `ShopArtSlot` nodes load the POLYGON Mini City pack from `Assets/Synty/PolygonMiniCity`, and the kerb is the only primitive mesh (the sky is set in `ShopScene.tscn`). It sits outside the street bounds and the workshop footprint, and `NeighbourhoodSceneTest` enforces both.

### Backoffice PC Ordering

The PC in the shop backoffice (`World/Markers/PcLocation/PcTerminal`, a child of the PC marker, an `OrderTerminal`) opens an ordering screen (`OrderTerminalUi`) for the `OrderCatalog` assigned to its `Catalog` export (`Data/Shop/BackofficeCatalog.tres`). Ordering uses `IMoneyService` (balance, `TrySpend`) and `IOrderingService`, both nodes under the scene's `Services` provider. A successful order deducts the price and instances the item scene (`Scenes/Shop/Items/*.tscn`) at the `DeliveryPoint` marker, in a grid of slots that never overlap. To sell different items elsewhere (for example a workshop terminal), add an `OrderTerminal`, an `OrderTerminalUi` and a new `OrderCatalog` resource; no code changes. While the screen is open `PlayerController.ControlEnabled` is false and the cursor is visible; closing it (button or Escape) restores both. The starting balance is `MoneyService.StartingBalance`; the balance is not saved.

### Selling Cards at the Register

The shop's checkout counter (`World/Storefront/CheckoutCounter`, a `SaleRegister`) sells one card per interaction: the card the player is holding (`PlayerHolder`), otherwise a card on the nearest shelf. A label above the counter shows the balance and the last result. Shelf scenes (`Scenes/Shop/Items/Shelf.tscn`, a `CardShelf`) have three one-card `DeckSlot` slots: drop a card on a slot to stock it, pick it up to take it back. `CardShelf` joins the `card_shelves` group, so shelves ordered at the PC need no wiring. `CardPricing.GetPrice` is the pure price function: every card sells for `FixedCardPrice` (special-card pricing is out of scope). `CardSale.TrySell` pays into `IMoneyService`, frees the card and refuses a second sale of the same card. `DeckSlot` only locks `CardController` bodies, so ordered boxes are ignored. The shop scene has the card services and a `CardSpawner` (keys 1, 2, 3 spawn random cards, key 4 spawns one special card) so cards can be spawned for testing.

### Workshop

The workshop (`Scenes/Components/Workshop.tscn`, instanced in `ShopScene.tscn`) is one 40 x 40 m warehouse: the cabinet room in the centre, a 3.2 m hallway or hall leaving each of its sides, ordering rooms (order office, delivery bay) to the west and build rooms to the east. The backoffice seam (a `PortalDoorway`, `Scenes/Portal/PortalDoorway.tscn`) only shows while the player stands in the backoffice holding a special card (press 4 in the shop to spawn one) and leads to `WorkshopEntry`; the always-open `ReturnSeam` on the cabinet room's south wall leads back to `World/Markers/ShopReturn`. `ReturnSeam` is a node of `ShopScene.tscn` (a `PortalDoorway` instance under `World/Workshop`), not of `Workshop.tscn`, because the shop decides where the door leads.

The walls are a 0.2 m `GridMap` (`WorkshopGrid`) baked from the spec in `Scripts/Features/Workshop/Models/WorkshopHallLayout.cs`. To change the layout, edit the spec, then press "Bake Layout" on the `WorkshopGrid` node in the inspector and save the scene; `WorkshopGridSceneContractTest` fails while the baked cells differ from the spec. Section markers under `WorkshopGrid/Sections` carry each room's sign and light.

The cabinet, holders, button and wires are `Scenes/Workshop/CabinetAssembly.tscn`, shared by the workshop's cabinet and the orderable `ArcadeCabinet`. The map screens stay on the workshop cabinet only, because they are wired to its `DeckbuilderController`.
