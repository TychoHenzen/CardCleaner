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
- Old interfaces (ISoftModifier, IHardConstraint) still exist for backward compatibility
- Phase 3.3 will remove ISoftModifier and IHardConstraint after migrating all consumers
- Constraint application in WfcTileSelector uses early-exit: if any constraint returns 0.0f, weight becomes 0 and remaining constraints are skipped
- AdjacencyConstraint constructor requires WfcAdjacencyRules instance
