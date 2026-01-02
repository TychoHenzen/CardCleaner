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
