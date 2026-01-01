# Map Generation V2 - Implementation Plan

**Purpose:** Atomic, context-independent tasks for implementing the WFC-native map generation system described in `MAP_GENERATION_REMEDIATION_QUESTIONNAIRE_V2.md`.

**Execution Model:** Context compaction is DISABLED. Each task is designed to be completed in a single context window (~100k usable tokens). Context is discarded between tasks.

**Selected Design Decisions (from V2 Questionnaire):**
| Decision Area | Selected Option |
|--------------|-----------------|
| Biome Gradient System | Option B: Pre-computed Biome Weight Grid |
| Hard/Soft Rule Dichotomy | Option B: Unified IWfcConstraint Interface |
| Clearest Winner Selection | Option A: Weighted Shannon Entropy |
| WFC-Native Connectivity | Option A: Articulation Point Prevention |
| Structure Generation | Option A: Post-Terrain with Reachability Validation |
| Deterministic RNG | Option C: Card Seed + Saved Seed Logging |

---

## Context Transfer Protocol

Since context is discarded between tasks, each task must:
1. Be self-contained with all necessary file paths and method signatures
2. Check memory for prior phase lessons before starting
3. Write discoveries to persistent storage after completing
4. Prove correctness with automated tests

### Lessons Storage (Persistent)
**Primary:** `docs/MAP_GENERATION_LESSONS.md` - append-only log of discoveries
**Secondary:** Memory system via `mem-search` skill - searchable across sessions

**DO NOT** store lessons in `.solve-session/` as it is cleared on task completion.

### Lessons File Format (`docs/MAP_GENERATION_LESSONS.md`)
```markdown
## Phase {N}.{Task}: {Title}
**Date:** YYYY-MM-DD
**Status:** Complete

### Discoveries
- What was found that wasn't expected

### API Changes Made
- Method signature changes with old → new

### Test Coverage Added
- List of test files created/modified
- Key test cases

### Integration Notes for Later Phases
- Ordering requirements, gotchas
- Dependencies discovered
```

### Task Instruction Template
Each task follows this structure:
1. **Prerequisites**: Which phases must be complete
2. **Lessons to Check**: Search memory for prior phase discoveries
3. **Files to Read**: Specific files needed (with paths)
4. **Acceptance Criteria**: Verifiable success conditions
5. **Required Tests**: Test files to create/modify with specific test cases
6. **Implementation Steps**: Concrete actions
7. **Verification**: Build/test commands that MUST pass
8. **Lessons Output**: What to append to `docs/MAP_GENERATION_LESSONS.md`

---

## Phase 1: Foundation (Deterministic RNG)

### T1.1: Implement Card-Based RNG Seeding

**Complexity:** Small (~20k tokens)

**Prerequisites:** None (first task)

**Lessons to Check:** None (first task)

**Context:**
The current `GameSessionService._Ready()` uses `_rng.Randomize()` which seeds from system time, making map generation non-reproducible. We need deterministic seeding so the same input cards always produce the same map.

**Files to Read:**
1. `Scripts/Features/Deckbuilder/Services/GameSessionService.cs` - Lines 123-140 (\_Ready method), Lines 60-82 (StartSession)
2. `Scripts/Features/Card/Models/CardSignature.cs` - Understand the 8D signature structure

**Files to Modify:**
1. `Scripts/Features/Deckbuilder/Services/GameSessionService.cs`

**Required Tests:**
Create or update: `Tests/Features/Deckbuilder/Services/GameSessionServiceTest.cs`

| Test Case | Description |
|-----------|-------------|
| `ComputeSeedFromCards_SameCards_ReturnsSameSeed` | Verify identical card lists produce identical seeds |
| `ComputeSeedFromCards_DifferentCards_ReturnsDifferentSeeds` | Verify different cards produce different seeds |
| `ComputeSeedFromCards_OrderMatters` | Verify [A,B] produces different seed than [B,A] |
| `GenerateMap_SameSeed_ProducesSameMap` | Verify same seed → same player start, enemy positions |

**Acceptance Criteria:**
1. `ComputeSeedFromCards()` method exists and produces consistent hash from card signatures
2. `_rng.Randomize()` call is replaced with `_rng.Seed = ComputeSeedFromCards(_mapSeeds)`
3. Seed value is logged with `ILog.Print($"Map seed: {seed}")` for debugging
4. All 4 required tests pass
5. Running StartSession with same cards twice produces identical map (verified by test)

**Implementation Steps:**
1. Add private method `ComputeSeedFromCards(List<CardSignature> cards) → ulong`:
```csharp
private ulong ComputeSeedFromCards(List<CardSignature> cards)
{
    var hash = 17UL;
    foreach (var card in cards)
    {
        for (int i = 0; i < 8; i++)
        {
            // Use BitConverter for deterministic float→bits conversion
            hash = hash * 31 + BitConverter.ToUInt32(BitConverter.GetBytes(card[i]), 0);
        }
    }
    return hash;
}
```
2. In `_Ready()`: Remove `_rng.Randomize();`
3. In `GenerateMap()`: Before creating gradient, add:
```csharp
var seed = ComputeSeedFromCards(_mapSeeds);
_rng.Seed = seed;
ILog.Print($"Map seed: {seed}");
```
4. Add `using System;` if BitConverter not available

**Verification:**
```bash
dotnet build
dotnet test --filter "FullyQualifiedName~GameSessionServiceTest"
```
All 4 required tests MUST pass before task is complete.

**Lessons Output:**
Append to `docs/MAP_GENERATION_LESSONS.md`:
```markdown
## Phase 1.1: Implement Card-Based RNG Seeding
**Date:** {date}
**Status:** Complete

### Discoveries
- {Any unexpected findings}

### API Changes Made
- Added `ComputeSeedFromCards(List<CardSignature>) → ulong` to GameSessionService
- Removed `_rng.Randomize()` from `_Ready()`

### Test Coverage Added
- Tests/Features/Deckbuilder/Services/GameSessionServiceTest.cs
- 4 test cases for seed determinism

### Integration Notes for Later Phases
- Seed is computed in GenerateMap(), not in StartSession()
- {Any other notes}
```

---

## Phase 2: Weighted Entropy Selection

### T2.1: Implement Weighted Shannon Entropy

**Complexity:** Medium (~40k tokens)

**Prerequisites:** Phase 1 complete (for deterministic testing)

**Lessons to Check:** Search memory for "Phase 1.1" or check `docs/MAP_GENERATION_LESSONS.md`

**Context:**
Current `WfcCellState.GetEntropy()` returns `_possibleTiles.Count` - just the number of options. We need to use Shannon entropy that accounts for probability weights, so cells with one dominant option have lower entropy (clearer winner).

**Files to Read:**
1. `Scripts/Features/Worldgen/Wfc/WfcCellState.cs` - Full file, understand current entropy
2. `Scripts/Features/Worldgen/Wfc/WfcGrid.cs` - Lines 152-213, entropy methods
3. `Scripts/Features/Worldgen/Wfc/WfcTileSelector.cs` - Lines 57-135, how weights are calculated
4. `Scripts/Features/Worldgen/Wfc/WfcSolver.cs` - How entropy is used for cell selection

**Files to Modify:**
1. `Scripts/Features/Worldgen/Wfc/WfcCellState.cs` - Add weighted entropy method
2. `Scripts/Features/Worldgen/Wfc/WfcGrid.cs` - Update cell selection to use weights
3. `Scripts/Features/Worldgen/Wfc/WfcSolver.cs` - Pass weights to grid entropy methods

**Required Tests:**
Create: `Tests/Features/Worldgen/Wfc/WfcEntropyTest.cs`

| Test Case | Description |
|-----------|-------------|
| `GetWeightedEntropy_SingleTile_ReturnsZero` | Collapsed cell has zero entropy |
| `GetWeightedEntropy_UniformWeights_ReturnsMaxEntropy` | Equal weights = highest entropy |
| `GetWeightedEntropy_DominantTile_ReturnsLowEntropy` | 90% weight on one tile = low entropy |
| `GetWeightedEntropy_Deterministic` | Same weights always return same entropy |
| `GetLowestEntropyCellWeighted_SelectsDominantCell` | Cell with clearest winner is selected |

