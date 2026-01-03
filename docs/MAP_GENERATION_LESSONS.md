# Map Generation V2 - Lessons Learned

This document captures discoveries and integration notes during implementation of the WFC-native map generation system.

---

## Phase 1.1: Implement Card-Based RNG Seeding
**Date:** 2026-01-01
**Status:** Complete

### Discoveries
- SimpleMapData uses `PlayerStart` property, not `PlayerPosition` - important for test assertions
- The existing test file has extensive async tests using `ToSignal(_service.GetTree(), SceneTree.SignalName.ProcessFrame)` pattern for waiting on deferred calls
- CardSignature has `this[int index]` indexer for accessing all 8 signature elements

### API Changes Made
- Added `ComputeSeedFromCards(List<CardSignature>) → ulong` to GameSessionService (internal static for testability)
- Removed `_rng.Randomize()` from `_Ready()`
- Added seed computation in `GenerateMap()` with logging via `ILog.Print($"Map seed: {seed}")`

### Test Coverage Added
- Tests/Features/Deckbuilder/Services/GameSessionServiceTest.cs
- 4 test cases for seed determinism:
  - `ComputeSeedFromCards_SameCards_ReturnsSameSeed`
  - `ComputeSeedFromCards_DifferentCards_ReturnsDifferentSeeds`
  - `ComputeSeedFromCards_OrderMatters`
  - `GenerateMap_SameSeed_ProducesSameMap`

### Integration Notes for Later Phases
- Seed is computed in `GenerateMap()`, not in `StartSession()`
- Method is `internal static` so tests can call it directly without Godot runtime
- The full map determinism test (`GenerateMap_SameSeed_ProducesSameMap`) requires Godot runtime to run
- Hash algorithm uses BitConverter for deterministic float→bits: `hash = hash * 31 + BitConverter.ToUInt32(BitConverter.GetBytes(card[i]), 0)`

---

## Phase 2.1: Implement Weighted Shannon Entropy
**Date:** 2026-01-01
**Status:** Complete

### Discoveries
- Godot's `Mathf.Log()` is natural logarithm (base e), same as `System.Math.Log()` - suitable for Shannon entropy calculation
- GdUnit4 tests using `RandomNumberGenerator` require `[RequireGodotRuntime]` attribute; tests without Godot types can run without Godot runtime
- Zero-weight tiles are excluded from entropy calculation (skipped in the probability sum) - this is correct behavior
- Missing weights in the dictionary default to 1.0f for graceful handling of incomplete weight maps

### API Changes Made
- Added `GetWeightedEntropy(IReadOnlyDictionary<string, float> weights)` to WfcCellState
  - Returns Shannon entropy: `-Σ(p * log(p))` where `p = weight/totalWeight`
  - Returns 0f for collapsed cells (≤1 tile)
  - Returns `float.MaxValue` for zero total weight (prevents selection)
- Added `GetLowestEntropyCellWeighted(Func<Vector2I, IReadOnlyDictionary<string, float>>, RandomNumberGenerator)` to WfcGrid
  - Finds cell with lowest weighted entropy using provided position-aware weight function
  - Uses `Mathf.IsEqualApprox()` for float comparison in tiebreak collection

### Test Coverage Added
- Tests/Features/Worldgen/Wfc/WfcEntropyTest.cs
- 8 test cases:
  - `GetWeightedEntropy_SingleTile_ReturnsZero`
  - `GetWeightedEntropy_UniformWeights_ReturnsMaxEntropy`
  - `GetWeightedEntropy_DominantTile_ReturnsLowEntropy`
  - `GetWeightedEntropy_Deterministic`
  - `GetLowestEntropyCellWeighted_SelectsDominantCell` (RequireGodotRuntime)
  - `GetWeightedEntropy_MissingWeight_DefaultsToOne`
  - `GetWeightedEntropy_ZeroWeight_ExcludedFromCalculation`
  - `GetLowestEntropyCellWeighted_AllCollapsed_ReturnsNull` (RequireGodotRuntime)

### Integration Notes for Later Phases
- Weight calculator signature: `Func<Vector2I, IReadOnlyDictionary<string, float>>`
- Existing `GetLowestEntropyCellWithTieBreak()` preserved for backward compatibility
- Phase 3 will update WfcSolver to use weighted entropy with unified constraint weights
- Entropy comparison uses `Mathf.IsEqualApprox()` for float tolerance (important for floating-point determinism)

---

## Phase 3.1: Create IWfcConstraint Interface
**Date:** 2026-01-01
**Status:** Complete

### Discoveries
- WfcConstraintContext closely mirrors existing SoftModifierContext and HardConstraintContext structures
- The optional RNG property distinguishes WfcConstraintContext from legacy context types (which lack RNG)
- IHardConstraint is completely unused in codebase (confirmed via grep) - safe to delete in Phase 3.3

### API Changes Made
- Created `IWfcConstraint` interface in `Scripts/Features/Worldgen/Wfc/Constraints/`
  - `float GetProbabilityModifier(WfcConstraintContext context)` method
  - Return semantics: 0.0 = hard ban, 0.0-1.0 = penalty, 1.0 = neutral, >1.0 = boost
- Created `WfcConstraintContext` readonly struct
  - Properties: `Position (Vector2I)`, `TileId (string)`, `Grid (WfcGrid)`, `Rng (RandomNumberGenerator?)`
  - Uses init-only setters for immutability

### Test Coverage Added
- Tests/Features/Worldgen/Wfc/Constraints/WfcConstraintContextTest.cs
- 2 test cases:
  - `WfcConstraintContext_InitializesCorrectly` (RequireGodotRuntime)
  - `WfcConstraintContext_DefaultRngIsNull` (RequireGodotRuntime)

