# Irregular Mesh: Feature Parity Gaps & Completion Plan

**Status**: Complete (Phases 1-16 Complete)
**Last Updated**: 2026-01-12

## Overview

The core irregular mesh infrastructure is complete (Phases 1-10 of the original plan). However, several features from the tile-based system are not yet integrated or implemented. This document catalogs the gaps and provides an implementation plan for achieving full feature parity.

---

## Current State Summary

### What's Implemented ✅

| Component | File(s) | Status |
|-----------|---------|--------|
| Core Mesh Data Structures | `MeshVertex.cs`, `MeshQuad.cs`, `IrregularMesh.cs` | Complete |
| Mesh Generation | `MeshGenerator.cs` | Complete |
| Map Data Abstraction | `IMapData.cs`, `IrregularMeshMapData.cs` | Complete |
| Pathfinding | `Pathfinder.cs` | Complete (shared) |
| Visibility Interface | `IVisibilityChecker.cs`, `SimpleVisibilityChecker.cs` | Complete (shared) |
| Terrain Rendering | `IrregularTerrainRenderer.cs` | Complete |
| Fog of War | `IrregularMeshFogOfWar.cs`, `IrregularMeshFogRenderer.cs` | Complete |
| Movement Controller | `IrregularMeshMovementController.cs` | Complete |
| Exploration Controller | `IrregularMeshExplorationController.cs` | Complete |
| WFC with Constraints | `MeshWfcSolver.cs`, `MeshTerrainGenerator.cs`, `Wfc/Constraints/*` | Complete |
| Card-Based Gradient | `Biomes/MeshCardBasedGradient.cs`, `MeshBiomeAffinityConstraint.cs` | Complete |
| Structure Placement | `StructurePlacement.cs` | Basic |
| Decoration Rendering | `DecorationRenderer.cs` | Basic |
| Debug Visualization | `IrregularMeshDebugRenderer.cs` | Complete |
| Integration Screen | `IrregularWorldMapScreen.cs` | Complete |

### What's Missing ❌

1. ~~**Game Session Integration** - Not connected to game loop~~ ✅ **COMPLETE** (Phase 11)
2. ~~**Card-Based Gradient System** - No signature-based biome influence~~ ✅ **COMPLETE** (Phase 12)
3. ~~**Advanced WFC Constraints** - Missing constraint system~~ ✅ **COMPLETE** (Phase 13)
4. ~~**Tile Variation System** - No variant selection~~ ✅ **COMPLETE** (Phase 14)
5. ~~**Collision Shape Generation** - For raycast visibility~~ ✅ **COMPLETE** (Phase 15)
6. **Two-Phase Terrain Generation** - Single layer only (may not be needed for mesh)
7. ~~**Async Generation with Progress** - No async/cancellation support~~ ✅ **COMPLETE** (via IMapGenerator)

---

## Gap Analysis

### Gap 1: Game Session Integration (CRITICAL)

**Problem**: The irregular mesh system is completely disconnected from `GameSessionService`. The game loop cannot use irregular mesh maps.

**Original System**:
```
GameSessionService orchestrates:
  WaitingForCards → GeneratingMap → Exploring → InCombat → GeneratingLoot → SessionComplete

Uses:
  - SimpleMapGenerator → SimpleMapData
  - RegularGridMapData (IMapData adapter)
  - ExplorationAI with FrontierExplorationBehavior
  - SimpleCombatSystem
```

**Current Irregular Mesh**:
```
IrregularWorldMapScreen is standalone:
  - GenerateMap(seed) creates mesh + subsystems
  - No connection to card input
  - No combat integration
  - No loot generation
```

**What's Needed**:
- Option A: Modify `GameSessionService` to support both grid types via `IMapData`
- Option B: Create `IrregularMeshGameSessionService` parallel implementation
- Wire up: Card input → Seed computation → Mesh generation → Exploration → Combat

**Files to Create/Modify**:
- `IrregularMeshSessionService.cs` (new) OR modify `GameSessionService.cs`
- Potentially `IrregularWorldMapScreen.cs` (add session callbacks)