**Acceptance Criteria:**
1. `WfcCellState.GetWeightedEntropy(IReadOnlyDictionary<string, float> weights)` method exists
2. Returns Shannon entropy: `-Σ(p * log(p))` where p = weight/totalWeight
3. `WfcGrid.GetLowestEntropyCellWeighted()` method exists accepting weight calculator
4. WfcSolver passes current biome weights to entropy calculation
5. All 5 required tests pass
6. Cells with one dominant tile (e.g., 90% weight) have lower entropy than uniform cells (verified by test)

**Implementation Steps:**
1. Add to `WfcCellState.cs`:
```csharp
/// <summary>
/// Calculates weighted Shannon entropy based on tile probabilities.
/// Lower entropy = clearer winner (more certainty about which tile to pick).
/// </summary>
public float GetWeightedEntropy(IReadOnlyDictionary<string, float> weights)
{
    if (_possibleTiles.Count <= 1)
        return 0f; // Already collapsed or contradiction

    var totalWeight = 0f;
    foreach (var tileId in _possibleTiles)
    {
        totalWeight += weights.TryGetValue(tileId, out var w) ? w : 1f;
    }

    if (totalWeight <= 0f)
        return float.MaxValue;

    var entropy = 0f;
    foreach (var tileId in _possibleTiles)
    {
        var weight = weights.TryGetValue(tileId, out var w) ? w : 1f;
        if (weight <= 0f) continue;

        var p = weight / totalWeight;
        entropy -= p * Mathf.Log(p);
    }

    return entropy;
}
```

2. Add to `WfcGrid.cs`:
```csharp
/// <summary>
/// Finds lowest weighted-entropy cell with random tiebreak.
/// Uses provided weight calculator for position-aware weights.
/// </summary>
public Vector2I? GetLowestEntropyCellWeighted(
    Func<Vector2I, IReadOnlyDictionary<string, float>> getWeightsAt,
    RandomNumberGenerator rng)
{
    var lowestEntropy = float.MaxValue;
    var candidates = new List<Vector2I>();

    for (var y = 0; y < _height; y++)
    {
        for (var x = 0; x < _width; x++)
        {
            var cell = _cells[y, x];
            if (cell.IsCollapsed()) continue;

            var pos = new Vector2I(x, y);
            var weights = getWeightsAt(pos);
            var entropy = cell.GetWeightedEntropy(weights);

            if (entropy < lowestEntropy)
            {
                lowestEntropy = entropy;
                candidates.Clear();
                candidates.Add(pos);
            }
            else if (Mathf.IsEqualApprox(entropy, lowestEntropy))
            {
                candidates.Add(pos);
            }
        }
    }

    if (candidates.Count == 0) return null;
    return candidates[rng.RandiRange(0, candidates.Count - 1)];
}
```

3. Update `WfcSolver` to use weighted entropy:
   - Add weight calculator parameter or use existing biome weights
   - Call `GetLowestEntropyCellWeighted` instead of `GetLowestEntropyCellWithTieBreak`

4. Ensure backward compatibility: keep old methods working

**Verification:**
```bash
dotnet build
dotnet test --filter "FullyQualifiedName~WfcEntropyTest"
```
All 5 required tests MUST pass before task is complete.

**Lessons Output:**
Append to `docs/MAP_GENERATION_LESSONS.md`:
```markdown
## Phase 2.1: Implement Weighted Shannon Entropy
**Date:** {date}
**Status:** Complete

### Discoveries
- {Any unexpected findings}

### API Changes Made
- Added `GetWeightedEntropy(IReadOnlyDictionary<string, float>)` to WfcCellState
- Added `GetLowestEntropyCellWeighted(Func<Vector2I, IReadOnlyDictionary<string, float>>, RNG)` to WfcGrid

### Test Coverage Added
- Tests/Features/Worldgen/Wfc/WfcEntropyTest.cs
- 5 test cases for weighted entropy calculation

### Integration Notes for Later Phases
- Weight calculator signature: `Func<Vector2I, IReadOnlyDictionary<string, float>>`
- {Any performance notes}
```

---

## Phase 3: Unified Constraint Interface

### T3.1: Create IWfcConstraint Interface

**Complexity:** Small (~15k tokens)

**Prerequisites:** None (additive only)

**Lessons to Check:** None (no dependencies)

**Context:**
Currently we have separate `IHardConstraint` (returns bool) and `ISoftModifier` (returns float). We need a unified `IWfcConstraint` where all constraints return a probability modifier (0.0 = banned, 1.0 = neutral, >1.0 = boosted).

**Files to Read:**
1. `Scripts/Features/Worldgen/Wfc/Modifiers/Hard/IHardConstraint.cs` - Current hard interface
2. `Scripts/Features/Worldgen/Wfc/Modifiers/Soft/ISoftModifier.cs` - Current soft interface

**Files to Create:**
1. `Scripts/Features/Worldgen/Wfc/Constraints/IWfcConstraint.cs`
2. `Scripts/Features/Worldgen/Wfc/Constraints/WfcConstraintContext.cs`

**Required Tests:**
Create: `Tests/Features/Worldgen/Wfc/Constraints/WfcConstraintContextTest.cs`

| Test Case | Description |
|-----------|-------------|
| `WfcConstraintContext_InitializesCorrectly` | Context struct holds all required properties |
| `WfcConstraintContext_DefaultRngIsNull` | Optional RNG property defaults correctly |

**Acceptance Criteria:**
1. `IWfcConstraint` interface exists with `float GetProbabilityModifier(WfcConstraintContext context)`
2. `WfcConstraintContext` contains: Position, TileId, Grid, and any other needed context
3. Return value semantics documented: 0.0 = hard ban, 0.0-1.0 = penalty, 1.0 = neutral, >1.0 = boost
4. All 2 required tests pass
5. Project builds with new files (no breaking changes yet)

**Implementation Steps:**
1. Create directory: `Scripts/Features/Worldgen/Wfc/Constraints/`
2. Create `WfcConstraintContext.cs`:
```csharp
using Godot;

namespace CardCleaner.Scripts.Features.Worldgen.Wfc.Constraints;

/// <summary>
/// Context provided to WFC constraints for probability calculation.
/// </summary>
public readonly struct WfcConstraintContext
{
    /// <summary>Grid position being evaluated.</summary>
    public Vector2I Position { get; init; }

    /// <summary>Tile ID being evaluated.</summary>
    public string TileId { get; init; }

    /// <summary>Current WFC grid state.</summary>
    public WfcGrid Grid { get; init; }

    /// <summary>Random number generator for stochastic constraints.</summary>
    public RandomNumberGenerator? Rng { get; init; }
}
```

3. Create `IWfcConstraint.cs`:
```csharp
namespace CardCleaner.Scripts.Features.Worldgen.Wfc.Constraints;

/// <summary>
/// Unified constraint interface for WFC tile selection.
/// Replaces separate IHardConstraint and ISoftModifier with single probability-based interface.
/// </summary>
/// <remarks>
/// Return value semantics:
/// - 0.0: Hard ban - tile is eliminated from possibilities
/// - 0.0-1.0: Soft penalty - reduces probability proportionally
/// - 1.0: Neutral - no effect on probability
/// - >1.0: Boost - increases probability proportionally
///
/// Constraints are applied multiplicatively: final_weight = base_weight × Π(modifiers)
/// </remarks>
public interface IWfcConstraint
{
    /// <summary>
    /// Calculates the probability modifier for placing a tile at a position.
    /// </summary>
    /// <param name="context">Position, tile, and grid context</param>
    /// <returns>Probability modifier (0.0 = ban, 1.0 = neutral, >1.0 = boost)</returns>
    float GetProbabilityModifier(WfcConstraintContext context);
}
```

4. Verify build succeeds

**Verification:**
```bash
dotnet build
dotnet test --filter "FullyQualifiedName~WfcConstraintContextTest"
```
All 2 required tests MUST pass before task is complete.

