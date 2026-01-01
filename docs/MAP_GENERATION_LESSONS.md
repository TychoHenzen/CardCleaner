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
