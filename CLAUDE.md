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

> **Architecture Document**: See `docs/WORLDGEN_ARCHITECTURE.md` for the complete worldgen redesign plan, including implementation phases, data definition guides, and technical reference.

**Active System**: **SimpleMapGenerator** (`Scripts/Features/Deckbuilder/Services/SimpleMapGenerator.cs`)
- Lightweight random placement with connectivity guarantees
- Signature-influenced tile selection (Febris→terrain, Ordinem→structure)
- Biome system with tile pools and weighted selection
- Auto-tiling support (Corner16 and Blob47 formats)

**Gradient Systems**:
- `CardBasedGradient`: Creates gradients from input cards (sphere/capsule/Bezier)
- `RadialGradient`: Center-to-edge signature blending
- `NoiseGradient`: FastNoiseLite-based variation

**Target Architecture** (see docs for details):
- Stage 1: Biome Placement (card gradient → biome grid)
- Stage 2: Terrain Generation (per-biome tile selection)
- Stage 3: Transitions (auto-tiling at biome edges)
- Stage 4: Structures (stamps + procedural generators)
- Stage 5: Entities (player, enemies, items)

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
