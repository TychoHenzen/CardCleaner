# Map Generation Pipeline Audit Report

**Generated:** January 2026
**Scope:** Button press → Map generation → Exploration agent initialization

---

## Executive Summary

This audit traces the complete map generation pipeline from UI trigger through exploration agent startup. The system uses Wave Function Collapse (WFC) for terrain generation with a multi-stage pipeline involving card signatures, biome mapping, and soft constraint modifiers.

### Key Finding Categories
- **Inconsistencies:** 3 identified
- **Gotchas:** 5 identified
- **Potential Bugs:** 4 identified
- **Unintuitive Behaviors:** 6 identified

---

## 1. Complete Flow Diagram

```
┌─────────────────────────────────────────────────────────────────────────┐
│                          BUTTON PRESS                                    │
│  DeckBuilderController.OnButtonPressed()                                │
│  └─ Consumes cards from MapCardSlot + AbilityDeckSlot                   │
│  └─ Calls WorldTileMapScreenScene.Initialize(mapSeeds, abilities)       │
└──────────────────────────────────┬──────────────────────────────────────┘
                                   ▼
┌─────────────────────────────────────────────────────────────────────────┐
│                     SimpleWorldMapScreen.Initialize()                    │
│  └─ If _serviceReady: StartSession() immediately                        │
│  └─ Else: Stores pending data for later callback                        │
│  └─ Calls gameSession.StartSession(mapSeeds, abilities)                 │
└──────────────────────────────────┬──────────────────────────────────────┘
                                   ▼
┌─────────────────────────────────────────────────────────────────────────┐
│                    GameSessionService.StartSession()                     │
│  State: WaitingForCards → GeneratingMap                                 │
│  └─ Stores _mapSeeds and _abilityCards                                  │
│  └─ CallDeferred(AdvanceSession)                                        │
└──────────────────────────────────┬──────────────────────────────────────┘
                                   ▼
┌─────────────────────────────────────────────────────────────────────────┐
│                    GameSessionService.GenerateMap()                      │
│  1. CalculateMapSize(_mapSeeds[0]) → size based on signature complexity │
│  2. CardBasedGradient(_mapSeeds, _rng) → spatial signature distribution │
│  3. BiomeMapGenerator(registry, gradient, size) → position→biome mapper │
│  4. WfcMapGenerator(transitionResolver) → terrain generator             │
│  5. SimpleMapGenerator(...) → orchestrates all components               │
│  6. mapGenerator.GenerateMap(size) → produces SimpleMapData             │
└──────────────────────────────────┬──────────────────────────────────────┘
                                   ▼
┌─────────────────────────────────────────────────────────────────────────┐
│                   SimpleMapGenerator.GenerateMap()                       │
│  1. SelectPerGenerationVariants() → per-tile-type random variant        │
│  2. Build biomeMap[y,x] from biomeProvider                              │
│  3. GenerateTerrainViaWfc() → WFC with hard constraints                 │
│  4. GenerateTerrainTransitions() → dual-grid visual system              │
│  5. PlaceStructures() → structure stamps on terrain                     │
│  6. EnsureConnectivity() → corridor flood-fill                          │
│  7. SelectContextualVariants() → position-aware variants                │
│  8. Place player + enemies randomly on passable tiles                   │
└──────────────────────────────────┬──────────────────────────────────────┘
                                   ▼
┌─────────────────────────────────────────────────────────────────────────┐
│                   WfcMapGenerator.GenerateMultiBiome()                   │
│  1. Collect union of tiles from all biomes                              │
│  2. ConfigureModifiers() → DiminishingReturns, Novelty, Compactness     │
│  3. WfcSolver.SolveWithRetry(createGrid, biome, seed, maxRetries)       │
│     ├─ WfcPropagator → 8-directional constraint propagation             │
│     ├─ WfcTileSelector → weighted tile selection with soft modifiers   │
│     └─ BlobSizeTracker → union-find for blob statistics                 │
│  4. WfcMapDataAdapter.ToSimpleMapData() → convert grid to output        │
└──────────────────────────────────┬──────────────────────────────────────┘
                                   ▼
┌─────────────────────────────────────────────────────────────────────────┐
│                      MapGenerated Event Fired                            │
│  State: GeneratingMap → Exploring                                       │
│  └─ SimpleWorldMapScreen receives map data                              │
│  └─ Renders terrain, fog-of-war, biome preview                          │
└──────────────────────────────────┬──────────────────────────────────────┘
                                   ▼
┌─────────────────────────────────────────────────────────────────────────┐
│                  GameSessionService.StartExploration()                   │
│  1. Creates ExplorationAI(_currentMap, _playerPosition)                 │
│  2. Subscribes to ExplorationAI events                                  │
│  3. Starts _gameTimer with ExplorationStepDelay (0.1s)                  │
└──────────────────────────────────┬──────────────────────────────────────┘
                                   ▼
┌─────────────────────────────────────────────────────────────────────────┐
│                      ExplorationAI Initialized                           │
│  1. Creates FrontierExplorationBehavior for blob-based exploration      │
│  2. Creates visibility checker (service locator or SimpleVisibility)    │
│  3. Initializes from PlayerStart or provided position                   │
│  4. UpdateVision() → marks initially visible tiles                      │
│  5. Ready to receive StepExploration() calls from timer                 │
└─────────────────────────────────────────────────────────────────────────┘
```

