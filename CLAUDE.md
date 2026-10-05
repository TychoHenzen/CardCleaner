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
```

Tests use gdUnit4 framework and require the Godot runtime (`[RequireGodotRuntime]` attribute). Test files are in `Tests/` mirroring the `Scripts/` structure.

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

**The Fix:** Remove `[Tool]` from runtime-only scripts. If `[Tool]` is truly needed, guard `_Ready()`:

```csharp
public override void _Ready()
{
    if (Engine.IsEditorHint()) return;
    // Runtime initialization...
}
```

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

The project uses a custom service locator pattern integrated with Godot's autoload system:

- **ServiceLocator** (`Scripts/Core/DependencyInjection/ServiceLocator.cs`): Godot autoload node registered as "Services" that manages the DI container
- **ServiceContainer**: Supports singleton, transient, and factory registrations
- **IServiceProvider**: Nodes implementing this interface and added to the `service_providers` group will have their services registered at startup
- Services can use async resolution via `ServiceLocator.Get<T>(callback)` for services not yet registered

Access services via `ServiceLocator.Get<T>()` or `ServiceLocator.Container.Resolve<T>()`.

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

### World Generation

> **Architecture Document**: See `docs/WORLDGEN_ARCHITECTURE.md` for the complete worldgen architecture, including the Soft WFC approach, weight modifier system, and data definition guides.

**Active System**: Two-Phase WFC via **SimpleMapGenerator** (`Scripts/Features/Deckbuilder/Services/SimpleMapGenerator.cs`)

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
- Each visual tile samples 4 data corners → 4-bit bitmask (Corner16 format)
- The gap constraint ensures each visual tile sees at most ONE auto-tile type
- `Dominance` property on tiles determines which terrain renders "on top"

**Gradient Systems**:
- `CardBasedGradient`: Creates gradients from input cards (sphere/capsule/Bezier)
- `RadialGradient`: Center-to-edge signature blending
- `NoiseGradient`: FastNoiseLite-based variation
- Gradients influence biome selection via `BiomeAffinityConstraint`

### Game Session Flow

The `GameSessionService` manages the game loop through states: `WaitingForCards` → `GeneratingMap` → `Exploring` → `InCombat` → `GeneratingLoot` → `SessionComplete`. Key supporting systems:

- **SimpleMapGenerator**: Creates tile-based maps influenced by card signatures
- **ExplorationAI**: A* pathfinding for autonomous exploration
  - Operates in two modes: `FrontierExploration` and `PathToEnemy`
  - Emits events for path updates, visited tiles, player movement, and enemy encounters
- **FrontierExplorationBehavior**: Intelligent exploration using frontier expansion
  - Tracks seen vs. visited tiles using visibility checking
  - Automatically marks trivially visible tiles as visited
- **IVisibilityChecker**: Interface for line-of-sight calculations with configurable vision range (default 5)
- **SimpleCombatSystem**: Turn-based combat using card signatures

**Debug Visualization**: Overlay layer system allows non-destructive visualization of exploration data (current path, target tile, visited tiles).

**Key Exploration Events**: `PathUpdated`, `VisitedTilesUpdated`, `PlayerMoved`, `EnemyEncountered`, `EnemySpotted`

### Persistence

Uses the Saveable addon (`addons/saveable/`) with Newtonsoft.Json. Implement `ISaveable` interface with `UniqueID`, `Load(NodeSave)`, and `Save(NodeSave)` methods. The `GameSaveService` handles auto-saving of game state.

### Feature Organization

Code is organized by feature under `Scripts/Features/`:
- **Card/**: Card models, controllers, components, and services (spawning, generation)
- **Deckbuilder/**: Game session management, map generation, combat, exploration
- **Worldgen/**: Procedural generation support (auto-tiling, biomes, gradients, blob generation)
- **Player/**: Player controller and interaction system
- **Conveyor/**: Conveyor belt mechanics
- **Pause/**: Pause menu controller

Core utilities and interfaces are in `Scripts/Core/`.

## Canonical dod-guard Workflow

- `/add-backlog-idea` creates an issue in **Backlog**.
- `/refine-backlog-item` researches the issue and moves it to **Todo** when it is ready.
- `/next-ticket` implements exactly one issue on one issue branch.
- `/submit-draft-pr` publishes the branch as a draft pull request.
- Review remains read-only until the user explicitly accepts the findings or requests remediation.
- `/complete-pr` owns the ready-to-merge check, merge, linked-issue confirmation, and remote branch deletion.

### Shop Scene and Licensed Assets

`Scenes/Gameplay/ShopScene.tscn` is the graybox shop (storefront, storage, backoffice). Launch it directly (editor F6); `StartScene.tscn` stays the main scene. Wall, floor, door and counter art comes from the licensed Synty packs, which are never committed:

- Run `tools/sync-assets.ps1` once, then open the project in Godot so it imports the FBX files. The script copies the files listed in `tools/synty-assets.json` into the gitignored `Assets/Synty/` folder. Set `CARDCLEANER_ASSET_SOURCE` to override the source root (default `../CardCleanerAssets`). A missing root or file is an error.
- Each shop issue adds its own manifest entries.
- `ShopArtSlot` nodes load a pack mesh at runtime and hide their graybox placeholder. Without the import the scene still loads and shows the placeholders. Collision is hand-authored and independent of the art.

### Backoffice PC Ordering

The PC in the shop backoffice (`World/PcTerminal`, an `OrderTerminal`) opens an ordering screen (`OrderTerminalUi`) for the `OrderCatalog` assigned to its `Catalog` export (`Data/Shop/BackofficeCatalog.tres`). Ordering uses `IMoneyService` (balance, `TrySpend`) and `IOrderingService`, both nodes under the scene's `Services` provider. A successful order deducts the price and instances the item scene (`Scenes/Shop/Items/*.tscn`) at the `DeliveryPoint` marker, in a grid of slots that never overlap. To sell different items elsewhere (for example a workshop terminal), add an `OrderTerminal`, an `OrderTerminalUi` and a new `OrderCatalog` resource; no code changes. While the screen is open `PlayerController.ControlEnabled` is false and the cursor is visible; closing it (button or Escape) restores both. The starting balance is `MoneyService.StartingBalance`; the balance is not saved.