**Lessons Output:**
Append to `docs/MAP_GENERATION_LESSONS.md`:
```markdown
## Phase 3.1: Create IWfcConstraint Interface
**Date:** {date}
**Status:** Complete

### Discoveries
- {Any unexpected findings}

### API Changes Made
- Created `IWfcConstraint` interface in `Scripts/Features/Worldgen/Wfc/Constraints/`
- Created `WfcConstraintContext` struct

### Test Coverage Added
- Tests/Features/Worldgen/Wfc/Constraints/WfcConstraintContextTest.cs
- 2 test cases for context initialization

### Integration Notes for Later Phases
- Namespace: `CardCleaner.Scripts.Features.Worldgen.Wfc.Constraints`
- Context properties: Position, TileId, Grid, Rng (optional)
```

---

### T3.2: Migrate Constraints to IWfcConstraint

**Complexity:** Medium (~40k tokens)

**Prerequisites:** T3.1 complete

**Lessons to Check:** Search memory for "Phase 3.1" or check `docs/MAP_GENERATION_LESSONS.md` for namespace and context properties

**Context:**
Convert all existing soft modifiers to implement `IWfcConstraint`, and create an adjacency constraint wrapper. The old interfaces remain for now (removed in T3.3).

**Files to Read:**
1. `Scripts/Features/Worldgen/Wfc/Modifiers/Soft/DiminishingReturnsSoftModifier.cs`
2. `Scripts/Features/Worldgen/Wfc/Modifiers/Soft/NoveltySoftModifier.cs`
3. `Scripts/Features/Worldgen/Wfc/Modifiers/Soft/CompactnessSoftModifier.cs`
4. `Scripts/Features/Worldgen/Wfc/WfcAdjacencyRules.cs`
5. `Scripts/Features/Worldgen/Wfc/WfcTileSelector.cs`

**Files to Modify:**
1. All three soft modifier files - implement IWfcConstraint additionally
2. `Scripts/Features/Worldgen/Wfc/WfcTileSelector.cs` - support both interfaces during transition

**Files to Create:**
1. `Scripts/Features/Worldgen/Wfc/Constraints/AdjacencyConstraint.cs`

**Required Tests:**
Create: `Tests/Features/Worldgen/Wfc/Constraints/ConstraintMigrationTest.cs`

| Test Case | Description |
|-----------|-------------|
| `DiminishingReturns_BothInterfacesReturnSameValue` | IWfcConstraint and ISoftModifier return same multiplier |
| `Novelty_BothInterfacesReturnSameValue` | IWfcConstraint and ISoftModifier return same multiplier |
| `Compactness_BothInterfacesReturnSameValue` | IWfcConstraint and ISoftModifier return same multiplier |
| `AdjacencyConstraint_ValidAdjacency_ReturnsOne` | Valid neighbor → 1.0 |
| `AdjacencyConstraint_InvalidAdjacency_ReturnsZero` | Invalid neighbor → 0.0 |
| `WfcTileSelector_ConstraintList_AppliesMultiplicatively` | Multiple constraints multiply together |

**Acceptance Criteria:**
1. DiminishingReturnsSoftModifier implements both ISoftModifier AND IWfcConstraint
2. NoveltySoftModifier implements both interfaces
3. CompactnessSoftModifier implements both interfaces
4. AdjacencyConstraint exists, returns 0.0 for invalid adjacencies, 1.0 for valid
5. WfcTileSelector has method accepting `List<IWfcConstraint>`
6. All 6 required tests pass
7. All existing tests still pass (backward compatible)

**Implementation Steps:**
1. Update each soft modifier to implement IWfcConstraint:
```csharp
// In DiminishingReturnsSoftModifier.cs
public class DiminishingReturnsSoftModifier : ISoftModifier, IWfcConstraint
{
    // Existing code...

    public float GetProbabilityModifier(WfcConstraintContext context)
    {
        // Reuse existing logic
        var softContext = new SoftModifierContext
        {
            Position = context.Position,
            TileId = context.TileId,
            Grid = context.Grid
        };
        return CalculateMultiplier(softContext);
    }
}
```

2. Create `AdjacencyConstraint.cs`:
```csharp
using CardCleaner.Scripts.Features.Worldgen.Wfc.Constraints;

namespace CardCleaner.Scripts.Features.Worldgen.Wfc.Constraints;

/// <summary>
/// Hard constraint wrapper around WfcAdjacencyRules.
/// Returns 0.0 for invalid adjacencies, 1.0 for valid.
/// </summary>
public class AdjacencyConstraint : IWfcConstraint
{
    private readonly WfcAdjacencyRules _rules;

    public AdjacencyConstraint(WfcAdjacencyRules rules)
    {
        _rules = rules;
    }

    public float GetProbabilityModifier(WfcConstraintContext context)
    {
        // Check all collapsed neighbors
        foreach (var neighborPos in context.Grid.GetNeighbors(context.Position))
        {
            var neighborTile = context.Grid.GetCollapsedTileAt(neighborPos);
            if (neighborTile == null) continue;

            // Get direction from neighbor to this position
            var direction = GetDirection(neighborPos, context.Position);
            if (!_rules.CanBeAdjacent(neighborTile, context.TileId, direction))
            {
                return 0.0f; // Hard ban
            }
        }
        return 1.0f; // Valid
    }

    private static Direction GetDirection(Vector2I from, Vector2I to)
    {
        // Implement direction calculation
    }
}
```

3. Add to WfcTileSelector:
```csharp
private readonly List<IWfcConstraint> _constraints = new();

public void AddConstraint(IWfcConstraint constraint) => _constraints.Add(constraint);

// In SelectTile, after existing modifier logic:
if (_constraints.Count > 0)
{
    var constraintContext = new WfcConstraintContext
    {
        Position = position.Value,
        TileId = tileId,
        Grid = grid
    };

    foreach (var constraint in _constraints)
    {
        var modifier = constraint.GetProbabilityModifier(constraintContext);
        if (modifier == 0f)
        {
            weight = 0f;
            break;
        }
        weight *= modifier;
    }
}
```

**Verification:**
```bash
dotnet build
dotnet test --filter "FullyQualifiedName~ConstraintMigrationTest"
dotnet test --filter "FullyQualifiedName~Wfc"
```
All 6 new tests MUST pass AND all existing WFC tests must still pass.

**Lessons Output:**
Append to `docs/MAP_GENERATION_LESSONS.md`:
```markdown
## Phase 3.2: Migrate Constraints to IWfcConstraint
**Date:** {date}
**Status:** Complete

### Discoveries
- {Any unexpected findings}
- {How Direction enum is defined/used for adjacency}

### API Changes Made
- DiminishingReturnsSoftModifier now implements IWfcConstraint
- NoveltySoftModifier now implements IWfcConstraint
- CompactnessSoftModifier now implements IWfcConstraint
- Created AdjacencyConstraint class
- Added `AddConstraint(IWfcConstraint)` to WfcTileSelector

### Test Coverage Added
- Tests/Features/Worldgen/Wfc/Constraints/ConstraintMigrationTest.cs
- 6 test cases for constraint migration

### Integration Notes for Later Phases
- AdjacencyConstraint requires Direction enum from: {location}
- Old interfaces still exist for backward compatibility
```

---

### T3.3: Remove Legacy Constraint Interfaces

**Complexity:** Small (~25k tokens)

**Prerequisites:** T3.2 complete

**Lessons to Check:** Search memory for "Phase 3.1" and "Phase 3.2" or check `docs/MAP_GENERATION_LESSONS.md`

**Context:**
With all constraints implementing IWfcConstraint, remove the legacy `IHardConstraint` and `ISoftModifier` interfaces and update all consumers.

**Files to Read:**
1. Grep for all uses of `IHardConstraint` and `ISoftModifier`
2. `Scripts/Features/Worldgen/Wfc/WfcTileSelector.cs`
3. `Scripts/Features/Worldgen/Wfc/WfcPropagator.cs`
4. `Scripts/Features/Worldgen/Wfc/WfcMapGenerator.cs`

**Files to Delete:**
1. `Scripts/Features/Worldgen/Wfc/Modifiers/Hard/IHardConstraint.cs`
2. `Scripts/Features/Worldgen/Wfc/Modifiers/Soft/ISoftModifier.cs`