---

## 2. Identified Inconsistencies

### INC-001: Multi-Biome WFC Uses Only First Biome for Soft Rules
**Location:** `WfcMapGenerator.cs:216-224`
**Severity:** Medium

```csharp
// Use the first biome as default (multi-biome support is simplified for now)
BiomeDefinition? defaultBiome = null;
foreach (var b in biomeRegistry.GetAllBiomes())
{
    defaultBiome = b;
    break;
}

var (solveResult, grid) = solver.SolveWithRetry(CreateGrid, defaultBiome, seed, MaxRetries);
```

**Issue:** When generating multi-biome maps, the WFC solver only uses the first biome's tile weights for soft rule weighting. All position-based biome preferences are ignored during tile selection.

**Impact:** Tiles are weighted uniformly across the entire map regardless of which biome each cell belongs to. The biome map is only used post-generation to label cells, not to influence generation.

---

### INC-002: CardBasedGradient Randomly Samples Grid Points
**Location:** `CardBasedGradient.cs:86-115`
**Severity:** Low

The gradient methods (sphere, capsule, bezier) sample random points to populate the 16x16 sample grid, then use bilinear interpolation for actual position queries. This means:

1. Two positions with the same gradient coordinates may get different signatures
2. The random sampling uses the instance's `_rng`, which is passed in but not seeded predictably

**Impact:** Gradient is not deterministic given the same card inputs unless RNG is externally controlled.

---

### INC-003: RNG Seed Inconsistency Between GameSessionService and WFC
**Location:** `GameSessionService.cs:125` vs `SimpleMapGenerator.cs:187`
**Severity:** Low

```csharp
// GameSessionService._Ready():
_rng.Randomize();  // Uses system time

// Later in GenerateTerrainViaWfc():
_wfcGenerator.GenerateMultiBiome(..., _rng.Randi());  // Uses randomized RNG
```

**Issue:** The RNG in GameSessionService is randomized at startup, then its state is passed to WFC. The WFC then creates its own RNG with the passed seed. This double-hop makes reproducibility harder to trace.

---

## 3. Identified Gotchas

### GOT-001: 4-Direction vs 8-Direction Neighbor Inconsistency
**Locations:** Multiple files

The codebase uses two different neighbor enumeration patterns:

| Context | Directions | Rationale |
|---------|------------|-----------|
| WFC Propagation | 8-directional | 2x2 window constraint affects diagonals |
| WFC Adjacency Rules | Symmetric (A↔B) | Transition pairs are bidirectional |
| Blob Size Tracking | 4-directional | Only cardinal neighbors count for blobs |
| A* Pathfinding | 4-directional | Movement is orthogonal only |
| Exploration Frontier | 4-directional | Same as pathfinding |