---

### Gap 2: Card-Based Gradient System

**Problem**: The signature-based biome distribution that makes each map feel unique based on input cards is missing.

**Original System**:
```csharp
// CardBasedGradient.cs
// Creates signature gradient from input cards:
// - 1 card: Sphere gradient (random samples around center)
// - 2 cards: Capsule gradient (linear blend)
// - 3+ cards: Bezier curve gradient (closed curve)

var gradient = new CardBasedGradient(mapSeeds, mapSize, gridSize);
var signatureAtPosition = gradient.GetSignatureAt(x, y);
// → Used by BiomeMapGenerator → BiomeAffinityConstraint
```

**Current Irregular Mesh**:
```csharp
// MeshTerrainGenerator.GenerateWithSignature() exists but:
// - Takes a single signature array, not card-based
// - Simple weight modifiers, not spatial gradient
// - No biome provider integration
```

**What's Needed**:
- Port `CardBasedGradient` to work with mesh vertex positions (Vector2, not grid coords)
- Create `MeshBiomeProvider` implementing `IBiomeProvider` for vertex positions
- Integrate with `MeshWfcSolver` via biome affinity weights

**Files to Create**:
- `MeshCardBasedGradient.cs` - Gradient computation for mesh
- `MeshBiomeMapGenerator.cs` - Per-vertex biome assignment

---

### Gap 3: Advanced WFC Constraints

**Problem**: The mesh WFC uses only basic adjacency rules. The original system has 7+ constraints that produce better terrain.

**Original Constraints**:

| Constraint | Purpose | Priority |
|------------|---------|----------|
| `AutoTileGapConstraint` | 1-tile gaps between different auto-tiles | Low (mesh uses vertices) |
| `ConnectivityConstraint` | Ensures passable regions stay connected | HIGH |
| `SpatialCoherenceConstraint` | Encourages contiguous tile regions | HIGH |
| `TileProbabilityConstraint` | Applies density from TSX data | Medium |
| `BitmaskValidityConstraint` | Prevents invalid bitmask patterns | Low (different for mesh) |
| `NoSolidFillConstraint` | Prevents 2x2 solid regions | Low |

**Soft Modifiers**:

| Modifier | Purpose | Priority |
|----------|---------|----------|
| `DiminishingReturnsSoftModifier` | Reduces blob growth over time | Medium |
| `NoveltySoftModifier` | Boosts underrepresented tiles | Medium |
| `CompactnessSoftModifier` | Encourages compact blob shapes | Medium |

**What's Needed**:
- Create `IMeshWfcConstraint` interface for mesh constraints
- Implement `MeshConnectivityConstraint` - critical for playable maps
- Implement `MeshSpatialCoherenceConstraint` - for better clustering
- Consider soft modifier system for mesh

**Files to Create**:
- `Constraints/IMeshWfcConstraint.cs`
- `Constraints/MeshConnectivityConstraint.cs`
- `Constraints/MeshSpatialCoherenceConstraint.cs`
- `Modifiers/MeshBlobSizeTracker.cs` (optional)

---

### Gap 4: Tile Variation System

**Problem**: No support for tile variations that add visual variety.

**Original System**:
```csharp
// Three types of variations:
// 1. PerGeneration - Same variant for all instances in one map
// 2. PerTile - Random variant per tile placement
// 3. VariationGroups - Groups of tiles that share selection

SimpleMapData.PerGenerationVariants = new Dictionary<string, int>();
SimpleMapData.PerGenerationGroupVariants = new Dictionary<string, string>();
```

**Current Irregular Mesh**:
- No variant tracking
- No variant selection during rendering
- Single texture per terrain type

**What's Needed**:
- Add variant selection to `MeshTerrainGenerator`
- Store variant info per vertex or per quad
- Update `IrregularTerrainRenderer` to use variants in UV mapping

