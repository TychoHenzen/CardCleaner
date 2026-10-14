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
or through the editor's FileSystem dock). A new folder of scripts or shaders outside those roots needs its own negation
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

The project uses a custom service locator pattern, split into a static lookup and a composition root:

- **ServiceLocator** (`Scripts/Core/DependencyInjection/ServiceLocator.cs`): static lookup over the DI container.
  It names no feature or composition type; `DependencyInjectionBoundaryTest` enforces that.
- **CompositionRoot** (`Scripts/Composition/CompositionRoot.cs`): the "Services" autoload. It registers the core
  services, then every node in the `service_providers` group, then runs the pending callbacks. Only that autoload
  composes; any other instance logs an error and changes nothing.
- **ServiceContainer**: Supports singleton registrations
- **IServiceProvider**: Nodes implementing this interface and added to the `service_providers` group will have their services registered at startup
- Services can use async resolution via `ServiceLocator.Get<T>(callback)` for services not yet registered

Access services via `ServiceLocator.Get<T>()` or `ServiceLocator.Container.Resolve<T>()`. Tests rebuild the container
with `CompositionRoot.ReinitializeServices()` and clear it with `ServiceLocator.ResetForTesting()`.

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
It measures twice and fails if the two reports differ. It then ratchets five gated numbers (`up_edges`,
`biggest_loop`, `seams_leaky`, `seams_missing`, `refs_from_non_ui_to_ui`) against
`Tests/Architecture/dependency-baseline.json` in that priority order (#299). The first number that differs from its
baseline decides; if no number differs, the test passes. If it went up, the test fails naming it (every other rise is
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
- `WfcSolver.cs`: Core WFC algorithm (cell selection, collapse, propagation)
- `WfcPropagator.cs`: Constraint propagation using adjacency rules
- `WfcTileSelector.cs`: Weighted tile selection with soft constraints
- `Constraints/AutoTileGapConstraint.cs`: Prevents adjacent different auto-tiles (hard constraint)
- `Constraints/SpatialCoherenceConstraint.cs`: Encourages contiguous tile regions

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
- **Packs/**: Card containers the player opens (box of 8 packs, pack of 8 boosters, booster of 8 cards); `CardPackGenerator` makes each card special with probability 1/512 and special cards show their signature along the card edges
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
- `Z:\Tycho\Projects\Assets` is our internal asset collection. Whenever new assets are needed, look there first. The Synty packs are under `Z:\Tycho\Projects\Assets\CardCleanerAssets` (mostly zips; extract one into the sync script's source root, `../CardCleanerAssets` or `CARDCLEANER_ASSET_SOURCE`, before listing its files in `tools/synty-assets.json`).

### Shop Scene and Licensed Assets

`Scenes/Gameplay/ShopScene.tscn` is the graybox shop (storefront, storage, backoffice). Launch it directly (editor F6); `StartScene.tscn` stays the main scene. Wall, floor, door and counter art comes from the licensed Synty packs. Only the files listed in the manifest are committed, and only in the private submodule, never in this repo:

- To add Synty files, list them in `tools/synty-assets.json`, run `tools/sync-assets.ps1`, open the project in Godot so it imports the FBX files, then commit the new files and their `.import` files inside `Assets/`. The script copies into `Assets/Synty/`. Set `CARDCLEANER_ASSET_SOURCE` to override the source root (default `../CardCleanerAssets`). A missing root, file or uninitialised submodule is an error.
- Each shop issue adds its own manifest entries.
- `ShopArtSlot` nodes load a pack mesh at runtime and hide their graybox placeholder. Without the import the scene still loads and shows the placeholders. The script is a `[Tool]`, so the editor viewport shows the real art too; the placeholder is restored around every editor save (`NotificationEditorPreSave` / `PostSave`) so no scene ever stores it hidden, and the loaded `Art` child has no owner and is never saved. Changing `ArtPath` or `ArtScale` in the inspector reloads the preview. The pack FBX files have no usable texture and are single-sided, so a slot applies its pack's colour atlas on a double-sided material (`PackMaterialCatalog` maps pack folder to atlas; add a new pack there). Collision is hand-authored and independent of the art.

### Backoffice PC Ordering

The PC in the shop backoffice (`World/Markers/PcLocation/PcTerminal`, a child of the PC marker, an `OrderTerminal`) opens an ordering screen (`OrderTerminalUi`) for the `OrderCatalog` assigned to its `Catalog` export (`Data/Shop/BackofficeCatalog.tres`). Ordering uses `IMoneyService` (balance, `TrySpend`) and `IOrderingService`, both nodes under the scene's `Services` provider. A successful order deducts the price and instances the item scene (`Scenes/Shop/Items/*.tscn`) at the `DeliveryPoint` marker, in a grid of slots that never overlap. To sell different items elsewhere (for example a workshop terminal), add an `OrderTerminal`, an `OrderTerminalUi` and a new `OrderCatalog` resource; no code changes. While the screen is open `PlayerController.ControlEnabled` is false and the cursor is visible; closing it (button or Escape) restores both. The starting balance is `MoneyService.StartingBalance`; the balance is not saved.

### Selling Cards at the Register

The shop's checkout counter (`World/Storefront/CheckoutCounter`, a `SaleRegister`) sells one card per interaction: the card the player is holding (`PlayerHolder`), otherwise a card on the nearest shelf. A label above the counter shows the balance and the last result. Shelf scenes (`Scenes/Shop/Items/Shelf.tscn`, a `CardShelf`) have three one-card `DeckSlot` slots: drop a card on a slot to stock it, pick it up to take it back. `CardShelf` joins the `card_shelves` group, so shelves ordered at the PC need no wiring. `CardPricing.GetPrice` is the pure price function: every card sells for `FixedCardPrice` (special-card pricing is out of scope). `CardSale.TrySell` pays into `IMoneyService`, frees the card and refuses a second sale of the same card. `DeckSlot` only locks `CardController` bodies, so ordered boxes are ignored. The shop scene has the card services and a `CardSpawner` (keys 1, 2, 3 spawn random cards, key 4 spawns one special card) so cards can be spawned for testing.

### Workshop

The workshop (`Scenes/Components/Workshop.tscn`, instanced in `ShopScene.tscn`) is one 40 x 40 m warehouse: the cabinet room in the centre, a 3.2 m hallway or hall leaving each of its sides, ordering rooms (order office, delivery bay) to the west and build rooms to the east. The backoffice seam (a `PortalDoorway`, `Scenes/Portal/PortalDoorway.tscn`) only shows while the player stands in the backoffice holding a special card (press 4 in the shop to spawn one) and leads to `WorkshopEntry`; the always-open `ReturnSeam` on the cabinet room's south wall leads back to `World/Markers/ShopReturn`. `ReturnSeam` is a node of `ShopScene.tscn` (a `PortalDoorway` instance under `World/Workshop`), not of `Workshop.tscn`, because the shop decides where the door leads.

The walls are a 0.2 m `GridMap` (`WorkshopGrid`) baked from the spec in `Scripts/Features/Workshop/Models/WorkshopHallLayout.cs`. To change the layout, edit the spec, then press "Bake Layout" on the `WorkshopGrid` node in the inspector and save the scene; `WorkshopGridSceneContractTest` fails while the baked cells differ from the spec. Section markers under `WorkshopGrid/Sections` carry each room's sign and light.

The cabinet, holders, button and wires are `Scenes/Workshop/CabinetAssembly.tscn`, shared by the workshop's cabinet and the orderable `ArcadeCabinet`. The map screens stay on the workshop cabinet only, because they are wired to its `DeckbuilderController`.