**Files to Modify:**
1. All soft modifier files - remove ISoftModifier, keep only IWfcConstraint
2. WfcTileSelector - remove `_softModifiers` list, use only `_constraints`
3. WfcPropagator - use AdjacencyConstraint instead of direct rules
4. WfcMapGenerator - update modifier registration

**Required Tests:**
Update: `Tests/Features/Worldgen/Wfc/Constraints/ConstraintMigrationTest.cs`

| Test Case | Description |
|-----------|-------------|
| `LegacyInterfaces_DoNotExist` | Compilation check - ISoftModifier and IHardConstraint are gone |
| `WfcMapGenerator_UsesOnlyConstraints` | Generator uses AddConstraint, not AddModifier |
| `MapGeneration_NoRegression` | Generated maps have same statistical properties as before |

**Acceptance Criteria:**
1. No references to `IHardConstraint` or `ISoftModifier` in codebase
2. All constraint registration uses `AddConstraint(IWfcConstraint)`
3. WfcTileSelector only has `_constraints` list
4. All 3 new tests pass
5. All existing tests pass
6. Map generation produces similar results (verified by regression test)

**Implementation Steps:**
1. Search codebase for all usages:
```bash
grep -r "IHardConstraint" --include="*.cs"
grep -r "ISoftModifier" --include="*.cs"
grep -r "AddModifier" --include="*.cs"
```

2. Update each soft modifier:
   - Remove `: ISoftModifier` from class declaration
   - Remove `CalculateMultiplier(SoftModifierContext)` method
   - Keep only `GetProbabilityModifier(WfcConstraintContext)`

3. Update WfcTileSelector:
   - Remove `_softModifiers` field
   - Remove `AddModifier(ISoftModifier)` method
   - Rename `AddConstraint` if needed
   - Update SelectTile to only use constraints

4. Update WfcMapGenerator:
   - Change `_selector.AddModifier(x)` to `_selector.AddConstraint(x)`
   - Add AdjacencyConstraint registration

5. Delete legacy interface files

6. Update namespaces/usings in all affected files

**Verification:**
```bash
dotnet build
dotnet test
grep -r "ISoftModifier" --include="*.cs" Scripts/  # Should return nothing
grep -r "IHardConstraint" --include="*.cs" Scripts/  # Should return nothing
```
All tests MUST pass AND grep commands must return no matches.

**Lessons Output:**
Append to `docs/MAP_GENERATION_LESSONS.md`:
```markdown
## Phase 3.3: Remove Legacy Constraint Interfaces
**Date:** {date}
**Status:** Complete

### Discoveries
- {Files that unexpectedly referenced old interfaces}
- {Any unexpected consumers found}

### API Changes Made
- Deleted IHardConstraint interface
- Deleted ISoftModifier interface
- Removed CalculateMultiplier() from all soft modifiers
- WfcTileSelector now only uses _constraints list

### Test Coverage Added
- Updated ConstraintMigrationTest.cs with 3 additional tests
- Regression test for map generation

### Integration Notes for Later Phases
- All new constraints must implement IWfcConstraint only
- Use AddConstraint() for registration
```

---

## Phase 4: Biome Gradient System

### T4.1: Create Biome Strength Grid

**Complexity:** Medium (~35k tokens)

**Prerequisites:** Phase 3 complete (unified constraints)

**Lessons to Check:** Search memory for "Phase 3" or check `docs/MAP_GENERATION_LESSONS.md` for IWfcConstraint usage patterns

**Context:**
Per the V2 design (Option B), we pre-compute a `float[y, x, biomeCount]` grid of biome strengths before WFC runs. This allows O(1) lookup during tile selection instead of per-tile gradient calculation.

**Files to Read:**
1. `Scripts/Features/Worldgen/CardBasedGradient.cs` - Current gradient implementation
2. `Scripts/Features/Worldgen/Biomes/BiomeRegistry.cs` - How biomes are registered
3. `Scripts/Features/Worldgen/Biomes/BiomeDefinition.cs` - Biome structure

**Files to Create:**
1. `Scripts/Features/Worldgen/Biomes/BiomeStrengthGrid.cs`
2. `Scripts/Features/Worldgen/Wfc/Constraints/BiomeAffinityConstraint.cs`

**Required Tests:**
Create: `Tests/Features/Worldgen/Biomes/BiomeStrengthGridTest.cs`

| Test Case | Description |
|-----------|-------------|
| `GetStrength_ValidPosition_ReturnsValue` | Grid returns value in [-1, 1] range |
| `GetStrength_UnknownBiome_ReturnsZero` | Unknown biome ID returns 0 |
| `Computation_75x75Grid_CompletesUnder100ms` | Performance requirement |
| `BiomeAffinityConstraint_PositiveBiome_ReturnsBoost` | Positive affinity + positive strength = boost |
| `BiomeAffinityConstraint_NegativeBiome_ReturnsPenalty` | Negative affinity + positive strength = penalty |
| `BiomeAffinityConstraint_NeutralBiome_ReturnsOne` | Neutral affinity = 1.0 |

**Acceptance Criteria:**
1. BiomeStrengthGrid class exists with `float GetStrength(Vector2I position, string biomeId)`
2. Grid is computed from CardBasedGradient before WFC starts
3. BiomeAffinityConstraint uses grid for position-based tile boosting
4. All 6 required tests pass
5. Grid computation takes < 100ms for 75x75 map (verified by test)

**Implementation Steps:**
1. Create `BiomeStrengthGrid.cs`:
```csharp
using System.Collections.Generic;
using CardCleaner.Scripts.Features.Card.Models;
using Godot;

namespace CardCleaner.Scripts.Features.Worldgen.Biomes;

/// <summary>
/// Pre-computed grid of biome strengths at each position.
/// Computed once before WFC, enabling O(1) lookup during tile selection.
/// </summary>
public class BiomeStrengthGrid
{
    private readonly float[,,] _strengths; // [y, x, biomeIndex]
    private readonly Dictionary<string, int> _biomeToIndex;
    private readonly int _width;
    private readonly int _height;

    public BiomeStrengthGrid(
        Vector2I size,
        BaselineGradient gradient,
        BiomeRegistry registry)
    {
        _width = size.X;
        _height = size.Y;

        var biomes = registry.GetAllBiomes().ToList();
        _biomeToIndex = new Dictionary<string, int>();
        for (int i = 0; i < biomes.Count; i++)
        {
            _biomeToIndex[biomes[i].Id] = i;
        }

        _strengths = new float[_height, _width, biomes.Count];
        ComputeStrengths(gradient, biomes, size);
    }

    /// <summary>
    /// Gets the biome strength at a position (-1 to +1 range).
    /// </summary>
    public float GetStrength(Vector2I position, string biomeId)
    {
        if (!_biomeToIndex.TryGetValue(biomeId, out var index))
            return 0f;
        return _strengths[position.Y, position.X, index];
    }

    private void ComputeStrengths(
        BaselineGradient gradient,
        List<BiomeDefinition> biomes,
        Vector2I size)
    {
        for (int y = 0; y < _height; y++)
        {
            for (int x = 0; x < _width; x++)
            {
                var pos = new Vector2I(x, y);
                var signature = gradient.GetSignatureAt(pos, size);

                for (int b = 0; b < biomes.Count; b++)
                {
                    // Calculate biome affinity from signature
                    var biome = biomes[b];
                    var strength = CalculateBiomeStrength(signature, biome);
                    _strengths[y, x, b] = strength;
                }
            }
        }
    }

    private static float CalculateBiomeStrength(CardSignature signature, BiomeDefinition biome)
    {
        // Use biome's signature pattern to calculate affinity
        // This should match BiomeMapGenerator's logic
        // Returns -1 to +1
        return biome.CalculateAffinity(signature);
    }
}
```