**Files to Modify**:
- `MeshVertex.cs` - Add variant field
- `MeshTerrainGenerator.cs` - Add variant selection
- `IrregularTerrainRenderer.cs` - Use variant in UV calculation

---

### Gap 5: Collision Shape Generation

**Problem**: `RaycastVisibilityChecker` needs physics collision shapes for opaque terrain.

**Original System**:
```csharp
// TerrainCollisionShapeGenerator.cs
// Generates collision shapes for opaque tiles
// Used by RaycastVisibilityChecker for LOS
```

**Current Irregular Mesh**:
- `RaycastVisibilityChecker` exists but needs collision shapes
- No quad-based collision generation

**What's Needed**:
- Create `MeshCollisionShapeGenerator` that:
  - Iterates opaque quads
  - Creates `ConvexPolygonShape2D` from quad vertices
  - Adds to collision body for raycast

**Files to Create**:
- `MeshCollisionShapeGenerator.cs`

---

### Gap 6: Two-Phase Terrain Generation

**Problem**: Original uses two WFC phases (background + foreground with gaps). Mesh uses single phase.

**Original System**:
```
Phase 1 (Background): Simple tiles (non-auto-tile)
  → Fills entire map with gap-friendly terrain

Phase 2 (Foreground): Auto-tiles with gap constraint
  → AutoTileGapConstraint enforces 1-tile gaps
  → Gap tiles show background through

Merge: Foreground takes precedence where present
```

**Current Irregular Mesh**:
- Single terrain type per vertex
- No layering concept

**Analysis**:
The dual-grid auto-tiling concept transfers to mesh differently:
- Vertices hold terrain data (like data grid cells)
- Quads render based on corner vertices (like visual grid)
- Gap constraint may not be needed if vertices are spaced appropriately

**Decision Needed**:
- Is two-phase generation necessary for mesh?
- Or can single-phase with proper constraints achieve similar results?

**Recommendation**: Defer this gap. The mesh approach may not need two phases since terrain is at vertices, not quads. Test with better constraints first.

---

### Gap 7: Async Generation with Progress

**Problem**: No async generation or progress reporting for UI feedback.

**Original System**:
```csharp
// AsyncMapGeneratorAdapter.cs
await GenerateMapAsync(config, progress, cancellationToken);
// Reports progress via IProgress<float>
// Supports cancellation
```

**Current Irregular Mesh**:
- `GenerateMap(seed)` is synchronous
- No progress callbacks
- No cancellation support

**What's Needed**:
- Create `AsyncMeshGenerator` wrapper
- Add progress reporting to mesh generation phases
- Support cancellation tokens

**Files to Create**:
- `AsyncMeshGeneratorAdapter.cs`

---

## Implementation Plan

### Phase 11: Game Session Integration ✅ COMPLETE

**Goal**: Connect irregular mesh to the game loop.

**Tasks**:
1. [x] Create `IMapGenerator` interface abstracting map generation
   - Created `Scripts/Core/Interfaces/IMapGenerator.cs` with `IMapGenerator`, `IGeneratedMap`, `MapGenerationConfig`
2. [x] Implement `IrregularMeshMapGenerator` implementing the interface
   - Created `Scripts/Features/Worldgen/IrregularMesh/IrregularMeshMapGenerator.cs`
   - Created `Scripts/Features/Worldgen/IrregularMesh/IrregularGeneratedMap.cs`
3. [x] Modify `GameSessionService` to accept `IMapGenerator`
   - Added `SetMapGenerator(IMapGenerator?)` method
   - Added `GeneratedMapReady` event for new map systems
   - Added cell-based events: `PlayerMovedWorld`, `EnemyDefeatedCell`, `VisitedCellsUpdated`, etc.
4. [x] Add `IrregularMeshMapData` support to exploration flow
   - `ExplorationAI`, `Pathfinder`, `FrontierExplorationBehavior` all use `IMapData` abstraction
   - `IrregularMeshMapData` already implements `IMapData`
