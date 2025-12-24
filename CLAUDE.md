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

Procedural world generation uses Wave Function Collapse with 3D layering and signature-based tile weighting:

**SemanticWfc3dGenerator** (`Scripts/Features/Worldgen/SemanticWfc3dGenerator.cs`):
- 3D WFC implementation generating worlds across 4 vertical layers
- Layers processed sequentially: Terrain → Decoration → Structure → Effects
- Supports constraint propagation between layers via `LayerConstraints`
- Integrates `GradientInfluenceComponent` to adjust tile weights by position

**SemanticTile** (`Scripts/Features/Worldgen/SemanticTile.cs`):
- Tile resources with `TileLayer` assignment, `BaseWeight`, and `CardSignature`
- **Socket System**: 10-directional sockets (N/E/S/W/NE/SE/SW/NW/Up/Down) define tile compatibility
- Tiles connect when adjacent sockets match via `CanConnectTo()` method
- `LayerConstraints` allow tiles to modify available options on other layers when placed

**Gradient Systems** (`Scripts/Features/Worldgen/Gradients/`):
- **RadialGradient**: Interpolates between center and edge signatures using falloff curves
- **NoiseGradient**: Applies FastNoiseLite-based variation to base signature
- **CardBasedGradient**: Creates gradients from input cards (sphere for 1 card, capsule for 2, Bezier curve for 3+)

**GradientInfluenceComponent**: Calculates position-specific signatures from gradients and adjusts tile weights based on signature similarity, creating signature-influenced terrain patterns.

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
- **Worldgen/**: WFC-based procedural generation, semantic tiles, gradients
- **Player/**: Player controller and interaction system
- **Conveyor/**: Conveyor belt mechanics
- **Pause/**: Pause menu controller

Core utilities and interfaces are in `Scripts/Core/`.