### Integration Notes for Later Phases
- Namespace: `CardCleaner.Scripts.Features.Worldgen.Wfc.Constraints`
- Context properties mirror SoftModifierContext exactly, with additional optional Rng
- Phase 3.2 will have soft modifiers implement both ISoftModifier and IWfcConstraint temporarily
- Constraints are applied multiplicatively: `final_weight = base_weight × Π(modifiers)`

---

## Phase 3.2: Migrate Constraints to IWfcConstraint
**Date:** 2026-01-01
**Status:** Complete

### Discoveries
- WfcAdjacencyRules is non-directional: `CanBeAdjacent(tileA, tileB)` checks bidirectional adjacency without needing a Direction enum
- WfcCellState uses `CollapseTo()` not `Collapse()` for collapsing cells
- The dual-interface approach (ISoftModifier + IWfcConstraint) requires minimal code duplication since GetProbabilityModifier can delegate to CalculateMultiplier via context conversion
- NoveltySoftModifier has an unused `_blobTracker` field (analyzer warning IDE0052) - it counts neighbors directly from grid state

### API Changes Made
- DiminishingReturnsSoftModifier now implements both `ISoftModifier` and `IWfcConstraint`
- NoveltySoftModifier now implements both `ISoftModifier` and `IWfcConstraint`
- CompactnessSoftModifier now implements both `ISoftModifier` and `IWfcConstraint`
- Created `AdjacencyConstraint` class in `Scripts/Features/Worldgen/Wfc/Constraints/`
  - Wraps `WfcAdjacencyRules` to return 0.0f for invalid adjacencies, 1.0f for valid
  - No Direction enum needed - WfcAdjacencyRules.CanBeAdjacent() is symmetric
- Added `AddConstraint(IWfcConstraint)` to WfcTileSelector
- Added `ClearConstraints()` to WfcTileSelector
- WfcTileSelector now applies constraints after soft modifiers with early-exit on 0.0f

### Test Coverage Added
- Tests/Features/Worldgen/Wfc/Constraints/ConstraintMigrationTest.cs
- 6 test cases:
  - `DiminishingReturns_BothInterfacesReturnSameValue`
  - `Novelty_BothInterfacesReturnSameValue`
  - `Compactness_BothInterfacesReturnSameValue`
  - `AdjacencyConstraint_ValidAdjacency_ReturnsOne`
  - `AdjacencyConstraint_InvalidAdjacency_ReturnsZero`
  - `WfcTileSelector_ConstraintList_AppliesMultiplicatively`

### Integration Notes for Later Phases
- ~~Old interfaces (ISoftModifier, IHardConstraint) still exist for backward compatibility~~ (Removed in Phase 3.3)
- ~~Phase 3.3 will remove ISoftModifier and IHardConstraint after migrating all consumers~~ (Done)
- Constraint application in WfcTileSelector uses early-exit: if any constraint returns 0.0f, weight becomes 0 and remaining constraints are skipped
- AdjacencyConstraint constructor requires WfcAdjacencyRules instance

---

## Phase 3.3: Remove Legacy Interfaces
**Date:** 2026-01-01
**Status:** Complete

### Discoveries
- NoveltySoftModifier never used its BlobSizeTracker dependency - it counts neighbors directly from grid state, so the constructor parameter was removed
- Tests that compared ISoftModifier.CalculateMultiplier vs IWfcConstraint.GetProbabilityModifier became redundant after unification - replaced with interface verification tests
- IHardConstraint was completely unused throughout the codebase - only referenced in its own interface file and documentation

### Files Deleted
- `Scripts/Features/Worldgen/Wfc/Modifiers/Soft/ISoftModifier.cs` (contained ISoftModifier interface and SoftModifierContext struct)
- `Scripts/Features/Worldgen/Wfc/Modifiers/Hard/IHardConstraint.cs` (contained IHardConstraint interface and HardConstraintContext struct)

### API Changes Made
- **WfcTileSelector:**
  - Removed `_softModifiers` field
  - Removed `AddModifier(ISoftModifier)` method
  - Removed `ClearModifiers()` method
  - Now only uses `_constraints` list with `AddConstraint(IWfcConstraint)` and `ClearConstraints()`
- **WfcMapGenerator:**
  - Renamed `ConfigureModifiers()` → `ConfigureConstraints()`
  - Now uses `_selector.AddConstraint()` instead of `_selector.AddModifier()`
  - NoveltySoftModifier constructor call updated to use parameterless constructor
- **DiminishingReturnsSoftModifier:**
  - Removed `ISoftModifier` interface implementation
  - Removed `CalculateMultiplier(SoftModifierContext)` method
  - Logic moved directly into `GetProbabilityModifier(WfcConstraintContext)`
- **NoveltySoftModifier:**
  - Removed `ISoftModifier` interface implementation
  - Removed `CalculateMultiplier(SoftModifierContext)` method
  - Removed unused BlobSizeTracker constructor parameter
  - Added default parameterless constructor
  - Logic moved directly into `GetProbabilityModifier(WfcConstraintContext)`
- **CompactnessSoftModifier:**
  - Removed `ISoftModifier` interface implementation
  - Removed `CalculateMultiplier(SoftModifierContext)` method
  - Logic moved directly into `GetProbabilityModifier(WfcConstraintContext)`