2. Create `BiomeAffinityConstraint.cs`:
```csharp
using CardCleaner.Scripts.Features.Worldgen.Biomes;

namespace CardCleaner.Scripts.Features.Worldgen.Wfc.Constraints;

/// <summary>
/// Adjusts tile probability based on biome strength at position.
/// Tiles with positive affinity for strong biomes get boosted.
/// </summary>
public class BiomeAffinityConstraint : IWfcConstraint
{
    private readonly BiomeStrengthGrid _grid;
    private readonly BiomeRegistry _registry;

    public BiomeAffinityConstraint(BiomeStrengthGrid grid, BiomeRegistry registry)
    {
        _grid = grid;
        _registry = registry;
    }

    public float GetProbabilityModifier(WfcConstraintContext context)
    {
        // For each biome, calculate: biomeStrength × tileAffinity
        // Sum all contributions
        var totalModifier = 1.0f;

        foreach (var biome in _registry.GetAllBiomes())
        {
            var biomeStrength = _grid.GetStrength(context.Position, biome.Id);
            var tileAffinity = GetTileAffinity(context.TileId, biome.Id);

            // Convert to multiplier: neutral = 1.0
            // positive affinity + positive strength = boost
            // negative affinity + positive strength = penalty
            totalModifier += biomeStrength * tileAffinity * 0.5f; // Tunable factor
        }

        return Mathf.Max(0.01f, totalModifier); // Prevent zero
    }

    private float GetTileAffinity(string tileId, string biomeId)
    {
        // TODO: Implement tile-biome affinity lookup
        // Per V2 notes: positive/neutral/negative per biome
        // Returns: +1 (positive), 0 (neutral), -1 (negative)
        return 0f; // Default neutral
    }
}
```

**Verification:**
```bash
dotnet build
dotnet test --filter "FullyQualifiedName~BiomeStrengthGridTest"
```
All 6 required tests MUST pass before task is complete.

**Lessons Output:**
Append to `docs/MAP_GENERATION_LESSONS.md`:
```markdown
## Phase 4.1: Create Biome Strength Grid
**Date:** {date}
**Status:** Complete

### Discoveries
- {How biome affinity calculation works}
- {Grid computation performance results}

### API Changes Made
- Created BiomeStrengthGrid class with GetStrength(Vector2I, string)
- Created BiomeAffinityConstraint implementing IWfcConstraint

### Test Coverage Added
- Tests/Features/Worldgen/Biomes/BiomeStrengthGridTest.cs
- 6 test cases including performance test

### Integration Notes for Later Phases
- Tile affinity configuration approach: {describe}
- BiomeStrengthGrid constructor requires: gradient, registry, size
```

---

### T4.2: Integrate Biome Grid into Generation

**Complexity:** Medium (~40k tokens)

**Prerequisites:** T4.1 complete

**Lessons to Check:** Search memory for "Phase 4.1" or check `docs/MAP_GENERATION_LESSONS.md` for BiomeStrengthGrid API

**Context:**
Connect the BiomeStrengthGrid into the WFC generation pipeline. Also implement tile-biome affinity configuration using positive/neutral/negative designation per biome (per V2 notes).

**Files to Read:**
1. `Scripts/Features/Worldgen/Wfc/WfcMapGenerator.cs` - Lines 179-244 (GenerateMultiBiome)
2. `Scripts/Features/Deckbuilder/Tiles/TileDefinition.cs` or equivalent
3. `Scripts/Features/Worldgen/Biomes/BiomeDefinition.cs`

**Files to Modify:**
1. `Scripts/Features/Worldgen/Wfc/WfcMapGenerator.cs` - Add BiomeStrengthGrid usage
2. `Scripts/Features/Deckbuilder/Tiles/TileDefinition.cs` or `BiomeDefinition.cs` - Add affinity config
3. `Scripts/Features/Worldgen/Wfc/Constraints/BiomeAffinityConstraint.cs` - Implement affinity lookup

**Required Tests:**
Create: `Tests/Features/Worldgen/Wfc/BiomeGridIntegrationTest.cs`

| Test Case | Description |
|-----------|-------------|
| `GenerateMultiBiome_CreatesAndUsesBiomeGrid` | Grid is created and constraint registered |
| `TileAffinity_DefaultsToPositiveForBiomeTiles` | Tiles in TilePool get positive affinity |
| `TileAffinity_DefaultsToNeutralForOtherTiles` | Tiles not in TilePool get neutral affinity |
| `MapGeneration_ShowsBiomeInfluence` | Tiles appropriate to biome appear in biome regions |
| `BiomeAffinity_Enum_HasThreeValues` | BiomeAffinity has Positive, Neutral, Negative |

**Acceptance Criteria:**
1. GenerateMultiBiome creates BiomeStrengthGrid before WFC
2. BiomeAffinityConstraint is registered with selector
3. Tile-biome affinity is configurable (enum: Positive, Neutral, Negative)
4. Tiles in biome's TilePool default to Positive affinity
5. All 5 required tests pass
6. Map generation shows position-based biome influence (verified visually and by test)

**Implementation Steps:**
1. Add affinity enum and configuration:
```csharp
public enum BiomeAffinity
{
    Negative = -1,
    Neutral = 0,
    Positive = 1
}

// In TileDefinition or BiomeDefinition
public Dictionary<string, BiomeAffinity> BiomeAffinities { get; set; } = new();
```

2. Update BiomeAffinityConstraint to use actual affinity lookup:
```csharp
private float GetTileAffinity(string tileId, string biomeId)
{
    var tileDef = _tileRegistry.GetTile(tileId);
    if (tileDef?.BiomeAffinities.TryGetValue(biomeId, out var affinity) == true)
    {
        return (int)affinity;
    }

    // Default: if tile is in biome's pool, positive; otherwise neutral
    var biome = _registry.GetBiome(biomeId);
    if (biome?.PassableTiles.Contains(tileId) == true)
    {
        return 1f;
    }

    return 0f;
}
```

3. Update WfcMapGenerator.GenerateMultiBiome():
```csharp
public WfcGenerationResult GenerateMultiBiome(
    BiomeRegistry biomeRegistry,
    Func<Vector2I, BiomeDefinition> getBiomeAt,
    BaselineGradient gradient, // Add gradient parameter
    Vector2I size,
    ulong seed)
{
    // Compute biome strength grid
    var biomeGrid = new BiomeStrengthGrid(size, gradient, biomeRegistry);

    // Create affinity constraint
    var affinityConstraint = new BiomeAffinityConstraint(biomeGrid, biomeRegistry, _tileRegistry);
    _selector.AddConstraint(affinityConstraint);

    // Rest of existing logic...
}
```

4. Update callers to pass gradient

**Verification:**
```bash
dotnet build
dotnet test --filter "FullyQualifiedName~BiomeGridIntegrationTest"
```
All 5 required tests MUST pass before task is complete.

**Lessons Output:**
Append to `docs/MAP_GENERATION_LESSONS.md`:
```markdown
## Phase 4.2: Integrate Biome Grid into Generation
**Date:** {date}
**Status:** Complete

### Discoveries
- {Final affinity configuration approach}
- {Visual quality improvements observed}

### API Changes Made
- Added BiomeAffinity enum (Positive, Neutral, Negative)
- Updated GenerateMultiBiome signature to accept gradient
- Added BiomeAffinities property to TileDefinition/BiomeDefinition

### Test Coverage Added
- Tests/Features/Worldgen/Wfc/BiomeGridIntegrationTest.cs
- 5 test cases for biome grid integration

### Integration Notes for Later Phases
- BiomeAffinityConstraint must be registered before other constraints
- Gradient must be passed to GenerateMultiBiome
```

---

## Phase 5: WFC-Native Connectivity

### T5.1: Implement Articulation Point Detection

**Complexity:** Large (~50k tokens)

**Prerequisites:** Phase 3 complete

**Lessons to Check:** Search memory for "Phase 3" or check `docs/MAP_GENERATION_LESSONS.md` for IWfcConstraint patterns

**Context:**
Implement Tarjan's algorithm for finding articulation points in a graph. An articulation point is a vertex whose removal disconnects the graph. We'll use this to prevent WFC from placing impassable tiles that would disconnect passable regions.