**Gotcha:** Developers may assume one pattern applies everywhere. The 8-directional propagation is critical for the 2x2 window constraint but could be confused with other 4-directional patterns.

---

### GOT-002: Soft Modifiers Apply Multiplicatively
**Location:** `WfcTileSelector.cs:99-112`

```csharp
foreach (var modifier in _softModifiers)
{
    weight *= modifier.CalculateMultiplier(context);
}
```

With default settings:
- Continuity bias: 5.0x (hard-coded in selector)
- Diminishing returns: 1/(1 + size * 0.5)
- Novelty: 3.0x for new blobs
- Compactness: 0.3x penalty for snakes, 1.5x boost for fills

**Example calculation for a tile extending a size-10 blob:**
```
Base: 1.0
× Continuity: 5.0
× DiminishingReturns: 1/(1 + 10*0.5) = 0.167
× Compactness (2 neighbors): 1.0
= Final: 0.835
```

**Gotcha:** The effective weights can vary by orders of magnitude, making tuning non-obvious.

---

### GOT-003: Transition Spacing Constraint is Position-Agnostic
**Location:** `WfcPropagator.cs:228-293`

The 2x2 window constraint prevents 3+ distinct terrain types in any window, but it doesn't consider which biome the cell belongs to. This means:

- A cell in a "forest" biome area can be forced to use "desert" tiles if its 2x2 window already has 2 types including desert
- The biome assignment happens separately from WFC generation

---

### GOT-004: EnsureConnectivity Modifies Terrain After WFC
**Location:** `SimpleMapGenerator.cs:287-337`

After WFC completes, `EnsureConnectivity()` may carve corridors through the terrain to connect disconnected components. These corridor tiles:

1. Use `biome.SelectPassableTile(_rng)` - random selection, not WFC
2. May violate 2x2 window constraints established by WFC
3. Are not reflected in the `DecorationOverlays` (terrain transitions)

**Impact:** Corridor tiles may have visual glitches at terrain boundaries.

---

### GOT-005: Player/Enemy Placement is Fully Random
**Location:** `SimpleMapGenerator.cs:147-152`

```csharp
var shuffledTiles = passableTiles.OrderBy(_ => _rng.Randf()).ToList();
var playerStart = shuffledTiles[0];
var enemyPositions = shuffledTiles.Skip(1).Take(enemyCount).ToList();
```

**Issues:**
- No minimum distance between player and enemies
- Enemies could spawn in dead-end corners
- Player could spawn at map edge with limited exploration options
- No consideration of biome (could spawn in water if passable)

---

## 4. Potential Bugs

### BUG-001: Missing BiomeMap Update After Corridor Creation
**Location:** `SimpleMapGenerator.cs:364-387`
**Severity:** Medium

When corridors are created in `CreateCorridor()`, the `biomeMap` array is not updated. The corridor tiles use biome-based tile selection, but the biome assignment in the map data may not reflect this.

```csharp
// biomeMap is built before EnsureConnectivity
var biomeMap = new string[size.Y, size.X];
// ... populated from biomeProvider

// Later, corridors are carved:
private void CreateCorridor(...)
{
    var biome = _biomeProvider.GetBiomeAt(current);
    tileIds[current.Y, current.X] = biome.SelectPassableTile(_rng);
    // biomeMap NOT updated here
}
```

**Impact:** The BiomeMap in SimpleMapData may show incorrect biome assignments for corridor tiles.

---

### BUG-002: DecorationOverlays Generated Before Structure Placement
**Location:** `SimpleMapGenerator.cs:124-127`

```csharp
// Generate terrain transitions BEFORE placing structures
var decorationOverlays = GenerateTerrainTransitions(terrainGrid, size);
// ...
// Place structures (on TOP of terrain, don't affect transitions)
if (StructuresEnabled && _structurePlacer != null && _structureStamps.Count > 0)
```