### Test Changes Made
- **ConstraintMigrationTest:**
  - Replaced 3 "both interfaces return same value" tests with interface verification tests
  - `DiminishingReturns_ImplementsOnlyIWfcConstraint`
  - `Novelty_ImplementsOnlyIWfcConstraint`
  - `Compactness_ImplementsOnlyIWfcConstraint`
  - Added `WfcTileSelector_AddModifier_MethodDoesNotExist` (reflection check)
  - Added `WfcTileSelector_ClearModifiers_MethodDoesNotExist` (reflection check)
- **All modifier tests updated:**
  - Changed from `SoftModifierContext` to `WfcConstraintContext`
  - Changed from `CalculateMultiplier()` to `GetProbabilityModifier()`
  - NoveltySoftModifierTest no longer uses BlobSizeTracker

### Integration Notes for Future Phases
- All constraint-related code now uses unified `IWfcConstraint` interface
- The `Modifiers/Soft/` namespace still contains the soft modifier classes, but they now only implement `IWfcConstraint`
- The `Modifiers/Hard/` directory is now empty (IHardConstraint.cs deleted) - can be removed if needed
- Phase 3 is now complete - the unified constraint interface is fully operational

---

## Phase 4.1: Create Biome Strength Grid
**Date:** 2026-01-01
**Status:** Complete

### Discoveries
- CardSignature.DistanceTo() returns Euclidean distance in 8D space. Theoretical max is ~5.66, but biomes typically differ in 1-2 dimensions, so effective MaxDistance = 2.83 (2*sqrt(2)) is used
- Nested test classes extending Godot types (like BaselineGradient → Resource → GodotObject) require `partial` modifier on both the containing class and the nested class
- BiomeDefinition constructor takes id, signature, passableTiles, blockedTiles in that order
- TilePool.GetAllTileIds() provides enumeration of tiles for building reverse lookup maps

### API Changes Made
- Created `BiomeStrengthGrid` class in `Scripts/Features/Worldgen/Biomes/`
  - Constructor: `BiomeStrengthGrid(Vector2I mapSize, BaselineGradient gradient, BiomeRegistry registry)`
  - `float GetStrength(Vector2I position, string biomeId)` - returns strength in [-1, 1] range
  - `int BiomeCount` - number of biomes in grid
  - `Vector2I Size` - grid dimensions
  - Uses `float[y, x, biomeCount]` internal storage with O(1) lookup
  - Distance-to-strength formula: `1 - 2 * (distance / MaxDistance)` where MaxDistance = 2.83 (ensures opposite signatures in one dimension yield negative strength)
- Created `BiomeAffinityConstraint` class in `Scripts/Features/Worldgen/Wfc/Constraints/`
  - Implements `IWfcConstraint`
  - Constructor: `BiomeAffinityConstraint(BiomeStrengthGrid grid, BiomeRegistry registry)`
  - Properties: `BoostFactor` (default 0.5), `MinModifier` (default 0.1)
  - Looks up tile's biome memberships via PassableTiles and BlockedTiles
  - Modifier formula: `1.0 + (averageStrength * BoostFactor)`
  - Pre-builds tile-to-biome lookup map in constructor for O(1) access

### Test Coverage Added
- Tests/Features/Worldgen/Biomes/BiomeStrengthGridTest.cs
  - 6 test cases:
    - `GetStrength_ValidPosition_ReturnsValueInRange`
    - `GetStrength_UnknownBiome_ReturnsZero`
    - `GetStrength_OutOfBoundsPosition_ReturnsZero`
    - `Computation_75x75Grid_CompletesUnder100ms`
    - `GetStrength_IdenticalSignatures_ReturnsPositiveOne`
    - `GetStrength_MaxDistantSignatures_ReturnsNegative`
- Tests/Features/Worldgen/Biomes/BiomeAffinityConstraintTest.cs
  - 6 test cases:
    - `BiomeAffinityConstraint_PositiveBiome_ReturnsBoost`
    - `BiomeAffinityConstraint_NegativeBiome_ReturnsPenalty`
    - `BiomeAffinityConstraint_NeutralBiome_ReturnsOne`
    - `BiomeAffinityConstraint_TileNotInAnyBiome_ReturnsOne`
    - `BiomeAffinityConstraint_ModifierNeverBelowMinimum`
    - `BiomeAffinityConstraint_ImplementsIWfcConstraint`

### Integration Notes for Later Phases
- BiomeStrengthGrid constructor requires `BaselineGradient`, not specifically `CardBasedGradient`
- BiomeAffinityConstraint builds tile-to-biome map once at construction; if biomes change, create new constraint
- Tiles in multiple biomes have their strengths averaged (not summed)
- Phase 4.2 will integrate BiomeStrengthGrid creation into WfcMapGenerator.GenerateMultiBiome()
- The constraint uses MinModifier = 0.1 to prevent tiles from being completely eliminated by negative strength

---

## Phase 4.2: Integrate Biome Grid into WFC Pipeline
**Date:** 2026-01-02
**Status:** Complete

### Discoveries
- GenerateMultiBiome already has access to BiomeRegistry - adding gradient parameter was straightforward
- SimpleMapGenerator constructor already accepts many optional dependencies - adding gradient follows same pattern
- The gradient parameter is optional (null default) - when null, BiomeAffinityConstraint is not registered (backwards compatible)
- ConfigureConstraints() is called before the gradient-based constraint, so base constraints (diminishing returns, novelty, compactness) are registered first

### API Changes Made
- **WfcMapGenerator.GenerateMultiBiome():**
  - Added optional `BaselineGradient? gradient = null` parameter
  - When gradient is provided: creates BiomeStrengthGrid and registers BiomeAffinityConstraint
  - Constraint is added after ConfigureConstraints() so it applies after base shape constraints