**Files to Read:**
1. Research Tarjan's articulation point algorithm
2. `Scripts/Features/Worldgen/Wfc/WfcGrid.cs` - Grid structure reference

**Files to Create:**
1. `Scripts/Features/Worldgen/Wfc/Connectivity/PassabilityGraph.cs`

**Required Tests:**
Create: `Tests/Features/Worldgen/Wfc/Connectivity/PassabilityGraphTest.cs`

| Test Case | Description |
|-----------|-------------|
| `EmptyGraph_IsArticulationPoint_ReturnsFalse` | Empty graph has no articulation points |
| `SingleNode_IsArticulationPoint_ReturnsFalse` | Single node cannot disconnect |
| `TwoNodes_EitherIsArticulationPoint` | Removing either disconnects |
| `LinearPath_MiddleIsArticulationPoint` | Middle node in A-B-C is articulation point |
| `Square_NoArticulationPoints` | 2x2 grid has no articulation points |
| `BridgeNode_IsArticulationPoint` | Node connecting two regions is articulation point |
| `AddNode_UpdatesGraph` | Adding node correctly updates adjacency |
| `RemoveNode_UpdatesGraph` | Removing node correctly updates adjacency |
| `AddEdge_ConnectsNodes` | Edge addition works |

**Acceptance Criteria:**
1. PassabilityGraph class represents passable tiles as graph nodes
2. `bool IsArticulationPoint(Vector2I position)` returns true if removing position creates disconnection
3. Algorithm is O(V+E) per check
4. All 9 required tests pass
5. Unit tests cover: empty graph, single node, linear path, 2x2 square, bridge nodes

**Implementation Steps:**
1. Create PassabilityGraph with standard graph operations:
```csharp
using System.Collections.Generic;
using Godot;

namespace CardCleaner.Scripts.Features.Worldgen.Wfc.Connectivity;

/// <summary>
/// Graph representation of passable tiles for connectivity analysis.
/// Supports incremental updates and articulation point detection.
/// </summary>
public class PassabilityGraph
{
    private readonly HashSet<Vector2I> _nodes = new();
    private readonly Dictionary<Vector2I, HashSet<Vector2I>> _adjacency = new();

    public void AddNode(Vector2I position)
    {
        if (_nodes.Add(position))
        {
            _adjacency[position] = new HashSet<Vector2I>();
        }
    }

    public void RemoveNode(Vector2I position)
    {
        if (!_nodes.Remove(position)) return;

        // Remove all edges to this node
        foreach (var neighbor in _adjacency[position])
        {
            _adjacency[neighbor].Remove(position);
        }
        _adjacency.Remove(position);
    }

    public void AddEdge(Vector2I a, Vector2I b)
    {
        if (!_nodes.Contains(a) || !_nodes.Contains(b)) return;
        _adjacency[a].Add(b);
        _adjacency[b].Add(a);
    }

    /// <summary>
    /// Checks if removing this node would disconnect the graph.
    /// Uses Tarjan's algorithm for articulation point detection.
    /// </summary>
    public bool IsArticulationPoint(Vector2I position)
    {
        if (!_nodes.Contains(position)) return false;
        if (_nodes.Count <= 2) return false; // Can't disconnect with 2 or fewer nodes

        // Tarjan's algorithm for single-point check
        var visited = new Dictionary<Vector2I, int>();
        var low = new Dictionary<Vector2I, int>();
        var parent = new Dictionary<Vector2I, Vector2I?>();
        var time = 0;
        var isArticulation = false;

        void Dfs(Vector2I node)
        {
            visited[node] = low[node] = time++;
            int children = 0;

            foreach (var neighbor in _adjacency[node])
            {
                if (neighbor == position) continue; // Skip the node we're testing

                if (!visited.ContainsKey(neighbor))
                {
                    children++;
                    parent[neighbor] = node;
                    Dfs(neighbor);
                    low[node] = Math.Min(low[node], low[neighbor]);

                    // Articulation point conditions
                    if (parent[node] == null && children > 1)
                        isArticulation = true;
                    if (parent[node] != null && low[neighbor] >= visited[node])
                        isArticulation = true;
                }
                else if (!parent.TryGetValue(node, out var p) || neighbor != p)
                {
                    low[node] = Math.Min(low[node], visited[neighbor]);
                }
            }
        }

        // Start DFS from a neighbor of the test position
        var startNode = GetAnyNeighborExcept(position);
        if (startNode == null) return true; // No neighbors means removing it disconnects

        parent[startNode.Value] = null;
        Dfs(startNode.Value);

        // Check if all nodes (except the test position) were visited
        return visited.Count < _nodes.Count - 1;
    }

    private Vector2I? GetAnyNeighborExcept(Vector2I exclude)
    {
        foreach (var node in _nodes)
        {
            if (node != exclude) return node;
        }
        return null;
    }

    public int NodeCount => _nodes.Count;
}
```

2. Write comprehensive tests

**Verification:**
```bash
dotnet build
dotnet test --filter "FullyQualifiedName~PassabilityGraphTest"
```
All 9 required tests MUST pass before task is complete.

**Lessons Output:**
Append to `docs/MAP_GENERATION_LESSONS.md`:
```markdown
## Phase 5.1: Implement Articulation Point Detection
**Date:** {date}
**Status:** Complete

### Discoveries
- {Algorithm complexity analysis}
- {Edge cases discovered during testing}

### API Changes Made
- Created PassabilityGraph class with AddNode, RemoveNode, AddEdge, IsArticulationPoint

### Test Coverage Added
- Tests/Features/Worldgen/Wfc/Connectivity/PassabilityGraphTest.cs
- 9 test cases covering graph operations and articulation point detection

### Integration Notes for Later Phases
- IsArticulationPoint uses Tarjan's algorithm: O(V+E)
- Graph must be incrementally updated during WFC solve
- {Any optimizations deferred}
```

---

### T5.2: Create Connectivity Constraint

**Complexity:** Medium (~40k tokens)

**Prerequisites:** T5.1 complete

**Lessons to Check:** Search memory for "Phase 5.1" or check `docs/MAP_GENERATION_LESSONS.md` for PassabilityGraph API

**Context:**
Create the IWfcConstraint implementation that uses PassabilityGraph to return 0.0 for tiles that would disconnect the passable region.

**Files to Read:**
1. `Scripts/Features/Worldgen/Wfc/Connectivity/PassabilityGraph.cs`
2. `Scripts/Features/Worldgen/Wfc/Constraints/IWfcConstraint.cs`
3. `Scripts/Features/Worldgen/Wfc/WfcSolver.cs`

**Files to Create:**
1. `Scripts/Features/Worldgen/Wfc/Constraints/ConnectivityConstraint.cs`

**Files to Modify:**
1. `Scripts/Features/Worldgen/Wfc/WfcSolver.cs` - Maintain PassabilityGraph during solve
2. `Scripts/Features/Worldgen/Wfc/WfcMapGenerator.cs` - Register connectivity constraint

**Required Tests:**
Create: `Tests/Features/Worldgen/Wfc/Constraints/ConnectivityConstraintTest.cs`

| Test Case | Description |
|-----------|-------------|
| `PassableTile_AlwaysReturnsOne` | Passable tiles never banned |
| `ImpassableTile_NonArticulationPoint_ReturnsOne` | Safe impassable tile allowed |
| `ImpassableTile_ArticulationPoint_ReturnsZero` | Disconnecting tile banned |
| `GraphUpdated_AfterCollapse` | Graph reflects collapsed tiles |
| `EmptyGraph_ImpassableAllowed` | First tiles can be impassable |
| `MapGeneration_10x10_AlwaysConnected` | 100 generated 10x10 maps are all connected |

**Acceptance Criteria:**
1. ConnectivityConstraint implements IWfcConstraint
2. Returns 0.0 if placing impassable tile would disconnect regions
3. Returns 1.0 otherwise
4. PassabilityGraph is updated incrementally during WFC solve
5. All 6 required tests pass
6. Test verifies: 10x10 map never has disconnected passable regions