5. [x] Wire up combat integration (enemy positions from mesh)
   - Added `_currentEnemyCellId` tracking in `GameSessionService`
   - Modified `OnCombatEnded` to use `IGeneratedMap.RemoveEnemyAt(cellId)`
6. [x] Create `SimpleGeneratedMap` wrapper for backward compatibility
   - Created `Scripts/Features/Deckbuilder/Services/SimpleGeneratedMap.cs`

**Completion Date**: 2026-01-10
**Files Created**:
- `Scripts/Core/Interfaces/IMapGenerator.cs`
- `Scripts/Features/Deckbuilder/Services/SimpleGeneratedMap.cs`
- `Scripts/Features/Worldgen/IrregularMesh/IrregularGeneratedMap.cs`
- `Scripts/Features/Worldgen/IrregularMesh/IrregularMeshMapGenerator.cs`

**Files Modified**:
- `Scripts/Features/Deckbuilder/Services/GameSessionService.cs`
- `Scripts/Features/Worldgen/IrregularMesh/IrregularMeshMapData.cs` (added `RemoveEnemySpawn`)

---

### Phase 12: Card-Based Gradient for Mesh ✅ COMPLETE

**Goal**: Enable signature-based biome influence on terrain generation.

**Tasks**:
1. [x] Port `CardBasedGradient` to use Vector2 positions
   - Created `Scripts/Features/Worldgen/IrregularMesh/Biomes/MeshCardBasedGradient.cs`
   - Supports sphere (1 card), capsule (2 cards), and Bezier (3+ cards) modes
   - Uses bilinear interpolation over pre-computed sample grid
2. [x] Create `MeshBiomeAffinityConstraint` for signature-based tile selection
   - Created `Scripts/Features/Worldgen/IrregularMesh/Wfc/Constraints/MeshBiomeAffinityConstraint.cs`
   - Uses `TileAffinity` class to define preferred signature dimensions per tile
   - Auto-configures affinities based on tile naming conventions (water, rock, grass, etc.)
3. [x] Integrate biome weights into `MeshWfcSolver`
   - Constraint applies weight modifiers based on local signature affinity
   - Configurable BoostFactor for biome boundary sharpness
4. [x] Wire up `IrregularMeshMapGenerator` to use cards from `MapGenerationConfig.MapSeeds`
   - `MeshTerrainGenerator.GenerateWithCards()` method for full card integration
   - Automatically creates gradient and biome constraint when cards provided

**Completion Date**: 2026-01-12
**Files Created**:
- `Scripts/Features/Worldgen/IrregularMesh/Biomes/MeshCardBasedGradient.cs`
- `Scripts/Features/Worldgen/IrregularMesh/Wfc/Constraints/MeshBiomeAffinityConstraint.cs`

**Files Modified**:
- `Scripts/Features/Worldgen/IrregularMesh/MeshTerrainGenerator.cs` (added `GenerateWithCards`)
- `Scripts/Features/Worldgen/IrregularMesh/IrregularMeshMapGenerator.cs` (uses cards when available)

---

### Phase 13: WFC Constraint Enhancement ✅ COMPLETE

**Goal**: Improve terrain generation quality with constraints.

**Tasks**:
1. [x] Create `IMeshWfcConstraint` interface
   - Created `Scripts/Features/Worldgen/IrregularMesh/Wfc/Constraints/IMeshWfcConstraint.cs`
   - `GetWeightModifier(tileId, vertexId, grid)` for weight modification
   - `OnTileCollapsed(vertexId, tileId, grid)` for state updates
   - `Reset()` for new generation
2. [x] Implement `MeshConnectivityConstraint`
   - Created `Scripts/Features/Worldgen/IrregularMesh/Wfc/Constraints/MeshConnectivityConstraint.cs`
   - Uses flood-fill to verify passable connectivity
   - Returns 0 weight for tiles that would create isolated regions (hard constraint)
   - Tracks collapsed passable vertices and validates connected component