**Issue:** Structures placed after transition calculation may cover transition tiles. If structures use different terrain types, the visual transitions could be incorrect.

**Impact:** Visual artifacts where structures meet terrain.

---

### BUG-003: NoveltySoftModifier and CompactnessSoftModifier Duplicate Neighbor Counting
**Location:** `NoveltySoftModifier.cs:45-58` and `CompactnessSoftModifier.cs:43-56`

Both modifiers independently count same-type neighbors using nearly identical code. This is inefficient and could lead to inconsistencies if one is modified without the other.

```csharp
// Both have identical implementations of:
private static int CountSameTypeNeighbors(SoftModifierContext context)
```

**Impact:** Performance overhead and maintenance risk.

---

### BUG-004: Exploration Defers Enemy Pursuit But May Miss Closer Enemies
**Location:** `ExplorationAI.cs:229-238`

When an enemy is spotted during active path following, the AI defers pursuit until reaching its current destination:

```csharp
if (CurrentMode == ExplorationMode.FrontierExploration && _pathToTarget.Count > 0)
{
    // We're exploring with an active path - defer enemy pursuit
    _pendingEnemyPosition = closestVisibleEnemy;
    // Don't switch mode or clear path
}
```

**Issue:** If a closer enemy appears after setting `_pendingEnemyPosition`, the original (potentially farther) enemy position is still used when switching modes.

**Impact:** AI may pursue a more distant enemy when a closer one is available.

---

## 5. Unintuitive Behaviors

### UNI-001: Map Size Depends Only on First Card Signature
**Location:** `GameSessionService.cs:367-378`

```csharp
private Vector2I CalculateMapSize(CardSignature signature)
{
    var complexity = 0f;
    for (var i = 0; i < 8; i++) complexity += Mathf.Abs(signature[i]);
    complexity /= 8f;

    var baseSize = 50;
    var sizeVariation = Mathf.RoundToInt(complexity * 25);
    return new Vector2I(size, size);
}
```

**Behavior:** Only `_mapSeeds[0]` affects map size, even when multiple cards are slotted. Additional cards only affect the gradient distribution, not dimensions.

---

### UNI-002: Biome Selection Uses Euclidean Distance in 8D Space
**Location:** `BiomeRegistry.cs:22-40` and `CardSignature.cs:121-131`

```csharp
public float DistanceTo(CardSignature other)
{
    var sum = 0f;
    for (var i = 0; i < 8; i++)
    {
        var diff = _elements[i] - other._elements[i];
        sum += diff * diff;
    }
    return Mathf.Sqrt(sum);
}
```

**Behavior:** Biome matching uses raw Euclidean distance across all 8 dimensions equally. No weighting by dimension importance. Maximum possible distance is √(8 * 4) ≈ 5.66 (corner to corner of 8D hypercube from -1 to 1).

---

### UNI-003: WFC Retry Creates Fresh RNG Each Attempt
**Location:** `WfcSolver.cs:166-173`

```csharp
for (var attempt = 0; attempt <= maxRetries; attempt++)
{
    var grid = createGrid();
    var rng = new RandomNumberGenerator();
    rng.Seed = baseSeed + (ulong)attempt;  // Deterministic increment
    // ...
}
```

**Behavior:** Each retry uses `baseSeed + attempt` as seed. While deterministic, this means retry 3 will always produce the same result regardless of what caused retries 0-2 to fail.

---

### UNI-004: Trivially Visible Tiles Skip Physical Movement
**Location:** `FrontierExplorationBehavior.cs:108-151`

```csharp
// Mark tiles as visited if they are trivially visible (no need to walk there)
private void MarkTriviallyVisibleTiles()
{
    do {
        // ...
        if (IsTriviallyVisible(tile))
        {
            _visitedTiles.Add(tile);
            changed = true;
        }
    } while (changed);
}
```