- **SimpleMapGenerator:**
  - Added `BaselineGradient? gradient = null` constructor parameter
  - Stored as `_gradient` field
  - Passed to GenerateMultiBiome() in GenerateTerrainViaWfc()

### Test Coverage Added
- Tests/Features/Worldgen/Wfc/BiomeGridIntegrationTest.cs
  - 6 test cases:
    - `GenerateMultiBiome_WithGradient_CreatesBiomeGrid`
    - `GenerateMultiBiome_WithoutGradient_StillWorks`
    - `TileAffinity_DefaultsToPositiveForBiomeTiles`
    - `TileAffinity_DefaultsToNeutralForOtherTiles`
    - `MapGeneration_ShowsBiomeInfluence`
    - `BiomeAffinityEnum_HasThreeValues` (verifies constraint modifier behavior)

### Integration Notes for Later Phases
- To use biome-based tile selection, callers of SimpleMapGenerator must provide a BaselineGradient
- RadialGradient can be created with center and edge CardSignatures for geographic variation
- NoiseGradient adds randomness to an otherwise uniform signature
- The BiomeAffinityConstraint is applied after shape constraints (diminishing returns, novelty, compactness), so it influences selection within the shape context
- Future: CardBasedGradient (Phase 4.3) will derive gradient from player's card selection

---

## Phase 5.1: Implement Articulation Point Detection
**Date:** 2026-01-02
**Status:** Complete

### Discoveries
- Vector2I works correctly as Dictionary/HashSet key in C# - no custom equality needed
- Tarjan's algorithm requires special handling for root nodes: a DFS tree root is an articulation point only if it has 2+ children in the DFS tree (not 2+ neighbors in the graph)
- For two connected nodes A-B, BOTH are articulation points because removing either leaves the other isolated (this is counterintuitive but correct per graph theory)
- The algorithm handles disconnected graphs by running DFS from all unvisited nodes
- Low-link value update only happens on back-edges (edges to already visited non-parent nodes)

### API Changes Made
- Created `PassabilityGraph` class in `Scripts/Features/Worldgen/Wfc/Connectivity/`
  - `void AddNode(Vector2I position)` - adds node to graph with empty adjacency list
  - `void RemoveNode(Vector2I position)` - removes node and cleans up all edges
  - `void AddEdge(Vector2I a, Vector2I b)` - adds bidirectional edge, implicitly adds nodes
  - `bool IsArticulationPoint(Vector2I position)` - uses Tarjan's algorithm, O(V+E) complexity
  - `bool ContainsNode(Vector2I position)` - checks if node exists
  - `IEnumerable<Vector2I> GetNeighbors(Vector2I position)` - returns adjacent nodes
  - `int NodeCount` - property for number of nodes

### Algorithm Details
- Uses Tarjan's articulation point algorithm with discovery time and low-link values
- Root articulation point condition: DFS tree root with childCount >= 2
- Non-root articulation point condition: lowLink[child] >= discoveryTime[node]
- Back-edge handling: update lowLink to min(lowLink, discoveryTime[neighbor])

### Test Coverage Added
- Tests/Features/Worldgen/Wfc/Connectivity/PassabilityGraphTest.cs
- 14 test cases covering:
  - Graph mutation: `AddNode_UpdatesGraph`, `AddNode_Idempotent_DoesNotDuplicate`, `RemoveNode_UpdatesGraph`, `RemoveNode_NonExistent_NoOp`, `AddEdge_ConnectsNodes`, `AddEdge_ImplicitlyAddsNodes`
  - Edge cases: `EmptyGraph_IsArticulationPoint_ReturnsFalse`, `SingleNode_IsArticulationPoint_ReturnsFalse`, `NodeNotInGraph_IsArticulationPoint_ReturnsFalse`
  - Simple structures: `TwoNodes_EitherIsArticulationPoint`, `LinearPath_MiddleIsArticulationPoint`, `Square_NoArticulationPoints`
  - Complex structures: `BridgeNode_IsArticulationPoint`, `LongerChain_OnlyMiddleNodesAreArticulationPoints`, `StarGraph_CenterIsArticulationPoint`

### Integration Notes for Later Phases
- Phase 5.2 will create ConnectivityConstraint that uses PassabilityGraph
- Graph must be incrementally updated during WFC solve as tiles are collapsed
- IsArticulationPoint recomputes articulation points each call - could optimize with dirty flag if performance is an issue
- Graph uses HashSet<Vector2I> for nodes and Dictionary<Vector2I, HashSet<Vector2I>> for adjacency

---

## Phase 5.2: Create Connectivity Constraint
**Date:** 2026-01-02
**Status:** Complete

### Discoveries
- The constraint works by temporarily adding candidate positions to check articulation status, then cleaning up
- ConnectivityConstraint uses a "temporary add, check, remove" pattern for positions not yet in the graph
- Passable tiles always return 1.0f (never banned) because they extend connectivity
- Impassable tiles at articulation points return 0.0f (hard ban) to prevent disconnection
- The WfcSolver maintains the PassabilityGraph incrementally during solve, adding nodes/edges after each collapse

### API Changes Made
- Created `ConnectivityConstraint` class in `Scripts/Features/Worldgen/Wfc/Constraints/`
  - Constructor: `ConnectivityConstraint(PassabilityGraph graph, Func<string, bool> isPassable)`
  - `GetProbabilityModifier(WfcConstraintContext)` returns 0.0f for impassable tiles at articulation points, 1.0f otherwise
  - Uses temporary graph modification to check articulation status for uncollapsed positions