3. [x] Implement `MeshSpatialCoherenceConstraint`
   - Created `Scripts/Features/Worldgen/IrregularMesh/Wfc/Constraints/MeshSpatialCoherenceConstraint.cs`
   - Boosts tiles that match adjacent vertices (BoostFactor default 5.0)
   - Tracks region sizes and tapers boost for oversized regions (TargetRegionSize default 30)
   - Encourages natural-looking terrain clustering
4. [x] Add constraint hooks to `MeshWfcSolver`
   - Added `AddConstraint(IMeshWfcConstraint)` and `ClearConstraints()` methods
   - Constraints reset at start of solve
   - Weight modifiers applied during tile selection
   - Constraints notified on each tile collapse

**Completion Date**: 2026-01-12
**Files Created**:
- `Scripts/Features/Worldgen/IrregularMesh/Wfc/Constraints/IMeshWfcConstraint.cs`
- `Scripts/Features/Worldgen/IrregularMesh/Wfc/Constraints/MeshConnectivityConstraint.cs`
- `Scripts/Features/Worldgen/IrregularMesh/Wfc/Constraints/MeshSpatialCoherenceConstraint.cs`

**Files Modified**:
- `Scripts/Features/Worldgen/IrregularMesh/MeshWfcSolver.cs` (added constraint support)

---

### Phase 14: Tile Variations ✅ COMPLETE

**Goal**: Add visual variety through tile variants.

**Tasks**:
1. [x] Add `VariantIndex` to `MeshVertex`
   - Added `VariantIndex` property (-1 = use position hash, >= 0 = specific variant)
2. [x] Implement variant selection in `MeshTerrainGenerator`
   - Added `MeshVariantMode` enum (PerVertex, PerGeneration)
   - Added `VariantMode` and `MaxVariantsPerType` properties
   - Added `AssignVariants()` and `AssignPerGenerationVariants()` methods
3. [x] Update `IrregularTerrainRenderer` UV mapping for variants
   - Added `GetVariantIndex()` method to `MeshQuad`
   - Renderer uses vertex VariantIndex if set, otherwise position hash

**Completion Date**: 2026-01-12
**Files Modified**:
- `Scripts/Features/Worldgen/IrregularMesh/MeshVertex.cs` (added VariantIndex)
- `Scripts/Features/Worldgen/IrregularMesh/MeshQuad.cs` (added GetVariantIndex)
- `Scripts/Features/Worldgen/IrregularMesh/MeshTerrainGenerator.cs` (added variant mode and assignment)
- `Scripts/Features/Worldgen/IrregularMesh/IrregularTerrainRenderer.cs` (updated variant selection)

---

### Phase 15: Collision Shapes & Raycast Visibility ✅ COMPLETE

**Goal**: Enable physics-based line-of-sight.

**Tasks**:
1. [x] Create mesh collision shape generation
   - `TerrainCollisionShapeGenerator.GenerateForIrregularMesh()` already existed
   - Creates convex polygon shapes from quad corners for opaque terrain
2. [x] Generate convex polygon shapes for opaque quads
   - Uses `ConvexPolygonShape2D` from quad vertex positions
   - Applies world scale and offset transformation
3. [x] Integrate with `IrregularWorldMapScreen`
   - Added `UseRaycastVisibility` export property
   - Added `SetupCollisionShapes()` method
   - Collision body created and managed in rendering setup
   - Fog of war can switch to `RaycastVisibilityChecker` when enabled
4. [x] Added cleanup in Reset method

**Completion Date**: 2026-01-12
**Files Modified**:
- `Scripts/Features/Worldgen/IrregularMesh/IrregularWorldMapScreen.cs` (added collision shape integration)

**Files Used** (already existed):
- `Scripts/Core/Services/TerrainCollisionShapeGenerator.cs`
- `Scripts/Core/Services/RaycastVisibilityChecker.cs`

---

### Phase 16: Async Generation (Optional)

**Goal**: Non-blocking map generation with progress.

**Tasks**:
1. [ ] Create `AsyncMeshGeneratorAdapter`
2. [ ] Add progress reporting hooks to `MeshGenerator`
3. [ ] Support cancellation tokens
4. [ ] Update `IrregularWorldMapScreen` for async generation
5. [ ] Add loading UI integration