**Behavior:** The exploration AI doesn't physically move to tiles that are "trivially visible" (fully visible with all neighbors seen). This optimizes exploration but means:
- The player sprite may appear to skip tiles
- Events like `PlayerMoved` won't fire for skipped tiles
- Achievement systems tracking "tiles walked" would be inaccurate

---

### UNI-005: Combat Uses Only First Map Seed
**Location:** `GameSessionService.cs:249`

```csharp
_combatSystem = new SimpleCombatSystem(_abilityCards, _mapSeeds[0], _rng);
```

**Behavior:** Combat calculations only use the first map seed signature, ignoring additional seeds that influenced map generation.

---

### UNI-006: Loot Generation Has Minimal Card Influence
**Location:** `GameSessionService.cs:352-365`

```csharp
private CardSignature GenerateLootSignature()
{
    var lootSignature = new CardSignature();
    var baseSeed = _mapSeeds[0];

    for (var i = 0; i < 8; i++)
    {
        var variation = _rng.Randfn(baseSeed[i], 0.1f);  // Normal distribution, σ=0.1
        lootSignature[i] = Mathf.Clamp(variation, -1f, 1f);
    }
    return lootSignature;
}
```

**Behavior:** Loot is slight variations of `_mapSeeds[0]` only. Neither additional map seeds nor ability cards influence loot generation, despite `_abilityCards` being available.

---

## 6. State Machine Analysis

### Session States and Valid Transitions

```
┌─────────────────┐
│ WaitingForCards │ ◄──────────────────────────────────────┐
└────────┬────────┘                                        │
         │ StartSession(cards)                             │
         ▼                                                 │
┌─────────────────┐                                        │
│ GeneratingMap   │                                        │
└────────┬────────┘                                        │
         │ GenerateMap()                                   │
         ▼                                                 │
┌─────────────────┐ ◄────────┐                             │
│   Exploring     │          │                             │
└────────┬────────┘          │                             │
         │ EnemyEncountered  │ PlayerWon && enemies remain │
         ▼                   │                             │
┌─────────────────┐          │                             │
│   InCombat      │ ─────────┘                             │
└────────┬────────┘                                        │
         │ CombatEnded                                     │
         ▼                                                 │
┌─────────────────┐          ┌─────────────────┐          │
│ GeneratingLoot  │ ─────────│ SessionComplete │ ─────────┘
└─────────────────┘          └─────────────────┘
```

### Transition Guards

| Transition | Guard Condition |
|------------|-----------------|
| WaitingForCards → GeneratingMap | mapSeeds != null && abilityCards != null |
| GeneratingMap → Exploring | MapGenerated event fired |
| Exploring → InCombat | EnemyEncountered event |
| Exploring → GeneratingLoot | HasFinishedExploration && no enemies found |
| InCombat → Exploring | PlayerWon && EnemyPositions.Count > 0 |
| InCombat → GeneratingLoot | PlayerWon && EnemyPositions.Count == 0 |
| InCombat → SessionComplete | !PlayerWon (player lost) |
| GeneratingLoot → SessionComplete | Always |
| SessionComplete → WaitingForCards | ResetForNextSession() |

---

## 7. RNG Seeding Analysis

### RNG Flow
```
GameSessionService._Ready()
└─ _rng.Randomize()  // System time seed

GameSessionService.GenerateMap()
├─ CalculateMapSize(_mapSeeds[0])  // No RNG
├─ new CardBasedGradient(_mapSeeds, _rng)  // Shares RNG instance
├─ new BiomeMapGenerator(...)  // Uses gradient's RNG indirectly
└─ SimpleMapGenerator.GenerateMap()
   ├─ SelectPerGenerationVariants()  // Uses _rng
   ├─ GenerateTerrainViaWfc()
   │  └─ _wfcGenerator.GenerateMultiBiome(..., _rng.Randi())
   │     └─ WfcSolver.SolveWithRetry()
   │        └─ new RandomNumberGenerator { Seed = baseSeed + attempt }  // Fresh RNG
   ├─ PlaceStructures()  // Uses _rng
   ├─ EnsureConnectivity()  // Uses _rng (via biome.SelectPassableTile)
   └─ Player/enemy placement  // Uses _rng
```