**Implementation Steps:**
1. Create ConnectivityConstraint:
```csharp
namespace CardCleaner.Scripts.Features.Worldgen.Wfc.Constraints;

public class ConnectivityConstraint : IWfcConstraint
{
    private readonly PassabilityGraph _graph;
    private readonly Func<string, bool> _isPassable;

    public ConnectivityConstraint(PassabilityGraph graph, Func<string, bool> isPassable)
    {
        _graph = graph;
        _isPassable = isPassable;
    }

    public float GetProbabilityModifier(WfcConstraintContext context)
    {
        // If placing a passable tile, it extends connectivity - always OK
        if (_isPassable(context.TileId))
            return 1.0f;

        // If placing impassable tile at position that's currently passable,
        // check if it would disconnect the graph
        if (_graph.IsArticulationPoint(context.Position))
            return 0.0f; // Hard ban

        return 1.0f;
    }
}
```

2. Update WfcSolver to maintain graph:
```csharp
// In WfcSolver, after collapsing a cell:
if (_passabilityGraph != null)
{
    var tileId = cell.GetCollapsedTile();
    if (_isPassable(tileId))
    {
        _passabilityGraph.AddNode(position);
        // Add edges to adjacent passable tiles
        foreach (var neighbor in grid.GetNeighbors(position))
        {
            var neighborTile = grid.GetCollapsedTileAt(neighbor);
            if (neighborTile != null && _isPassable(neighborTile))
            {
                _passabilityGraph.AddEdge(position, neighbor);
            }
        }
    }
}
```

3. Register constraint in WfcMapGenerator

**Verification:**
```bash
dotnet build
dotnet test --filter "FullyQualifiedName~ConnectivityConstraintTest"
```
All 6 required tests MUST pass before task is complete.

**Lessons Output:**
Append to `docs/MAP_GENERATION_LESSONS.md`:
```markdown
## Phase 5.2: Create Connectivity Constraint
**Date:** {date}
**Status:** Complete

### Discoveries
- {Integration approach with WfcSolver}
- {Performance impact measurements}
- {Edge cases with early-game empty graph}

### API Changes Made
- Created ConnectivityConstraint implementing IWfcConstraint
- Added PassabilityGraph field to WfcSolver
- Added graph update logic after each collapse

### Test Coverage Added
- Tests/Features/Worldgen/Wfc/Constraints/ConnectivityConstraintTest.cs
- 6 test cases including statistical verification

### Integration Notes for Later Phases
- ConnectivityConstraint must receive isPassable callback
- Graph is maintained incrementally - do not rebuild
- {Performance notes}
```

---

### T5.3: Verify Connectivity Without Corridors

**Complexity:** Small (~25k tokens)

**Prerequisites:** T5.2 complete

**Lessons to Check:** Search memory for "Phase 5.2" or check `docs/MAP_GENERATION_LESSONS.md` for ConnectivityConstraint integration

**Context:**
Before removing the corridor code (Phase 6), verify that WFC-native connectivity is working reliably. Temporarily disable corridors and measure success rate.

**Files to Read:**
1. `Scripts/Features/Deckbuilder/Services/SimpleMapGenerator.cs` - EnsureConnectivity location

**Files to Modify:**
1. `Scripts/Features/Deckbuilder/Services/SimpleMapGenerator.cs` - Add feature flag

**Required Tests:**
Create: `Tests/Features/Deckbuilder/Services/ConnectivityVerificationTest.cs`

| Test Case | Description |
|-----------|-------------|
| `Generate100Maps_WithoutCorridor_AllConnected` | 100 maps generated, all must be connected |
| `GenerateMap_WithFlagDisabled_SkipsEnsureConnectivity` | Flag works correctly |
| `ConnectivityRate_Above99Percent` | Statistical verification of success rate |

**GATE CONDITION:** This test determines if Phase 6 can proceed.
- If success rate >= 99%, proceed to T6.1
- If success rate < 99%, STOP and document issues

**Acceptance Criteria:**
1. Add `public bool EnableCorridorFallback { get; set; } = true` flag
2. When disabled, skip EnsureConnectivity call
3. All 3 required tests pass
4. Generate 100 maps with flag disabled, measure disconnection rate
5. Success rate must be >= 99% to proceed with Phase 6
6. If < 99%, document issues in lessons and DO NOT proceed to T6.1

**Implementation Steps:**
1. Add feature flag to SimpleMapGenerator:
```csharp
/// <summary>
/// Enable/disable corridor fallback for connectivity.
/// Set to false to rely on WFC-native connectivity constraint.
/// </summary>
public bool EnableCorridorFallback { get; set; } = true;
```

2. Wrap EnsureConnectivity call:
```csharp
if (EnableCorridorFallback)
{
    EnsureConnectivity(finalTileIds, size, passableTiles);
}
```

3. Create test that generates 100 maps and checks connectivity:
```csharp
[Test]
public void GenerateMap_WithoutCorridorFallback_IsConnected()
{
    var generator = CreateGenerator();
    generator.EnableCorridorFallback = false;

    var disconnectedCount = 0;
    for (int i = 0; i < 100; i++)
    {
        var map = generator.GenerateMap(new Vector2I(50, 50));
        if (!IsFullyConnected(map.PassableTiles))
        {
            disconnectedCount++;
        }
    }

    Assert.Less(disconnectedCount, 2, $"Disconnected maps: {disconnectedCount}/100");
}
```

**Verification:**
```bash
dotnet build
dotnet test --filter "FullyQualifiedName~ConnectivityVerificationTest"
```
All 3 required tests MUST pass. CRITICAL: Note the success rate for Phase 6 gate.

**Lessons Output:**
Append to `docs/MAP_GENERATION_LESSONS.md`:
```markdown
## Phase 5.3: Verify Connectivity Without Corridors
**Date:** {date}
**Status:** Complete

### Gate Result
- **Success Rate:** {X}% (100 maps tested)
- **Phase 6 Proceed:** {YES if >= 99%, NO if < 99%}

### Discoveries
- {Failure patterns observed, if any}
- {Types of maps that failed, if any}

### API Changes Made
- Added EnableCorridorFallback property to SimpleMapGenerator

### Test Coverage Added
- Tests/Features/Deckbuilder/Services/ConnectivityVerificationTest.cs
- 3 test cases including statistical verification

### Integration Notes for Later Phases
- {If failed: what needs to be fixed before Phase 6}
- {If passed: safe to remove corridor code}
```

---

## Phase 6: Legacy Code Removal

### T6.1: Remove Corridor System

**Complexity:** Small (~20k tokens)

**Prerequisites:** T5.3 complete with >= 99% success rate

**Lessons to Check:** Check `docs/MAP_GENERATION_LESSONS.md` for Phase 5.3 Gate Result - MUST confirm "Phase 6 Proceed: YES"

**Context:**
With WFC-native connectivity working, remove the corridor carving system entirely.

**IMPORTANT:** Do NOT start this task if Phase 5.3 reported < 99% success rate. Check lessons file first!

**Files to Modify:**
1. `Scripts/Features/Deckbuilder/Services/SimpleMapGenerator.cs`

**Code to Remove:**
1. `EnsureConnectivity()` method
2. `FloodFill()` method
3. `CreateCorridor()` method
4. `EnableCorridorFallback` property
5. The call site in `GenerateMap()`