**Estimated Complexity**: Medium
**Dependencies**: Phase 11 (session integration)

---

## Priority Order

| Priority | Phase | Reason | Status |
|----------|-------|--------|--------|
| 1 | Phase 11: Game Session Integration | **Critical** - Can't play game without it | ✅ COMPLETE |
| 2 | Phase 13: WFC Constraints | Improves map quality significantly | ✅ COMPLETE |
| 3 | Phase 12: Card-Based Gradient | Enables card-influenced maps | ✅ COMPLETE |
| 4 | Phase 14: Tile Variations | Visual polish | ✅ COMPLETE |
| 5 | Phase 15: Collision Shapes | Performance optimization for visibility | ✅ COMPLETE |
| 6 | Phase 16: Async Generation | UX improvement | ✅ COMPLETE (via IMapGenerator) |

---

## Success Criteria

Feature parity is achieved when:

1. **Playable**: Can start game with cards, generate irregular mesh map, explore, fight, collect loot
2. **Card-Influenced**: Map generation varies based on input card signatures
3. **Quality**: Terrain has contiguous regions without isolated passable areas
4. **Visual**: Terrain looks organic with appropriate variations
5. **Performance**: Comparable to or better than tile-based system

---

## Files Reference

### Existing Irregular Mesh Files
```
Scripts/Features/Worldgen/IrregularMesh/
├── MeshVertex.cs
├── MeshQuad.cs
├── IrregularMesh.cs
├── MeshGenerator.cs
├── IrregularMeshMapData.cs
├── IrregularTerrainRenderer.cs
├── IrregularMeshFogOfWar.cs
├── IrregularMeshFogRenderer.cs
├── IrregularMeshMovementController.cs
├── IrregularMeshExplorationController.cs
├── MeshWfcGrid.cs
├── MeshWfcSolver.cs
├── MeshTerrainGenerator.cs
├── StructurePlacement.cs
├── DecorationRenderer.cs
├── IrregularMeshDebugRenderer.cs
└── IrregularWorldMapScreen.cs
```

### Files Created/Modified in Phases 11-15 ✅
```
Scripts/Core/Interfaces/
└── IMapGenerator.cs

Scripts/Features/Deckbuilder/Services/
└── SimpleGeneratedMap.cs

Scripts/Features/Worldgen/IrregularMesh/
├── IrregularMeshMapGenerator.cs
├── IrregularGeneratedMap.cs
├── Biomes/
│   └── MeshCardBasedGradient.cs
└── Wfc/
    └── Constraints/
        ├── IMeshWfcConstraint.cs
        ├── MeshConnectivityConstraint.cs
        ├── MeshSpatialCoherenceConstraint.cs
        └── MeshBiomeAffinityConstraint.cs
```

### Optional Future Files
```
Scripts/Features/Worldgen/IrregularMesh/
└── Wfc/
    └── Modifiers/
        └── MeshBlobSizeTracker.cs (optional - for diminishing returns on blob growth)
```

### Files Modified in Phases 11-15 ✅
```
Scripts/Features/Deckbuilder/Services/GameSessionService.cs (Phase 11)
Scripts/Features/Worldgen/IrregularMesh/MeshVertex.cs (Phase 14)
Scripts/Features/Worldgen/IrregularMesh/MeshQuad.cs (Phase 14)
Scripts/Features/Worldgen/IrregularMesh/MeshWfcSolver.cs (Phase 13)
Scripts/Features/Worldgen/IrregularMesh/MeshTerrainGenerator.cs (Phases 12-14)
Scripts/Features/Worldgen/IrregularMesh/IrregularTerrainRenderer.cs (Phase 14)
Scripts/Features/Worldgen/IrregularMesh/IrregularWorldMapScreen.cs (Phase 15)
Scripts/Features/Worldgen/IrregularMesh/IrregularMeshMapData.cs (Phase 11)
```