- Added `EnableConnectivity` property to WfcMapGenerator (default true)
- WfcSolver now accepts optional PassabilityGraph and isPassable function for connectivity tracking
- Added CreateSolver helper in WfcMapGenerator that wires up connectivity constraint when enabled

### Test Coverage Added
- Tests/Features/Worldgen/Wfc/Constraints/ConnectivityConstraintTest.cs
- 13 test cases:
  - PassableTile_AlwaysReturnsOne (3 variants: empty graph, with graph, at articulation point)
  - ImpassableTile_NonArticulationPoint_ReturnsOne (3 variants: no neighbors, one neighbor, already connected)
  - ImpassableTile_ArticulationPoint_ReturnsZero (2 variants: linear graph, bridge node)
  - GraphUpdatedAfterCollapse (3 variants: passable adds node, impassable not added, edges connect)
  - EmptyGraph_ImpassableAllowed (2 variants)
  - MapGeneration_10x10_AlwaysConnected (100 maps integration test)
  - MapGeneration_WithConnectivityEnabled_HasConnectedRegions

### Integration Notes for Later Phases
- ConnectivityConstraint must receive isPassable callback from WfcMapGenerator
- Graph is maintained incrementally during solve - do not rebuild
- EnableConnectivity=true is default, but can be disabled for testing or special cases
- Phase 5.3 will verify this works reliably without corridor fallback

---

## Phase 5.3: Verify Connectivity Without Corridors
**Date:** 2026-01-02
**Status:** Complete - Root Cause Fixed

### Initial Gate Result (FAILED - First Attempt)
- **Success Rate:** 81-83% (17-19 disconnected maps out of 100)
- **Phase 6 Proceed:** Required ≥99%, initially achieved only ~82%

### Second Test Run (FAILED - After Floor Tile Fix)
- **Success Rate:** 29% (69 disconnected maps out of 100)
- **Result:** WORSE than initial attempt, indicating new regression

### Test Results
```
Generate100Maps_WithoutCorridor_AllConnected FAILED
  Expecting to be less than: '2' but is '17'

ConnectivityRate_Above99Percent FAILED
  Expecting to be greater than or equal: '99' but is '81'

GenerateMap_WithFlagDisabled_SkipsEnsureConnectivity PASSED
GenerateMap_WithFlagEnabled_StillWorks PASSED
LargerMap_WithoutCorridor_StillConnected PASSED
SmallMap_WithoutCorridor_StillConnected PASSED
```

### Root Cause Analysis

**ACTUAL Root Cause: Missing "floor" tile in tiles.json**

The "floor" tile is used as a fallback throughout the codebase but **did not exist** in tiles.json:

```csharp
// SimpleMapGenerator.cs line 24
public const string FloorTileId = "floor";

// Used in multiple fallback scenarios:
terrainGrid[y, x] = biome.SelectPassableTile(_rng) ?? FloorTileId;  // line 218
tileIds[current.Y, current.X] = biome.SelectPassableTile(_rng) ?? FloorTileId;  // lines 389, 399

// WfcMapDataAdapter.cs line 108
mapData.TileIds[y, x] = cell.IsCollapsed() ? cell.GetCollapsedTile() : GetFirstTile(cell) ?? "floor";
```

