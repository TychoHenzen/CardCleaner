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