**Required Tests:**
Update: `Tests/Features/Deckbuilder/Services/SimpleMapGeneratorTest.cs` (or create if doesn't exist)

| Test Case | Description |
|-----------|-------------|
| `EnsureConnectivity_MethodDoesNotExist` | Reflection check that method is gone |
| `GenerateMap_NoCorridorCode_StillConnected` | Maps remain connected after removal |
| `GenerateMap_Performance_NoRegression` | Generation time not significantly worse |

**Acceptance Criteria:**
1. No method named `EnsureConnectivity` exists
2. No method named `CreateCorridor` exists
3. No method named `FloodFill` exists (in this file)
4. All 3 required tests pass
5. All existing tests pass
6. Map generation still produces connected maps (verified by test)

**Implementation Steps:**
1. Verify Phase 5.3 lessons confirm > 99% success rate
2. Delete methods in order (smallest dependencies first):
   - FloodFill
   - CreateCorridor
   - EnsureConnectivity
3. Remove call site in GenerateMap
4. Remove EnableCorridorFallback property
5. Clean up any unused variables

**Verification:**
```bash
dotnet build
dotnet test --filter "FullyQualifiedName~SimpleMapGeneratorTest"
dotnet test  # Run all tests
grep -n "EnsureConnectivity\|CreateCorridor\|FloodFill" Scripts/Features/Deckbuilder/Services/SimpleMapGenerator.cs  # Should return nothing
```
All tests MUST pass AND grep must return no matches.

**Lessons Output:**
Append to `docs/MAP_GENERATION_LESSONS.md`:
```markdown
## Phase 6.1: Remove Corridor System
**Date:** {date}
**Status:** Complete

### Discoveries
- {Any unexpected references found}
- {Methods removed: list with line counts}

### API Changes Made
- Removed EnsureConnectivity() method
- Removed FloodFill() method
- Removed CreateCorridor() method
- Removed EnableCorridorFallback property

### Test Coverage Added
- Updated SimpleMapGeneratorTest.cs with 3 tests
- Reflection test confirms methods are gone

### Integration Notes for Later Phases
- WFC-native connectivity is now the only connectivity mechanism
- {Any performance notes}
```

---

## Phase 7: Structure Generation

### T7.1: Reorder Structure Placement

**Complexity:** Small (~30k tokens)

**Prerequisites:** Phase 6 complete

**Lessons to Check:** Check `docs/MAP_GENERATION_LESSONS.md` for Phase 6.1 completion confirmation

**Context:**
Move structure placement to after terrain is fully complete, and add reachability validation before placing structures.

**Files to Read:**
1. `Scripts/Features/Deckbuilder/Services/SimpleMapGenerator.cs` - Current PlaceStructures location
2. `Scripts/Features/Worldgen/Wfc/Connectivity/PassabilityGraph.cs` - Reuse for reachability

**Files to Modify:**
1. `Scripts/Features/Deckbuilder/Services/SimpleMapGenerator.cs`

**Required Tests:**
Update: `Tests/Features/Deckbuilder/Services/StructurePlacementTest.cs` (or create if doesn't exist)

| Test Case | Description |
|-----------|-------------|
| `PlaceStructures_CalledAfterTransitions` | Order verification via mock/spy |
| `WouldBlockPaths_BlockingStructure_Skipped` | Structure that would disconnect is rejected |
| `WouldBlockPaths_SafeStructure_Placed` | Structure that doesn't block is placed |
| `GenerateMap_WithStructures_StillConnected` | Maps with structures remain connected |
| `GenerateMap_StructuresStillAppear` | At least some structures placed (not over-filtered) |

**Acceptance Criteria:**
1. PlaceStructures() is called AFTER terrain transitions are generated
2. `WouldBlockPaths()` check exists before each structure placement
3. Structures that would disconnect regions are skipped
4. All 5 required tests pass
5. All existing tests pass
6. Structures still appear on maps (not over-filtered, verified by test)

**Implementation Steps:**
1. Add reachability check method:
```csharp
private bool WouldBlockPaths(StructurePlacement placement, HashSet<Vector2I> passableSet)
{
    // Build temporary graph without structure tiles
    var testSet = new HashSet<Vector2I>(passableSet);
    for (var dy = 0; dy < placement.Result.Size.Y; dy++)
    for (var dx = 0; dx < placement.Result.Size.X; dx++)
    {
        var structurePos = placement.Position + new Vector2I(dx, dy);
        testSet.Remove(structurePos);
    }

    // Check if remaining tiles are connected
    return !IsConnected(testSet);
}

private bool IsConnected(HashSet<Vector2I> tiles)
{
    if (tiles.Count <= 1) return true;

    var visited = new HashSet<Vector2I>();
    var queue = new Queue<Vector2I>();
    queue.Enqueue(tiles.First());

    while (queue.Count > 0)
    {
        var current = queue.Dequeue();
        if (!visited.Add(current)) continue;

        foreach (var neighbor in GetNeighbors(current))
        {
            if (tiles.Contains(neighbor) && !visited.Contains(neighbor))
            {
                queue.Enqueue(neighbor);
            }
        }
    }

    return visited.Count == tiles.Count;
}
```

2. Move PlaceStructures call to after terrain transitions:
```csharp
// Current order:
// - WFC terrain
// - Terrain transitions
// - PlaceStructures  <-- MOVE HERE
// - (Corridors removed)
// - Variants
// - Entities
```

3. Update PlaceStructures to use WouldBlockPaths:
```csharp
var placed = _structurePlacer.TryPlaceRandom(...);
if (placed)
{
    var lastPlacement = _structurePlacer.PlacedStructures[^1];
    if (WouldBlockPaths(lastPlacement, passableSet))
    {
        // Undo placement
        _structurePlacer.UndoLastPlacement();
        continue;
    }
    // ... rest of existing code
}
```

**Verification:**
```bash
dotnet build
dotnet test --filter "FullyQualifiedName~StructurePlacementTest"
dotnet test  # Run all tests
```
All 5 required tests MUST pass AND all existing tests must pass.

**Lessons Output:**
Append to `docs/MAP_GENERATION_LESSONS.md`:
```markdown
## Phase 7.1: Reorder Structure Placement
**Date:** {date}
**Status:** Complete

### Discoveries
- {Structure rejection rate}
- {Any StructurePlacer API changes needed}

### API Changes Made
- Added WouldBlockPaths() method to SimpleMapGenerator
- Added IsConnected() helper method
- Moved PlaceStructures() call to after transitions

### Test Coverage Added
- Tests/Features/Deckbuilder/Services/StructurePlacementTest.cs
- 5 test cases for structure placement ordering and validation

### Final Generation Pipeline Order
1. Pre-select per-generation variants
2. Build biome map
3. Compute biome strength grid
4. WFC terrain generation (with connectivity constraint)
5. Generate terrain transitions
6. Place structures (with reachability check)
7. Select contextual variants
8. Place player and enemies

### Integration Notes
- Implementation complete for V2 WFC-native map generation
```

---

## Execution Checklist

| Phase | Task | Prerequisites | Est. Tokens | Status |
|-------|------|--------------|-------------|--------|
| 1 | T1.1: Deterministic RNG | None | 20k | ⬜ |
| 2 | T2.1: Weighted Entropy | T1.1 | 40k | ⬜ |
| 3 | T3.1: IWfcConstraint Interface | None | 15k | ⬜ |
| 3 | T3.2: Migrate Constraints | T3.1 | 40k | ⬜ |
| 3 | T3.3: Remove Legacy | T3.2 | 25k | ⬜ |
| 4 | T4.1: Biome Strength Grid | T3.3 | 35k | ⬜ |
| 4 | T4.2: Integrate Biome Grid | T4.1 | 40k | ⬜ |
| 5 | T5.1: Articulation Points | T3.3 | 50k | ⬜ |
| 5 | T5.2: Connectivity Constraint | T5.1 | 40k | ⬜ |
| 5 | T5.3: Verify Without Corridors | T5.2 | 25k | ⬜ |
| 6 | T6.1: Remove Corridors | T5.3 (>99%) | 20k | ⬜ |
| 7 | T7.1: Reorder Structures | T6.1 | 30k | ⬜ |

**Total Estimated Tokens:** ~380k across 12 tasks

---

## Risk Mitigation

### Phase 3 Risk: Large Refactor
If T3.2 becomes too large, split into:
- T3.2a: Migrate soft modifiers only
- T3.2b: Create adjacency constraint
- T3.2c: Update selector

### Phase 5 Risk: Connectivity Not Reliable
If T5.3 shows < 99% success:
1. Keep corridor fallback temporarily
2. Analyze failure patterns
3. Add additional constraints (e.g., minimum passable percentage)
4. Retry T5.3 after improvements

### Phase 4 Risk: Performance
If BiomeStrengthGrid computation is slow:
1. Profile and optimize inner loop
2. Consider caching signature calculations
3. Reduce biome count in tests

---

*Generated from MAP_GENERATION_REMEDIATION_QUESTIONNAIRE_V2.md selected options*