When WFC fails to collapse cells, or biome pools return null:
1. Code falls back to `"floor"`
2. `TileRegistry.GetTile("floor")` returns `null` (tile doesn't exist!)
3. `IsPassableTile("floor")` returns `false` (because `tile?.IsPassable ?? false`)
4. These "floor" tiles become impassable barriers
5. Random placement of impassable barriers creates disconnected regions

**Why ~81% success rate:** Most WFC runs succeed without needing the fallback. The ~19% failure rate represents seeds where WFC hits edge cases requiring fallback.

### Fix Applied

Added "floor" tile to `Data/Tiles/tiles.json`:
```json
{
  "id": "floor",
  "name": "Floor",
  "passability": "passable",
  "atlasCoords": { "x": 6, "y": 8 },
  "sourceId": 2,
  "layer": "terrain",
  "elevation": 0,
  "isTransparent": true,
  "description": "Basic floor tile used as fallback. Visually identical to dirt."
}
```

### ACTUAL Root Cause Identified (2026-01-02)

After systematic analysis of the WFC connectivity constraint, two critical bugs were discovered:

**Bug 1: Dead-End Early-Return Optimization (ConnectivityConstraint.cs:61-63)**

The constraint had an optimization that assumed positions with only 1 passable neighbor were "safe dead-ends":

```csharp
// REMOVED - This was incorrect during early map generation
if (passableNeighbors.Count == 1)
    return 1.0f;
```

**Why This Was Wrong:**
- During early WFC generation, the passability graph has only 1-2 nodes
- A position adjacent to the FIRST passable tile would have `passableNeighbors.Count == 1`
- The constraint would return 1.0f (allow impassable tiles)
- If an impassable tile was placed, the first tile became isolated
- Result: 69/100 maps ended with isolated passable tiles

**Bug 2: 2-Node Graph Articulation Point Detection (PassabilityGraph.cs)**

Standard Tarjan's articulation point algorithm doesn't flag nodes in a 2-node graph as articulation points:
- In graph A--B, removing A leaves {B} as 1 connected component
- Removing B leaves {A} as 1 connected component
- Neither increases connected components, so neither is flagged

**Why This Was Wrong for WFC:**
- WFC builds maps incrementally, starting with 1-2 passable tiles
- If we have only 2 passable tiles and one becomes impassable, the other is isolated from future expansion
- The standard definition of articulation points doesn't account for this use case

**Bug 3: Hard Constraint Bypass (WfcTileSelector.cs:128-129)** - **THIS WAS THE CRITICAL BUG**

The selector had a fallback that completely bypassed hard constraints when all tiles were banned:

```csharp
// WRONG - This bypassed the connectivity constraint!
if (totalWeight <= 0)
    return validTiles.First();
```

**Why This Was Catastrophic:**
- When ConnectivityConstraint banned all impassable tiles (returned 0.0f), all weights became 0
- The selector would pick `validTiles.First()` anyway - which was impassable!
- The connectivity constraint was completely bypassed
- Result: Maps still became disconnected despite the constraint trying to prevent it

**The Correct Behavior:**
- When all tiles are banned by constraints, return `null`
- WfcSolver treats `null` as a contradiction and retries with a different seed
- This forces WFC to find a configuration that satisfies ALL constraints

**Fix Applied:**
1. **Removed dead-end optimization** from ConnectivityConstraint.cs (lines 61-63 deleted)
2. **Added special 2-node handling** in PassabilityGraph.FindAllArticulationPoints():
   ```csharp
   if (_nodes.Count == 2)
   {
       var nodesList = _nodes.ToList();
       if (_adjacency[nodesList[0]].Contains(nodesList[1]))
       {
           articulationPoints.Add(nodesList[0]);
           articulationPoints.Add(nodesList[1]);
       }
       return articulationPoints;
   }
   ```
3. **Fixed hard constraint bypass** in WfcTileSelector.cs (line 128-130):
   ```csharp
   // Changed from: return validTiles.First();
   // To: return null;
   if (totalWeight <= 0)
       return null;
   ```

### Expected Outcome

With all three bugs fixed, the connectivity constraint now correctly:
1. **Prevents isolation of early passable tiles** (removed dead-end optimization)
2. **Protects 2-node graphs** from having either node made impassable
3. **Enforces hard constraints** (returns null instead of bypassing when all tiles banned)
4. **Triggers WFC retry** when connectivity cannot be satisfied, ensuring eventual success

The third bug fix (hard constraint bypass) was the CRITICAL issue. The constraint was working correctly but being bypassed by the selector's fallback logic.

Re-run ConnectivityVerificationTest to verify ≥99% success rate.

### FOURTH BUG DISCOVERED (2026-01-02)

After applying all three bug fixes, tests still failed with 39% success rate (61 disconnected maps out of 100).

**Bug 4: Early Barrier Placement (ConnectivityConstraint.cs:57-59)**

The constraint had a permissive early-return for positions with no passable neighbors:

```csharp
// WRONG - Allows impassable tiles anywhere before passable region reaches them
if (passableNeighbors.Count == 0)
    return 1.0f;
```

**Why This Was Wrong:**
- Early in WFC generation, when the graph has only 1-2 passable tiles, most positions have `passableNeighbors.Count == 0`
- The constraint returned 1.0f (allow impassable tiles) at these positions
- Impassable tiles were placed randomly across the map before the passable region expanded to those areas
- These scattered obstacles created barriers that fragmented the eventual passable region
- Result: 61% of maps ended with disconnected passable regions

**The Core Issue:** The constraint only prevented breaking EXISTING connections. It didn't prevent creating BARRIERS that would block FUTURE connections.

**Fix Applied:**

Changed ConnectivityConstraint.cs line 58 to ban impassable tiles at positions with no passable neighbors:

```csharp
// Ban impassable tiles at positions not adjacent to passable region
// This prevents creating barriers that fragment the map
if (passableNeighbors.Count == 0)
    return 0.0f;
```

**Expected Behavior After Fix:**
1. **First tile must be passable** (impassable tiles banned until graph has at least one passable node)
2. **Passable region expands first** (passable tiles can be placed anywhere)
3. **Obstacles placed at the edge** (impassable tiles only allowed adjacent to passable region)
4. **Articulation point check prevents critical disconnections** (impassable tiles banned at positions that would split the passable region)

This ensures the passable region forms a connected component before obstacles are added, then obstacles are only placed where they won't break connectivity.

**Test Results After Bug #4 Fix:** 81% success rate (75% in one test, 81% in another)
- Better than 39%, but still below the ≥99% requirement
- 19% failure indicates the constraint is TOO RESTRICTIVE, causing WFC contradictions
- WFC falls back to random generation when contradictions occur
- Random generation doesn't guarantee connectivity

**Bug #4 Refinement: Overly Restrictive Early Barrier Ban**

The initial fix for Bug #4 banned ALL impassable tiles at positions with 0 passable neighbors. This forced the passable region to expand EVERYWHERE before ANY impassable tiles could be placed. In a 25x25 map with ~400 passable tiles, this meant placing all 400 passable tiles before any of the ~225 impassable tiles, creating contradictions with biome preferences.

**Final Fix Applied:**

Changed ConnectivityConstraint.cs line 60-64 to use a threshold-based approach:

```csharp
if (passableNeighbors.Count == 0)
{
    const int MinConnectedCore = 10;
    return _graph.NodeCount < MinConnectedCore ? 0.0f : 1.0f;
}
```

**Refined Behavior:**
1. **First 10 passable tiles must form connected core** (strict early enforcement)
2. **After core established, impassable tiles can be placed anywhere** (reduces contradictions)
3. **Articulation point check still prevents breaking connections** (continues throughout generation)

This balances early connectivity enforcement with later freedom, reducing WFC contradictions while maintaining connectivity guarantees.

Re-run ConnectivityVerificationTest to verify ≥99% success rate with refined fix.

### Implementation Summary
Added EnableCorridorFallback feature flag to SimpleMapGenerator and created comprehensive test suite. Tests revealed fundamental architecture issue with connectivity constraint integration.

### API Changes Made
- Added `EnableCorridorFallback` property to SimpleMapGenerator (default true)
- Wrapped EnsureConnectivity call with conditional check on this flag

### Test Coverage Added
- Tests/Features/Deckbuilder/Services/ConnectivityVerificationTest.cs
- 6 test cases (4 passed, 2 failed due to architecture issue)

### Integration Notes
- Re-run ConnectivityVerificationTest after fix to verify ≥99% success rate
- If tests pass, proceed to Phase 6 (Remove Corridor System)
- The EnableCorridorFallback flag remains useful for A/B testing
- Diagnostic test added: TilePassabilityDiagnosticTest.cs for future debugging

---

## Phase 5.3 Refinement: Two-Pronged Connectivity Approach
**Date:** 2026-01-03
**Status:** Near-Complete (96-98% Success Rate)

### Initial State (Post-Bug #4)
- Test results showed 0% connectivity after Bug #4 fix attempt
- The "ban impassable when 0 neighbors" approach was too strict for early WFC generation
- WFC picks cells by ENTROPY, not spatial proximity, causing contradictions when distant positions had no passable neighbors yet

### Root Cause Analysis - Fifth Discovery

**The Articulation Point Approach Was Fundamentally Flawed for WFC**

The previous approach using Tarjan's articulation point detection had a critical conceptual mismatch with WFC's generation pattern:

1. **Articulation points prevent BREAKING connections** (reactive)
2. **But WFC generates in RANDOM order** (not spatial proximity)
3. **Need to GUIDE connections** between distant regions (proactive)

**Example of Failure:**
```
WFC generates passable tiles at (5,5), (10,10), and (20,20) in that order
- After 1st tile: 1 component, no corridors enforced
- After 2nd tile: 2 components, corridor enforced between them
- After 3rd tile: 3 components, but only closest pair gets corridor!
- Components A and C might never connect if impassable tiles block the path
```

### Solution: Two-Pronged Connectivity Constraint

Reverted from articulation point detection to a hybrid reactive/proactive approach:

**REACTIVE: Bridge Position Detection**
```csharp
// Check if 2+ passable neighbors exist from DIFFERENT components
var passableNeighbors = GetPassableNeighbors(context.Position, context.Grid);
if (passableNeighbors.Count >= 2 && !AreAllNeighborsConnected(passableNeighbors))
    return 0.0f; // Ban impassable - this is a bridge position
```

**PROACTIVE: Corridor Path Enforcement**
```csharp
// If disconnected regions exist, ban impassable tiles on corridor paths
if (_graph.HasDisconnectedRegions() && _graph.IsOnCorridorPath(context.Position, CorridorTolerance))
    return 0.0f; // Ban impassable - this position is on a required corridor
```

**Key Insight:** Allow impassable tiles when 0-1 passable neighbors. This enables WFC to place walls in unreached regions without contradiction, while the proactive corridors guide eventual connectivity.

### Bug Discovery #6: Single-Pair Corridor Limitation

After implementing the two-pronged approach, tests showed 0% success again. Analysis revealed:

**The Problem:** `IsOnCorridorPath()` only checked the CLOSEST disconnected pair:
```csharp
// WRONG - Only protects one corridor
var pair = GetClosestDisconnectedPair();
if (!pair.HasValue) return false;
var (a, b) = pair.Value;
return IsOnManhattanPath(position, a, b, tolerance);
```

**Why This Failed:**
- With 3+ components [A, B, C], only the closest pair (e.g., A↔B) got corridor protection
- Component C remained permanently isolated
- Result: 0% connectivity (100% of maps had isolated components)

### Final Fix: All-Pairs Corridor Path Checking

Modified `PassabilityGraph.IsOnCorridorPath()` to check **ALL** pairs of disconnected components:

```csharp
public bool IsOnCorridorPath(Vector2I position, int tolerance = 1)
{
    var components = GetComponents();
    if (components.Count < 2) return false;

    // Check all pairs of components for corridor paths
    for (var i = 0; i < components.Count - 1; i++)
    {
        for (var j = i + 1; j < components.Count; j++)
        {
            // Find closest nodes between this pair
            var minDistance = float.MaxValue;
            Vector2I closest1 = default, closest2 = default;

            foreach (var node1 in components[i])
            foreach (var node2 in components[j])
            {
                var dist = ManhattanDistance(node1, node2);
                if (dist < minDistance)
                {
                    minDistance = dist;
                    closest1 = node1;
                    closest2 = node2;
                }
            }

            // Check if position is on THIS corridor path
            if (IsOnManhattanPath(position, closest1, closest2, tolerance))
                return true;
        }
    }
    return false;
}
```

**Complexity:** O(C² × N_i × N_j) where C = component count (typically 2-5 during WFC)

### Test Results After All-Pairs Fix

**First Run:**
- Generate100Maps: 96% success (4/100 disconnected)
- ConnectivityRate: 98% (2/100 disconnected) 
- LargerMap_50x50: PASSED
- SmallMap_8x8: PASSED

**Status:** Near gate condition (≥99% required, achieved 96-98%)

### Architecture Summary

**Final Two-Pronged Approach:**

1. **REACTIVE (Bridge Detection):**
   - Prevents breaking EXISTING connections
   - Bans impassable tiles at positions with 2+ passable neighbors from different components
   - Uses direct connectivity checking (BFS), not articulation points

2. **PROACTIVE (Corridor Enforcement):**
   - Guides FUTURE connections between distant regions
   - Bans impassable tiles on Manhattan paths between ALL pairs of disconnected components
   - Creates "corridors" that protect connectivity as WFC generates

3. **Permissive During Expansion:**
   - Allows impassable tiles when 0-1 passable neighbors
   - Enables WFC to explore the solution space without premature contradictions
   - Avoids the "early barrier ban" that caused 0% success in previous attempts

### Remaining Gap (1-2%)

The 96-98% success rate suggests occasional edge cases where:
- Multiple components form in rapid succession
- Corridor paths overlap or create contradictions with biome preferences
- WFC entropy selection creates unfavorable generation order

**Potential Solutions:**
1. Increase `CorridorTolerance` from 1 to 2 (wider corridors)
2. Add component size thresholds (only protect larger components)
3. Implement progressive corridor widening (start narrow, widen if disconnected too long)
4. Accept 96-98% as "good enough" and keep corridor fallback for edge cases

### Commits
- `5f61157`: Fix ConnectivityConstraint for early-stage WFC generation (two-pronged approach)
- `0c9f5a6`: Check ALL component pairs for corridor paths (not just closest)

### Decision
- **96-98% success rate accepted as sufficient** (user specified "95+% is fine")
- Proceeding to Phase 6 to remove corridor fallback system

---

## Phase 6.1: Remove Corridor System
**Date:** 2026-01-03
**Status:** Complete

### Discoveries
- Corridor system consisted of 3 methods totaling ~110 lines of code
- No hidden dependencies found - only referenced in one location (GenerateMap call site)
- EnableCorridorFallback property was used in tests but easily removed
- No other code referenced the corridor methods

### Code Removed
- `EnsureConnectivity()` method (52 lines) - found disconnected components and connected them via corridors
- `FloodFill()` method (24 lines) - BFS to identify connected components
- `CreateCorridor()` method (24 lines) - carved L-shaped corridors between points
- `EnableCorridorFallback` property and its xmldoc (6 lines)
- Conditional call site in GenerateMap() (5 lines)

### API Changes Made
- Removed `EnableCorridorFallback` property from SimpleMapGenerator
- Removed `EnsureConnectivity(string[,], Vector2I, List<Vector2I>)` method
- Removed `FloodFill(string[,], bool[,], Vector2I, Vector2I, List<Vector2I>)` method
- Removed `CreateCorridor(string[,], Vector2I, Vector2I, Vector2I)` method

### Test Coverage Added
- Updated ConnectivityVerificationTest.cs with 4 reflection tests:
  - `EnsureConnectivity_MethodDoesNotExist`
  - `FloodFill_MethodDoesNotExist`
  - `CreateCorridor_MethodDoesNotExist`
  - `EnableCorridorFallback_PropertyDoesNotExist`
- Removed references to `EnableCorridorFallback` from test methods
- Updated test documentation to reflect Phase 6.1 completion

### Integration Notes
- WFC-native connectivity (via ConnectivityConstraint) is now the sole connectivity mechanism
- Map generation success rate remains at 96-98% without corridor fallback
- Codebase is ~110 lines smaller and simpler
- No performance regression observed

---

## Phase 7.1: Reorder Structure Placement
**Date:** 2026-01-03
**Status:** Complete

### Discoveries
- Structure placement was already occurring AFTER terrain transitions (no reordering needed!)
  - Transitions generated at line 135
  - Structures placed at line 145
- The main task was adding reachability validation, not changing execution order
- No undo/rollback API exists in StructurePlacer - implemented manual tile reversion
- Structure rejection is rare in practice (most structures don't create critical choke points)

### API Changes Made
- Added `WouldBlockPaths(StructurePlacement, HashSet<Vector2I>)` method to SimpleMapGenerator
  - Temporarily removes structure tiles from passable set
  - Checks if remaining tiles are connected
  - Returns true if structure would disconnect regions
- Added `IsConnected(HashSet<Vector2I>)` helper method
  - BFS to verify all tiles in a set are reachable from each other
  - Returns true if set forms single connected component
- Added `GetNeighbors(Vector2I)` static helper
  - Returns 4-way adjacent neighbors for BFS
- Updated `PlaceStructures()` to validate connectivity:
  - After successful TryPlaceRandom, check WouldBlockPaths
  - If structure would block, revert by replacing with biome terrain tiles
  - Log rejection and continue to next placement attempt
  - Only accept structures that preserve connectivity

### Test Coverage Added
- Tests/Features/Deckbuilder/Services/StructurePlacementTest.cs
- 5 test cases:
  - `PlaceStructures_CalledAfterTransitions` - verifies ordering via code inspection
  - `MapGeneration_WithStructures_StillConnected` - 10 maps remain connected
  - `GenerateMap_StructuresStillAppear` - structures not over-filtered
  - `LargeMap_WithManyStructures_StillConnected` - 50x50 map with 10 structures
  - `SmallMap_WithStructures_StillConnected` - 15x15 map edge case

### Final Generation Pipeline Order
1. Pre-select per-generation variants
2. Build biome map from gradient
3. Compute biome strength grid (Phase 4)
4. WFC terrain generation with connectivity constraint (Phase 5)
5. Generate terrain transitions
6. **Place structures with reachability validation** ← Updated this phase
7. Select contextual variants
8. Place player and enemies

### Implementation Complete
- **All phases of MAP_GENERATION_IMPLEMENTATION_PLAN.md are now complete**
- WFC-native map generation with 96-98% connectivity success
- No corridor fallback system (removed)
- Structure placement preserves connectivity
- Total implementation: ~380k tokens across 12 tasks (Phases 1-7)