### Reproducibility Notes
1. GameSessionService uses system-time seeded RNG - not reproducible
2. WFC creates its own seeded RNG from `_rng.Randi()` - reproducible given same parent state
3. Multiple WFC retries increment seed deterministically
4. CardBasedGradient shares RNG instance - order-dependent effects

---

## 8. Performance Considerations

### Identified Hotspots

1. **BiomeRegistry.FindClosestBySignature()** - O(n) linear search for each tile
2. **BlobSizeTracker.GetPotentialBlobSize()** - O(neighbors * union-find) per tile selection
3. **WfcPropagator.ComputeValidTiles()** - O(neighbors * possible_tiles) per propagation step
4. **FrontierExplorationBehavior.FindUnvisitedBlobs()** - BFS + flood fill for each target search

### Typical Operation Counts (60x60 map)
- Grid cells: 3,600
- WFC iterations: ~3,600 (one per cell)
- Propagation operations: Variable, depends on constraint tightness
- Blob queries: O(passable_tiles) per exploration step

---

## 9. Recommendations

### High Priority
1. **BUG-001:** Update biomeMap when creating corridors
2. **INC-001:** Implement position-aware biome weighting in WFC

### Medium Priority
3. **GOT-004:** Consider regenerating terrain transitions after structure/corridor placement
4. **BUG-004:** Track multiple visible enemies and update pending target dynamically
5. **GOT-005:** Add minimum spawn distance constraints

### Low Priority
6. **BUG-003:** Extract shared neighbor counting to a utility method
7. **UNI-006:** Incorporate ability cards into loot generation
8. **INC-003:** Consider exposing seed parameter for reproducible sessions

---

## Appendix A: File Reference

| File | Primary Responsibility |
|------|----------------------|
| `DeckBuilderController.cs` | Button handler, card consumption |
| `SimpleWorldMapScreen.cs` | UI controller, event bridging |
| `GameSessionService.cs` | State machine, session orchestration |
| `SimpleMapGenerator.cs` | Generation orchestration, connectivity |
| `WfcMapGenerator.cs` | WFC configuration, modifier setup |
| `WfcSolver.cs` | WFC algorithm loop |
| `WfcPropagator.cs` | Constraint propagation |
| `WfcTileSelector.cs` | Weighted tile selection |
| `WfcAdjacencyRules.cs` | Transition-based adjacency |
| `BiomeMapGenerator.cs` | Position → biome mapping |
| `BiomeRegistry.cs` | Signature → biome lookup |
| `CardBasedGradient.cs` | Card → spatial signature gradient |
| `ExplorationAI.cs` | Exploration logic, pathfinding |
| `FrontierExplorationBehavior.cs` | Blob-based frontier exploration |

---

## Appendix B: Configuration Reference

| Parameter | Location | Default | Effect |
|-----------|----------|---------|--------|
| MaxWfcRetries | SimpleMapGenerator | 5 | Retry attempts on contradiction |
| MaxIterations | WfcSolver | 10,000 | Prevent infinite loops |
| NonBiomeTilePenalty | WfcTileSelector | 0.1 | Weight for non-biome tiles |
| ContinuityBiasMultiplier | WfcTileSelector | 5.0 | Boost for matching neighbors |
| DiminishingReturnsDecay | WfcMapGenerator | 0.5 | Blob size penalty factor |
| NoveltyBoost | WfcMapGenerator | 3.0 | New blob starter boost |
| SnakePenalty | CompactnessSoftModifier | 0.3 | Thin extension penalty |
| CompactBoost | CompactnessSoftModifier | 1.5 | Fill-in boost |
| SignificantBlobThreshold | FrontierExplorationBehavior | 5 | Min blob for prioritization |
| VisionRange | ExplorationAI | 5 | Fog of war radius |
| ExplorationStepDelay | GameSessionService | 0.1s | Timer between moves |

---

*End of Audit Report*
