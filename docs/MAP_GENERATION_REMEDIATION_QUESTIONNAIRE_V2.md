# Map Generation Pipeline - Remediation Questionnaire V2

**Purpose:** This questionnaire reframes the map generation issues around a WFC-native design approach, based on design vision clarified during V1 review.

**Key Paradigm Shift:** V1 focused on fixing symptoms (corridor carving, disconnected regions). V2 focuses on building correct systems from the ground up:
- Connectivity as a WFC constraint, not post-processing
- Biome gradients with soft probability modifiers
- Tile selection by "clearest winner," not "fewest options"
- Deterministic generation seeded by input cards

**References:**
- [Wave Function Collapse Explained - Robert Heaton](https://robertheaton.com/2018/12/17/wavefunction-collapse-algorithm/)
- [Wave Function Collapse Explained - Boris the Brave](https://www.boristhebrave.com/2020/04/13/wave-function-collapse-explained/)

---

## Table of Contents

1. [Design Principles](#design-principles)
2. [Core Architecture](#core-architecture)
   - [Biome Gradient System](#1-biome-gradient-system)
   - [Hard/Soft Rule Dichotomy](#2-hardsoft-rule-dichotomy)
   - [Clearest Winner Selection](#3-clearest-winner-selection)
3. [WFC-Native Connectivity](#wfc-native-connectivity)
4. [Structure Generation](#structure-generation)
5. [Deterministic RNG](#deterministic-rng)
6. [Implementation Roadmap](#implementation-roadmap)
7. [Appendix: Future Considerations](#appendix-future-considerations)

---

## Design Principles

These principles emerged from V1 review and should guide all implementation decisions:

| Principle | Description |
|-----------|-------------|
| **WFC-Native** | All constraints should be expressible within WFC, not as post-processing |
| **Probability-Based** | Soft rules modify probability; only hard rules completely ban tiles |
| **Terrain First** | Terrain generation is complete before structures or entities |
| **Card-Deterministic** | Same input cards = same output map (given same card order) |
| **Biome Gradients** | Each position has continuous biome affinities, not discrete assignment |

---

## Core Architecture

### 1. Biome Gradient System

**Current State (Problems):**
- `WfcMapGenerator.GenerateMultiBiome()` uses only first biome for soft rules
- `BiomeMapGenerator` assigns discrete biome per position (closest match)
- Position-based biome preferences are ignored during tile selection

**Target State:**
Cards define 8D shapes that create a gradient map:
- **1 card:** Hypersphere centered on card's signature
- **2 cards:** Capsule with ends at both signatures
- **3+ cards:** Toroidal bezier curve through all signatures

Each position has a **biome strength** from -1 to +1 for each biome. Tiles are boosted/deboosted based on their biome affinity at that position.

```
Position (x,y) → Gradient → Signature → BiomeStrengths{forest: 0.8, desert: -0.3, ...}
                                        ↓
Tile "grass_01" has affinity {forest: 1.0, desert: 0.1}
                                        ↓
Effective weight = base_weight × (1 + forest_strength × forest_affinity + ...)
```

#### Solution Options

##### Option A: Continuous Biome Strength in SoftModifier

Create a new `BiomeAffinitySoftModifier` that:
1. Queries gradient for position signature
2. Calculates biome strength for each biome at position
3. Multiplies tile weight by affinity match

```csharp
public class BiomeAffinitySoftModifier : ISoftModifier
{
    public float CalculateMultiplier(SoftModifierContext context)
    {
        var signature = _gradient.GetSignatureAt(context.Position, _mapSize);
        var affinityScore = CalculateAffinityScore(context.TileId, signature);
        return Mathf.Max(0.01f, 1.0f + affinityScore); // Clamp to prevent zero
    }
}
```

| Pros | Cons |
|------|------|
| Uses existing ISoftModifier pattern | Per-tile gradient lookup (performance) |
| Gradual biome transitions | Needs tile→biome affinity mapping |
| Natural blending at boundaries | Complex tuning of affinity scores |

**Code to Remove:**
- Single-biome selection in `WfcMapGenerator.cs:216-224`
- Consider removing `NonBiomeTilePenalty` as redundant

- [ ] **Select Option A**

---

##### Option B: Pre-computed Biome Weight Grid

Before WFC, compute a `float[y, x, biomeCount]` grid of biome strengths. During tile selection, look up pre-computed values.

| Pros | Cons |
|------|------|
| O(1) lookup during WFC | Memory: O(width × height × biomes) |
| Can visualize/debug grid | Requires upfront computation |
| Decouples gradient from WFC | Grid resolution vs map resolution |

**Code to Remove:**
- Same as Option A

- [X] **Select Option B**

---

##### Option C: Biome Regions with Soft Boundaries

Define hard biome regions but apply soft weight modifiers at boundaries (e.g., 3-tile transition zone).

| Pros | Cons |
|------|------|
| Simpler than full gradient | Less organic transitions |
| Clear biome ownership | Doesn't match 8D shape vision |
| Easier to debug | Loses card signature nuance |

- [ ] **Select Option C (Compromise)**

---

#### Clarifying Questions

**Q1: Should biome strength affect ALL tiles or only biome-associated tiles?**

- [ ] All tiles get multiplied (unknown affinity = 1.0)
- [X] Only tiles with explicit affinity declarations
- [ ] Different behavior for passable vs impassable tiles

**Q2: How should tile-biome affinity be defined?**

- [X] In TileDefinition as `Dictionary<string, float>` affinities
- [ ] In BiomeDefinition as `TilePool` with weights (current approach, extend it)
- [ ] Separate affinity configuration file
- [ ] Derive from tile visual properties automatically

**Notes:**
```
we should already have a list of biomes that a tile belongs to, 
we do not need floating point values on the tile type, perhaps just a positive/neutral/negative selection per biome, defaulting to neutral
```

---

### 2. Hard/Soft Rule Dichotomy

**Current State (Problems):**
- `WfcAdjacencyRules` is purely hard (can/cannot be adjacent)
- `WfcPropagator` 2x2 constraint is hard (eliminates tiles)
- Soft modifiers multiply but no clear distinction in codebase

**Target State:**
Clear separation between rules that **ban** tiles vs rules that **adjust probability**:

| Rule Type | Effect | Example |
|-----------|--------|---------|
| **Hard Rule** | Sets probability to 0 (eliminated) | "Water cannot be adjacent to lava" |
| **Soft Rule** | Multiplies probability | "Desert tiles 0.1x likely in forest biome" |

```csharp
// Hard: IHardConstraint.IsValid() → false = tile eliminated from possibilities
// Soft: ISoftModifier.CalculateMultiplier() → 0.1 = 10% as likely, but not banned
```

#### Solution Options

##### Option A: Formalize Existing Pattern

The codebase already has `IHardConstraint` and `ISoftModifier`. Formalize this:
1. Document the distinction clearly
2. Audit existing rules for correct classification
3. Ensure soft modifiers never return 0

**Audit of Current Rules:**

| Rule | Current | Should Be |
|------|---------|-----------|
| Adjacency rules | Hard | Hard (correct) |
| 2x2 window constraint | Hard | Hard (correct) |
| DiminishingReturns | Soft | Soft (correct) |
| NoveltySoftModifier | Soft | Soft (correct) |
| CompactnessSoftModifier | Soft | Soft (correct) |
| NonBiomeTilePenalty | Soft (0.1x) | Soft (correct) |
| ContinuityBiasMultiplier | Soft (5.0x) | Soft (correct) |

| Pros | Cons |
|------|------|
| Minimal code changes | Just documentation/convention |
| Uses existing architecture | Doesn't add new capability |

**Code to Remove:**
- None, but add minimum floor to soft modifiers to prevent accidental zeros

- [ ] **Select Option A**

---

##### Option B: Unified Constraint System with Probability Output

Replace separate hard/soft interfaces with single interface returning probability modifier:

```csharp
public interface IWfcConstraint
{
    float GetProbabilityModifier(ConstraintContext context);
    // Returns: 0.0 = hard ban, 0.0-1.0 = soft penalty, 1.0 = neutral, >1.0 = boost
}
```

| Pros | Cons |
|------|------|
| Single interface to implement | Larger refactor |
| Explicit probability semantics | Changes existing modifier pattern |
| Can represent continuum | Need to update all existing constraints |

**Code to Remove:**
- `IHardConstraint` interface
- `ISoftModifier` interface
- Replace with unified `IWfcConstraint`

- [X] **Select Option B**

---

##### Option C: Soft Rules Only (No Hard Bans Except Adjacency)

Make almost all rules soft, with only physical adjacency as hard. Even biome mismatches just get 0.01x multiplier.

| Pros | Cons |
|------|------|
| Maximum flexibility | Some combinations should be impossible |
| Fewer contradictions | Very rare tiles might appear inappropriately |
| Simpler mental model | Less control over strict requirements |

- [ ] **Select Option C**

---

#### Clarifying Questions

**Q1: Should any current soft rules become hard rules?**

- [X] No, current classification is correct
- [ ] Some biome mismatches should be hard bans
- [ ] Need to evaluate case by case

**Q2: What should the minimum soft multiplier floor be?**

- [ ] 0.01 (1% chance)
- [ ] 0.001 (0.1% chance)
- [X] 0 allowed (effectively hard ban)
- [ ] Configurable per rule

**Notes:**
```
[Add implementation notes here]


```

---

### 3. Clearest Winner Selection

**Current State (Problems):**
- `WfcCellState.GetEntropy()` returns `_possibleTiles.Count` (option count only)
- Selection picks cell with **fewest options**, not **clearest winner**
- Weighted probabilities don't affect which cell is selected, only which tile

**Target State:**
Select the cell where one tile has the **highest relative probability** compared to alternatives. This is "clearest winner" - where soft rules have most strongly converged on a single outcome.

```
Cell A: tiles [grass: 0.8, dirt: 0.1, rock: 0.1] → winner clarity = 0.8/1.0 = 80%
Cell B: tiles [grass: 0.33, dirt: 0.33, rock: 0.33] → winner clarity = 33%

Standard WFC: Both have 3 options, pick randomly
Clearest Winner: Pick Cell A (more confident choice)
```

#### Solution Options

##### Option A: Weighted Shannon Entropy

Replace count-based entropy with probability-weighted Shannon entropy:

```csharp
public float GetWeightedEntropy(Dictionary<string, float> weights)
{
    var totalWeight = weights.Values.Sum();
    if (totalWeight <= 0) return float.MaxValue;

    var entropy = 0f;
    foreach (var w in weights.Values)
    {
        if (w <= 0) continue;
        var p = w / totalWeight;
        entropy -= p * Mathf.Log(p);
    }
    return entropy;
}
```

Lower entropy = clearer winner. Select cell with **lowest weighted entropy**.

| Pros | Cons |
|------|------|
| Mathematically principled | Requires weight calculation for entropy |
| Standard information theory | More computation per cell |
| Naturally handles probability | Need to cache/update weights |

**Code to Remove:**
- `WfcCellState.GetEntropy()` count-based implementation
- `WfcGrid.GetLowestEntropyCellWithTieBreak()` needs weight-aware version

- [A] **Select Option A**

---

##### Option B: Dominance Ratio

Select cell where top tile has highest ratio vs second tile:

```csharp
public float GetDominanceRatio(Dictionary<string, float> weights)
{
    var sorted = weights.Values.OrderByDescending(w => w).ToList();
    if (sorted.Count < 2) return float.MaxValue; // Already collapsed
    return sorted[0] / sorted[1]; // Higher = clearer winner
}
```

| Pros | Cons |
|------|------|
| Simple intuition | Only considers top 2 tiles |
| Fast calculation | Ignores distribution shape |
| Easy to debug | May miss subtle patterns |

**Code to Remove:**
- Same as Option A

- [ ] **Select Option B**

---

##### Option C: Hybrid - Low Count First, Then Weighted

Keep current approach but add weighted selection as tiebreaker:

```csharp
// Primary: fewest options (standard WFC)
// Tiebreaker: lowest weighted entropy among equal-count cells
```

| Pros | Cons |
|------|------|
| Backward compatible | Doesn't fully embrace probability approach |
| Lower risk | May not significantly change behavior |
| Incremental improvement | Hybrid complexity |

- [ ] **Select Option C (Conservative)**

---

##### Option D: Research Needed

The "clearest winner" concept may need prototyping to validate behavior. Create experimental branch first.

- [ ] **Select Option D (Defer with prototype)**

---

#### Clarifying Questions

**Q1: How important is determinism in cell selection order?**

- [X] Critical - same weights must select same cell
- [ ] Important for debugging, not gameplay
- [ ] Not important, randomness is fine

**Q2: Should we add weighted entropy visualization for debugging?**

- [ ] Yes, add overlay showing entropy values
- [X] No, standard debugging is sufficient

**Notes:**
```
[Add implementation notes here]


```

---

## WFC-Native Connectivity

**Current State (Problems):**
- `SimpleMapGenerator.EnsureConnectivity()` carves corridors **after** WFC completes
- Corridors violate 2x2 window constraints established by WFC
- Corridors don't update `biomeMap` (BUG-001)
- Corridors don't get decoration overlays (GOT-004)

**Target State:**
Connectivity is a **WFC constraint** - the algorithm prevents disconnected regions during generation, not after.

### Why Corridors Are Wrong

The current approach:
1. WFC generates terrain respecting all constraints
2. Post-processing checks if passable tiles are connected
3. If not, carves corridors that **violate** constraints

This is backwards. The constraint should be: "Never collapse a tile in a way that disconnects the passable region."

### Solution Options

#### Option A: Articulation Point Prevention (Hard Constraint)

During WFC, before collapsing a cell, check if the collapse would create an articulation point (a tile whose removal disconnects the graph).

```csharp
public class ConnectivityHardConstraint : IHardConstraint
{
    public bool IsValid(HardConstraintContext context)
    {
        // If this tile is passable, check if making it impassable
        // would disconnect any existing passable regions
        if (!context.IsPassable(context.TileId))
        {
            return !WouldDisconnectRegions(context.Position, context.Grid);
        }
        return true;
    }
}
```

| Pros | Cons |
|------|------|
| Guarantees connectivity | Complex graph algorithm |
| No post-processing needed | Performance: O(V+E) per collapse check |
| Respects all WFC constraints | May over-constrain early cells |

**Code to Remove:**
- `SimpleMapGenerator.EnsureConnectivity()`
- `SimpleMapGenerator.CreateCorridor()`
- `SimpleMapGenerator.FindConnectedComponents()`
- `SimpleMapGenerator.FindClosestPair()`
- Remove `biomeMap` parameter threading for corridors

- [X] **Select Option A**

---

#### Option B: Flood-Fill Validation with Backtracking

After each collapse, verify connectivity. If broken, backtrack and try different tile.

```csharp
// In WfcSolver.Solve()
var selectedTile = _selector.SelectTile(...);
targetCell.Collapse(selectedTile);

if (!ValidateConnectivity(grid))
{
    // Backtrack: restore cell, remove tile from options, retry
    targetCell.Restore();
    targetCell.RemovePossibility(selectedTile);
    continue; // Try again
}
```

| Pros | Cons |
|------|------|
| Uses existing WFC backtracking | Expensive full flood-fill per collapse |
| Clear failure → retry pattern | May cause many backtracks |
| Simpler than articulation points | Could thrash on difficult maps |

**Code to Remove:**
- Same as Option A

- [ ] **Select Option B**

---

#### Option C: Soft Connectivity Bias (Encourage, Don't Force)

Add a soft modifier that **boosts** tiles extending connected regions and **penalizes** tiles that reduce connectivity, but don't hard-ban anything.

```csharp
public class ConnectivitySoftModifier : ISoftModifier
{
    public float CalculateMultiplier(SoftModifierContext context)
    {
        var connectivity = EvaluateConnectivityImpact(context);
        // connectivity: -1 (disconnects) to +1 (connects)
        return 1.0f + connectivity * ConnectivityWeight; // e.g., weight = 2.0
    }
}
```

| Pros | Cons |
|------|------|
| No hard constraint complexity | Doesn't guarantee connectivity |
| Soft guidance, not rigid | May still need corridor fallback |
| Better performance | Doesn't fully solve problem |

**Code to Remove:**
- Keep `EnsureConnectivity()` as fallback but should rarely trigger

- [ ] **Select Option C (Soft approach + fallback)**

---

#### Option D: Generation Order - Center Out

Generate from center outward, always expanding the connected region. Early cells establish a connected core; later cells extend it.

```csharp
// Instead of lowest entropy, select:
// 1. Uncollapsed cell adjacent to collapsed passable cells
// 2. Prefer extending the main connected component
```

| Pros | Cons |
|------|------|
| Naturally builds connected region | Changes fundamental WFC order |
| No explicit connectivity check | May not work well with entropy selection |
| Simple concept | Biases toward certain map shapes |

**Code to Remove:**
- Same as Option A (if guaranteed to work)

- [ ] **Select Option D**

---

#### Option E: Accept Corridors (Keep Current + Fix Bugs)

Fix the bugs in corridor system (BUG-001, GOT-004) but keep the post-processing approach.

| Pros | Cons |
|------|------|
| Minimal change | Doesn't match WFC-native philosophy |
| Known working system | Corridors still violate constraints |
| Quick fix | Technical debt remains |

**Code to Change (not remove):**
- Add `biomeMap` update in `CreateCorridor()`
- Regenerate transitions after corridor creation

- [ ] **Select Option E (Pragmatic)**

---

### Clarifying Questions

**Q1: How often do current maps require corridor carving?**

- [ ] Rarely (<10% of maps)
- [ ] Sometimes (10-30%)
- [ ] Often (>30%)
- [X] Need metrics (unknown)

**Q2: Is perfect connectivity a hard requirement or preference?**

- [X] Hard requirement - disconnected regions are bugs
- [ ] Strong preference - rare disconnection acceptable
- [ ] Soft preference - some isolated areas are interesting

**Q3: Should water/impassable areas be allowed to disconnect land?**

- [ ] Yes, islands are valid
- [X] No, all land must be connected
- [ ] Only if player starts on largest region

**Notes:**
```
it *currently* is a hard requirement, but there are several ways to make it a soft requirement:
1) the true hard requirement is that all enemies must be on the same walkable area as the player 
2) we could use structures to connect two otherwise disconnected areas (eg: a bridge connecting an island, or a tunnel through a mountain)
```

---

## Structure Generation

**Current State (Problems):**
- Structures placed **before** transitions calculated
- Structures can block paths, requiring corridor carving
- No reachability check before placement

**Target State:**
Terrain is **leading**. Structures generate after terrain is complete, with awareness of:
1. Biome context (structure must fit biome)
2. Reachability (structure must not block paths)
3. Terrain compatibility (structure must have valid transitions)

### Solution Options

#### Option A: Post-Terrain Structure Placement with Reachability Validation

```csharp
// After WFC terrain is complete:
foreach (var stamp in _structureStamps)
{
    var placement = FindValidPlacement(stamp, terrainGrid);
    if (placement != null && !WouldBlockPaths(placement, terrainGrid))
    {
        PlaceStructure(stamp, placement);
    }
}
```

| Pros | Cons |
|------|------|
| Clear generation order | Some structures may not fit |
| Respects terrain | Reachability check is O(V+E) |
| No corridor conflicts | May reduce structure variety |

**Code to Remove:**
- Move `PlaceStructures()` call to after `EnsureConnectivity()` (or after connectivity constraint if using WFC-native)

- [x] **Select Option A**

---

#### Option B: Structures as WFC Constraints

Define structures as collections of tiles with WFC constraints. Structure "seeds" collapse early and propagate their requirements.

| Pros | Cons |
|------|------|
| Fully integrated with WFC | Complex constraint definition |
| Natural terrain integration | May cause contradictions |
| No separate placement phase | Harder to control structure locations |

**Code to Remove:**
- `PlaceStructures()` entirely
- `_structureStamps` approach
- Replace with WFC-integrated structure system

- [ ] **Select Option B**

---

#### Option C: Defer Structure System (Focus on Terrain)

Per user note: "for now, though, we're purely trying to get terrain to work in a sensible way."

Keep current structure system but document that it will be redesigned after terrain is stable.

| Pros | Cons |
|------|------|
| Focus on terrain priority | Structures remain problematic |
| No distraction from core work | Technical debt |
| Faster to implement terrain | — |

**Code to Remove:**
- None for now

- [ ] **Select Option C (Defer)**

---

### Clarifying Questions

**Q1: Are structures essential for current gameplay?**

- [x] Yes, structures are core to gameplay
- [ ] No, terrain-only maps are playable
- [ ] Structures are nice-to-have

**Notes:**
```
we have two broad categories of structures:
1) stamps, these are NxM fixed-structure objects that are just multiple tiles in a fixed pattern, like a big tree, or a large boulder.
2) true structures, these are objects that have a more "logical" structure than terrain, but are still built procedurally, like a building or a dungeon. or a bridge 
```

---

## Deterministic RNG

**Current State (Problems):**
- `GameSessionService._rng.Randomize()` uses system time
- Cannot reproduce maps from card inputs
- Debugging generation issues is difficult

**Target State:**
Same input cards → same output map (given same card order).

### Solution Options

#### Option A: Seed from Card Signature Hash

```csharp
public void StartSession(List<CardSignature> mapSeeds, ...)
{
    var seed = ComputeSeedFromCards(mapSeeds);
    _rng.Seed = seed;
}

private ulong ComputeSeedFromCards(List<CardSignature> cards)
{
    // Combine all card signatures into deterministic seed
    var hash = 17UL;
    foreach (var card in cards)
    {
        for (int i = 0; i < 8; i++)
        {
            hash = hash * 31 + BitConverter.ToUInt32(BitConverter.GetBytes(card[i]), 0);
        }
    }
    return hash;
}
```

| Pros | Cons |
|------|------|
| Reproducible generation | Card order matters |
| Debug with card values | Floating point hash may vary |
| Simple implementation | Need to propagate seed everywhere |

**Code to Remove:**
- `_rng.Randomize()` in `GameSessionService._Ready()`

- [ ] **Select Option A**

---

#### Option B: Explicit Seed Parameter (Debug Mode)

Add optional seed parameter for testing while keeping random default:

```csharp
public void StartSession(..., ulong? debugSeed = null)
{
    _rng.Seed = debugSeed ?? (ulong)DateTime.Now.Ticks;
    _usedSeed = _rng.Seed; // Save for debugging/replay
}
```

| Pros | Cons |
|------|------|
| Backward compatible | Default still non-deterministic |
| Good for debugging | Doesn't achieve card-determinism goal |
| Saves seed for replay | Two modes to maintain |

**Code to Remove:**
- None, additive change

- [ ] **Select Option B (Partial)**

---

#### Option C: Hybrid - Card Seed + Saved Seed

Compute seed from cards, but also save it for replay/debugging:

```csharp
public void StartSession(List<CardSignature> mapSeeds, ...)
{
    _usedSeed = ComputeSeedFromCards(mapSeeds);
    _rng.Seed = _usedSeed;
    GD.Print($"Map seed: {_usedSeed}"); // For debugging
}
```

| Pros | Cons |
|------|------|
| Achieves determinism goal | More logging/tracking |
| Also enables replay | — |
| Best of both worlds | — |

**Code to Remove:**
- `_rng.Randomize()` in `GameSessionService._Ready()`

- [X] **Select Option C (Recommended)**

---

### Clarifying Questions

**Q1: Should saved games include the computed seed?**

- [ ] Yes, for perfect replay
- [x] No, just save map state
- [ ] Optional for debug builds

**Notes:**
```
it would be helpful to log the seed for debugging so we can do more targeted testing, 
but it does not make sense to save it anywhere 
```

---

## Implementation Roadmap

### Phase 1: Foundation (Determinism + Cleanup)
**Goal:** Reproducible generation and removal of problematic code

| Task | Files Affected | Removes |
|------|----------------|---------|
| Implement card-based RNG seeding | `GameSessionService.cs` | `_rng.Randomize()` |
| Deterministic gradient sampling | `CardBasedGradient.cs` | RNG-dependent sampling |
| Remove neighbor counting duplication | Soft modifiers | Duplicate `CountSameTypeNeighbors` |

### Phase 2: Core WFC Improvements (Biome + Selection)
**Goal:** Proper multi-biome generation with clearest winner selection

| Task | Files Affected | Removes |
|------|----------------|---------|
| BiomeAffinitySoftModifier | New file + `WfcTileSelector.cs` | Single-biome limitation |
| Weighted entropy calculation | `WfcCellState.cs`, `WfcGrid.cs` | Count-based entropy |
| Pre-computed biome strength grid (if chosen) | New file | Per-tile gradient lookup |

### Phase 3: Connectivity Constraint
**Goal:** Eliminate corridor post-processing

| Task | Files Affected | Removes |
|------|----------------|---------|
| Implement connectivity constraint | New `ConnectivityConstraint.cs` | — |
| Integrate with WfcSolver | `WfcSolver.cs` | — |
| Remove corridor system | `SimpleMapGenerator.cs` | `EnsureConnectivity()`, `CreateCorridor()`, `FindConnectedComponents()`, `FindClosestPair()` |

### Phase 4: Structure System Redesign
**Goal:** Structures that respect terrain

| Task | Files Affected | Removes |
|------|----------------|---------|
| Reorder generation phases | `SimpleMapGenerator.cs` | — |
| Add reachability validation | `SimpleMapGenerator.cs` | — |
| Update transitions after structures | `SimpleMapGenerator.cs` | Early transition generation |

---

## Summary Decision Table

| Issue | Recommended | Code to Remove |
|-------|-------------|----------------|
| **Biome Gradient** | Option A (SoftModifier) | Single-biome selection (WfcMapGenerator:216-224) |
| **Hard/Soft Rules** | Option A (Formalize existing) | None (documentation) |
| **Clearest Winner** | Option A (Weighted entropy) | Count-based entropy (WfcCellState) |
| **Connectivity** | Option A or B (WFC constraint) | `EnsureConnectivity()`, `CreateCorridor()`, 4 helper methods |
| **Structures** | Option C (Defer) | None for now |
| **RNG** | Option C (Card seed + save) | `_rng.Randomize()` |

---

## Appendix: Future Considerations

These items were explicitly deprioritized during V1 review. Address after terrain generation is stable.

### Enemy/Entity Placement
- Minimum distance constraints
- Biome-aware spawning
- Difficulty scaling with cards

### AI Pursuit Logic
- Multi-enemy tracking
- Closest enemy updates

### Loot Generation
- Blend all map seeds
- Combat performance influence
- Card signature evolution loop

### Map Size Calculation
- Consider all cards, not just first
- Investigate "1 pixel gap" issue mentioned in UNI-001

### Combat Seed
- Blend all map seeds for combat calculations

---

*V2 Generated from [Map Generation Audit Report](./MAP_GENERATION_AUDIT.md) and V1 review notes*
