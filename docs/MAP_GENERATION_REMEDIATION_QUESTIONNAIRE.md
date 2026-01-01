# Map Generation Pipeline - Remediation Questionnaire

**Purpose:** This questionnaire helps prioritize and plan fixes for issues identified in the [Map Generation Audit](./MAP_GENERATION_AUDIT.md).

**Instructions:**
- Check `[x]` for options you want to pursue
- Add comments in the "Notes" sections
- Issues are ordered by severity (High → Medium → Low)

---

## Table of Contents

1. [High Priority Issues](#high-priority-issues)
   - [BUG-001: Missing BiomeMap Update After Corridors](#bug-001-missing-biomemap-update-after-corridor-creation)
   - [INC-001: Multi-Biome WFC Uses Only First Biome](#inc-001-multi-biome-wfc-uses-only-first-biome-for-soft-rules)
2. [Medium Priority Issues](#medium-priority-issues)
   - [GOT-004: Terrain Transitions Before Structures/Corridors](#got-004-terrain-transitions-generated-before-structurescorridors)
   - [BUG-004: Exploration Defers Wrong Enemy](#bug-004-exploration-defers-enemy-pursuit-but-may-miss-closer-enemies)
   - [GOT-005: Random Player/Enemy Placement](#got-005-playerenemy-placement-is-fully-random)
   - [BUG-002: DecorationOverlays Before Structure Placement](#bug-002-decorationoverlays-generated-before-structure-placement)
3. [Low Priority Issues](#low-priority-issues)
   - [BUG-003: Duplicate Neighbor Counting](#bug-003-noveltysoftmodifier-and-compactnesssoftmodifier-duplicate-neighbor-counting)
   - [UNI-006: Loot Generation Has Minimal Card Influence](#uni-006-loot-generation-has-minimal-card-influence)
   - [INC-003: RNG Seed Inconsistency](#inc-003-rng-seed-inconsistency-between-gamesessionservice-and-wfc)
   - [INC-002: CardBasedGradient Random Sampling](#inc-002-cardbasedgradient-randomly-samples-grid-points)
4. [Informational / Design Decisions](#informational--design-decisions)
   - [GOT-001: 4-Direction vs 8-Direction Neighbors](#got-001-4-direction-vs-8-direction-neighbor-inconsistency)
   - [GOT-002: Soft Modifiers Apply Multiplicatively](#got-002-soft-modifiers-apply-multiplicatively)
   - [GOT-003: Transition Spacing is Position-Agnostic](#got-003-transition-spacing-constraint-is-position-agnostic)
   - [UNI-001 to UNI-005: Unintuitive Behaviors](#uni-001-to-uni-005-other-unintuitive-behaviors)

---

# High Priority Issues

---

## BUG-001: Missing BiomeMap Update After Corridor Creation

**Severity:** High | **Type:** Bug
**Location:** `SimpleMapGenerator.cs:364-387`

### Problem Description

When `EnsureConnectivity()` carves corridors to connect disconnected map regions, it modifies the terrain tiles but does **not** update the `biomeMap` array. This means:

1. `SimpleMapData.BiomeMap` shows incorrect biome assignments for corridor tiles
2. Any system reading biome data post-generation gets stale information
3. Visual debugging overlays would display wrong biome colors for corridors

### Solution Options

#### Option A: Update biomeMap inline during corridor creation
```csharp
private void CreateCorridor(...)
{
    var biome = _biomeProvider.GetBiomeAt(current);
    tileIds[current.Y, current.X] = biome.SelectPassableTile(_rng);
    biomeMap[current.Y, current.X] = biome.Name;  // ADD THIS LINE
}
```

| Pros | Cons |
|------|------|
| Simple one-line fix | Requires passing `biomeMap` to CreateCorridor |
| Keeps biome data consistent | Slight API change to private method |
| Immediate fix, minimal risk | — |

- [ ] **Select Option A**

---

#### Option B: Post-process biomeMap after all connectivity passes
```csharp
// After EnsureConnectivity(), rebuild affected portions
foreach (var corridorTile in carvedCorridors)
{
    biomeMap[corridorTile.Y, corridorTile.X] = _biomeProvider.GetBiomeAt(corridorTile).Name;
}
```

| Pros | Cons |
|------|------|
| Cleaner separation of concerns | Requires tracking carved corridors |
| Single pass for all corrections | Additional memory for corridor list |
| Easier to extend for other post-processing | Slight performance overhead |

- [ ] **Select Option B**

---

#### Option C: Accept current behavior (document as known limitation)

| Pros | Cons |
|------|------|
| No code changes required | BiomeMap remains incorrect |
| Zero risk of regression | Technical debt accumulates |
| — | Future systems may rely on incorrect data |

- [ ] **Select Option C (Not Recommended)**

---

### Clarifying Questions

**Q1: Are there downstream systems that currently read BiomeMap after generation?**

- [ ] Yes, and they're affected by this bug
- [ ] Yes, but they don't rely on corridor accuracy
- [ ] No, BiomeMap is only used during generation
- [x] Unsure, needs investigation

**Notes:**
```
Need to analyze why we're doing "corridor generation" in the first place,
i would assume it is to ensure all regions of walkable terrain are reachable
however, in order to do that, we should probably not do this as a post-processing step at all,
but rather as a WFC rule that treats the map as a graph, and disallows any tile resolution that would
separate the walkable area into two disconnected regions
```

---

## INC-001: Multi-Biome WFC Uses Only First Biome for Soft Rules

**Severity:** High | **Type:** Inconsistency
**Location:** `WfcMapGenerator.cs:216-224`

### Problem Description

When generating multi-biome maps, the WFC solver only uses the first biome's tile weights. All position-based biome preferences are ignored during tile selection, meaning:

1. A "desert" tile is equally likely in a "forest" area as in a "desert" area
2. The biome gradient only labels cells post-generation, it doesn't influence generation
3. Multi-card inputs don't meaningfully affect terrain distribution

### Solution Options

#### Option A: Pass position-aware biome to WfcTileSelector
```csharp
// During tile selection, query biome at position
public TileDefinition SelectTile(WfcGrid grid, Vector2I position, ...)
{
    var biome = _biomeProvider.GetBiomeAt(position);
    // Apply biome-specific weights
}
```

| Pros | Cons |
|------|------|
| True multi-biome generation | Significant refactor of WfcTileSelector |
| Cards meaningfully affect terrain | May need biome blending at boundaries |
| Matches expected behavior | Performance impact from per-tile biome lookup |
| More interesting map variety | — |

- [ ] **Select Option A**

---

#### Option B: Pre-seed WFC grid with biome hints
```csharp
// Before WFC solve, bias initial possibilities based on biome
foreach (var cell in grid.AllCells())
{
    var biome = _biomeProvider.GetBiomeAt(cell.Position);
    cell.BiasToward(biome.PreferredTiles);
}
```

| Pros | Cons |
|------|------|
| Lighter touch than Option A | Hints may be overridden by WFC constraints |
| Uses existing WFC infrastructure | Less precise biome boundaries |
| Lower implementation risk | May not fully solve the problem |

- [ ] **Select Option B**

---

#### Option C: Use separate WFC solve per biome, then stitch
```csharp
// Solve each biome region independently
foreach (var biomeRegion in FindBiomeRegions())
{
    SolveRegion(biomeRegion);
}
// Handle transitions at boundaries
StitchRegions();
```

| Pros | Cons |
|------|------|
| Clean biome separation | Complex region boundary handling |
| Each region uses correct weights | Stitching may introduce artifacts |
| Parallelizable | Significant architecture change |

- [ ] **Select Option C**

---

#### Option D: Accept current behavior (single-biome effective generation)

| Pros | Cons |
|------|------|
| No changes needed | Multi-card input is misleading to players |
| Current behavior is stable | Feature feels incomplete |
| — | Biome system is underutilized |

- [ ] **Select Option D (Not Recommended)**

---

### Clarifying Questions

**Q1: How important is visual biome distinction to gameplay?**

- [X] Critical - biomes should look distinctly different
- [ ] Important - prefer variation but not mandatory
- [ ] Minor - current homogeneous look is acceptable
- [ ] Need to playtest to determine

**Q2: Are biome boundaries expected to be sharp or gradual?**

- [X] Sharp boundaries (forest ends, desert begins)
- [ ] Gradual transitions (forest → sparse trees → desert)
- [ ] Depends on the biomes involved
- [ ] No preference

**Notes:**
```
The original concept for biomes is as follows:
the input cards describe a shape through 8-dimensional space as a map seed, 
for 1 card a hypersphere centered on the card's signature, for 2 cards a capsule with ends on both card's signatures, 
and for 3 or more, A closed loop/toroid following a bezier curve outline based on the cards' signatures

Biomes are then positioned around the 8d cube outlined by the 8d cube defined by the [-1,+1] axes of the signature
and then as a first pass for the map, we create a "biome gradient map" where each biome shows up in a way related to the map seed

as a result, each biome has a certain strength from +1 to -1 at each tile position.
each tile then, has an association with one or more biomes, 
if a tile matches a biome that is strongly active on a given tile
it should be more likely to be placed in that position (boosted) 
if a tile has a high negative association with the tile it should be less likely (deboosted)

the exact placement rules beyond that should be controlled by other WFC rules, including auto-tiling.
This should lead to natural biome transitions
```

---

# Medium Priority Issues

---

## GOT-004: Terrain Transitions Generated Before Structures/Corridors

**Severity:** Medium | **Type:** Gotcha / Visual Bug
**Location:** `SimpleMapGenerator.cs:287-337`

### Problem Description

`GenerateTerrainTransitions()` runs before `EnsureConnectivity()`. Corridor tiles carved afterward:

1. May violate 2x2 window constraints established by WFC
2. Have no decoration overlays (terrain transitions)
3. May create visual glitches at corridor-terrain boundaries

### Solution Options

#### Option A: Regenerate transitions after all modifications
```csharp
// Move transition generation to after structures and corridors
PlaceStructures(terrainGrid, ...);
EnsureConnectivity(terrainGrid, biomeMap);
var decorationOverlays = GenerateTerrainTransitions(terrainGrid, size);  // MOVED HERE
```

| Pros | Cons |
|------|------|
| Complete transition coverage | Performance: transitions calculated for more tiles |
| No visual artifacts | May need to handle structure tiles specially |
| Clean solution | — |

- [ ] **Select Option A**

---

#### Option B: Regenerate only for affected tiles (incremental update)
```csharp
// Track modified positions, update only those
var modifiedTiles = new HashSet<Vector2I>();
// ... corridor creation populates modifiedTiles ...
UpdateTransitionsAt(decorationOverlays, modifiedTiles);
```

| Pros | Cons |
|------|------|
| Better performance | More complex implementation |
| Minimal redundant work | Need to track all modified positions |
| Localized changes | Boundary effects may need wider update |

- [ ] **Select Option B**

---

#### Option C: Accept visual artifacts for corridors (document limitation)

| Pros | Cons |
|------|------|
| No code changes | Visual quality degradation |
| Maintains current performance | Player-visible issue |
| — | Feels unpolished |

- [ ] **Select Option C**

---

### Clarifying Questions

**Q1: How frequently do corridors get carved?**

- [ ] Rarely (most maps are naturally connected)
- [ ] Sometimes (depends on WFC output)
- [ ] Often (connectivity frequently fails)
- [ ] Unsure, need metrics

**Q2: Are corridor visual artifacts currently noticeable during gameplay?**

- [ ] Yes, very noticeable
- [ ] Somewhat noticeable
- [ ] Haven't noticed them
- [ ] Need to check

**Notes:**
```
again, this entire system makes no sense, 
we should check for clearance when placing structures, not the other way around


```

---

## BUG-004: Exploration Defers Enemy Pursuit But May Miss Closer Enemies

**Severity:** Medium | **Type:** Bug
**Location:** `ExplorationAI.cs:229-238`

### Problem Description

When the AI spots an enemy while following a path, it stores `_pendingEnemyPosition`. If a **closer** enemy is spotted before reaching the destination, the original (farther) enemy is still pursued.

### Solution Options

#### Option A: Always update to closest visible enemy
```csharp
if (_pendingEnemyPosition == null ||
    DistanceTo(closestVisibleEnemy) < DistanceTo(_pendingEnemyPosition.Value))
{
    _pendingEnemyPosition = closestVisibleEnemy;
}
```

| Pros | Cons |
|------|------|
| AI always targets closest threat | More aggressive behavior |
| Simple fix | May feel reactive/twitchy |
| Logical behavior | — |

- [ ] **Select Option A**

---

#### Option B: Track all visible enemies, re-evaluate at destination
```csharp
private HashSet<Vector2I> _visibleEnemies = new();
// On reaching destination, find closest from set
```

| Pros | Cons |
|------|------|
| Full situational awareness | More memory/tracking |
| Strategic decision at destination | Still defers decision |
| Handles multiple enemies well | More complex |

- [ ] **Select Option B**

---

#### Option C: Interrupt path immediately for any enemy
```csharp
// Don't defer - switch to PathToEnemy immediately
CurrentMode = ExplorationMode.PathToEnemy;
_targetEnemy = closestVisibleEnemy;
```

| Pros | Cons |
|------|------|
| Immediate response | May abandon valuable exploration |
| Predictable behavior | Could cause thrashing with many enemies |
| No deferred state | Changes current behavior significantly |

- [ ] **Select Option C**

---

#### Option D: Accept current behavior (design choice)

| Pros | Cons |
|------|------|
| No changes needed | May pursue farther enemy |
| Predictable "finish current action" | Not optimal tactically |

- [X] **Select Option D**

---

### Clarifying Questions

**Q1: What's the intended AI personality?**

- [ ] Aggressive (immediately engage threats)
- [ ] Methodical (finish current objective, then engage)
- [ ] Strategic (evaluate best target, not just closest)
- [ ] No specific design, open to suggestions

**Notes:**
```
don't care... this behavior was implemented in this way to stop the AI from 
swapping back and forth between two tiles and getting softlocked.
```

---

## GOT-005: Player/Enemy Placement is Fully Random

**Severity:** Medium | **Type:** Gotcha / Game Design
**Location:** `SimpleMapGenerator.cs:147-152`

### Problem Description

Player and enemy positions are selected randomly from passable tiles with no constraints:

1. No minimum distance between player and enemies
2. Enemies can spawn in dead-end corners (unfair advantage or too easy)
3. Player can spawn at map edge with limited exploration options
4. No biome-aware spawning

### Solution Options

#### Option A: Minimum distance constraint only
```csharp
var playerStart = shuffledTiles[0];
var validEnemyTiles = shuffledTiles
    .Where(t => ManhattanDistance(t, playerStart) >= MinSpawnDistance)
    .Take(enemyCount);
```

| Pros | Cons |
|------|------|
| Simple implementation | Doesn't address dead-ends or edge spawns |
| Prevents immediate combat | Could fail if map is too small |
| Low risk | — |

**Configuration:** `MinSpawnDistance = ___` (suggested: 8-12 tiles)

- [ ] **Select Option A**

---

#### Option B: Player prefers center, enemies prefer periphery
```csharp
var centerTiles = passableTiles.OrderBy(t => DistanceToCenter(t)).Take(10);
var playerStart = centerTiles.Random();
var edgeTiles = passableTiles.Where(t => !centerTiles.Contains(t));
```

| Pros | Cons |
|------|------|
| Player starts with options | May feel formulaic |
| Enemies are exploration rewards | Assumes center is accessible |
| Natural difficulty curve | Center might not be passable |

- [ ] **Select Option B**

---

#### Option C: Score-based spawn point selection
```csharp
// Score each tile based on: centrality, connectivity, distance from edges, etc.
float ScoreSpawnPoint(Vector2I pos) =>
    centralityScore + connectivityScore + visibilityScore;
```

| Pros | Cons |
|------|------|
| Most sophisticated | Complex to tune |
| Considers multiple factors | Performance overhead |
| Customizable per biome/difficulty | Over-engineering risk |

- [ ] **Select Option C**

---

#### Option D: Accept random spawning (current behavior)

| Pros | Cons |
|------|------|
| No changes | Unfair starts possible |
| True randomness | Poor player experience |

- [X] **Select Option D (Not Recommended)**

---

### Clarifying Questions

**Q1: Should enemies spawn in specific biomes (e.g., monsters in forest, not water)?**

- [ ] Yes, biome-aware spawning preferred
- [ ] No, any passable tile is fine
- [X] Different enemy types for different biomes (future feature)
- [ ] Needs design discussion

**Q2: Is there a target difficulty curve for early exploration?**

- [ ] Easy start, harder as you explore
- [ ] Immediate challenge is fine
- [ ] Random is acceptable
- [X] Should scale with slotted cards

**Notes:**
```
i don't care about enemy placement logic for now, 
i first want to get terrain generation itself to a usable system 
```

---

## BUG-002: DecorationOverlays Generated Before Structure Placement

**Severity:** Medium | **Type:** Bug
**Location:** `SimpleMapGenerator.cs:124-127`

### Problem Description

Structures are placed **after** terrain transitions are calculated. If structures use different terrain types than the underlying terrain, visual transitions may be incorrect.

### Relationship to GOT-004

This issue is related to GOT-004 (corridor transitions). **If you selected Option A for GOT-004 (regenerate transitions after all modifications), this issue is automatically resolved.**

### Solution Options

#### Option A: Addressed by GOT-004 solution
If transitions are regenerated after structures and corridors, this bug is fixed.

- [ ] **Defer to GOT-004 solution**

---

#### Option B: Handle structures separately from terrain transitions
Structures could have their own transition handling that doesn't rely on terrain-level decoration overlays.

| Pros | Cons |
|------|------|
| Clean separation | More complex rendering |
| Structures control own boundaries | Two transition systems to maintain |

- [X] **Select Option B (only if not using GOT-004 Option A)**

---

**Notes:**
```
structures should generate with awareness of the surrounding terrain and distinctly after terrain.
terrain is leading, and structures should themselves adhere to rules about reachability.
for now, though, we're purely trying to get terrain to work in a sensible way.
```

---

# Low Priority Issues

---

## BUG-003: Duplicate Neighbor Counting in Soft Modifiers

**Severity:** Low | **Type:** Code Quality / Performance
**Location:** `NoveltySoftModifier.cs:45-58` and `CompactnessSoftModifier.cs:43-56`

### Problem Description

Both modifiers have nearly identical `CountSameTypeNeighbors()` implementations. This is:
- Maintenance risk (change one, forget the other)
- Minor performance overhead (counting done twice)

### Solution Options

#### Option A: Extract to shared utility
```csharp
// In SoftModifierUtilities.cs
public static int CountSameTypeNeighbors(SoftModifierContext context)
{
    // Shared implementation
}
```

| Pros | Cons |
|------|------|
| Single source of truth | New file/class |
| Easier to maintain | Minor refactor |
| Slight performance gain | — |

- [x] **Select Option A**

---

#### Option B: Cache neighbor count in context
```csharp
public class SoftModifierContext
{
    private int? _cachedNeighborCount;
    public int SameTypeNeighborCount => _cachedNeighborCount ??= CountNeighbors();
}
```

| Pros | Cons |
|------|------|
| Computed once per context | Changes context class |
| Transparent to modifiers | Slightly more complex context |

- [ ] **Select Option B**

---

#### Option C: Accept duplication (low impact)

| Pros | Cons |
|------|------|
| No changes | Tech debt |
| Working code | Maintenance risk |

- [ ] **Select Option C**

---

**Notes:**
```
i do not believe the stuff we're doing with regards to counting neighbors makes any sense in the WFC algorithm
Here are some references to investigate for more details on the algorithm
https://robertheaton.com/2018/12/17/wavefunction-collapse-algorithm/
https://www.boristhebrave.com/2020/04/13/wave-function-collapse-explained/

Our primary modifications to this algorithm are that we have "hard rules" and "soft rules"
a "hard rule" is like the sudoku example: 
every line must contain the numbers 1-9 exactly once
a "soft rule" is a more subtle variant: 
in a forest biome, we want most of our tiles to be related to forest biomes, 
but it's fine to occasionally see an ore vein like you'd expect in a cavern, 
or a set of bones from a desert occasionally, 
a soft rule doesn't *ban* options that don't match it, it simply alters their probability.
as a result, instead of having clear yes/no rules for every tile where a tile is either in, or out of the set of possible tiles
we want a probability distribution, where every tile has a certain "weight" or a relative likelihood of being placed in that position.
a "hard rule" would set the probability to zero, completely banning that tile from spawning. 
A "soft rule" would multiply the probability by some factor, a desert tile in a forest biome would get a 0.1x multiplier, and a forest tile a 3x multiplier.
Then, when we're deciding what tile to pick, instead of picking the tile with the fewest options, we pick the tile with the clearest winner.
shannon entropy might still be relevant, but it might need adjustment to work with our system.
Then, we normalize the odds by summing the total odds and dividing by that (or denormalize the RNG call by multiplying it with the sum of all weights), and resolve the tile with whatever was picked 
```

---

## UNI-006: Loot Generation Has Minimal Card Influence

**Severity:** Low | **Type:** Design / Feature Gap
**Location:** `GameSessionService.cs:352-365`

### Problem Description

Loot is generated as slight variations of `_mapSeeds[0]` only. Neither:
- Additional map seeds (cards 2, 3, ...)
- Ability cards
- Combat performance

...influence the loot signature.

### Solution Options

#### Option A: Blend all map seeds into loot
```csharp
var blendedSignature = CardSignature.Average(_mapSeeds);
// Then apply variation to blended signature
```

| Pros | Cons |
|------|------|
| All input cards matter | Still ignores abilities |
| Simple implementation | May dilute rare card influence |

- [ ] **Select Option A**

---

#### Option B: Loot biased by combat performance + cards
```csharp
var performanceBonus = CalculatePerformanceModifier(combatStats);
var lootSignature = BlendSignatures(_mapSeeds, _abilityCards, performanceBonus);
```

| Pros | Cons |
|------|------|
| Skill-rewarding | More complex design |
| All inputs considered | Needs combat stat tracking |
| Meaningful player agency | Balance tuning required |

- [ ] **Select Option B**

---

#### Option C: Defer (design question for later)

| Pros | Cons |
|------|------|
| No rush | Feature remains incomplete |
| Avoids premature optimization | — |

- [X] **Select Option C**

---

### Clarifying Questions

**Q1: Should loot feel "earned" or "found"?**

- [X] Earned (performance matters)
- [X] Found (exploration matters)
- [X] Matching (loot reflects input cards)
- [ ] Random is fine

**Notes:**
```
Again, this is a very early stub of features we will be adding in the future.
The basic idea is that the cards "infuse" their signature into the land, each tile carrying a signature relative to the shape outlined in INC-001
the land "infuses" its signature into the enemies that spawn
Those enemies then drop loot based on the enemy's signature.
this then causes a gameplay loop where i can target certain signatures in an evolution-esque way.
```

---

## INC-003: RNG Seed Inconsistency Between GameSessionService and WFC

**Severity:** Low | **Type:** Reproducibility
**Location:** `GameSessionService.cs:125` vs `SimpleMapGenerator.cs:187`

### Problem Description

The RNG chain makes exact reproduction difficult:
1. `GameSessionService._rng` uses system time (not reproducible)
2. WFC receives `_rng.Randi()` (depends on prior RNG state)
3. Multiple components share RNG instance (order-dependent)

### Solution Options

#### Option A: Add optional seed parameter for debug/testing
```csharp
public void StartSession(..., ulong? debugSeed = null)
{
    if (debugSeed.HasValue)
        _rng.Seed = debugSeed.Value;
    else
        _rng.Randomize();
}
```

| Pros | Cons |
|------|------|
| Reproducible when needed | Extra parameter |
| Backward compatible | Players probably don't need this |
| Great for bug reports | — |

- [ ] **Select Option A**

---

#### Option B: Expose seed in save data
```csharp
// Save the randomized seed so sessions can be replayed
public class SessionSaveData
{
    public ulong InitialSeed { get; set; }
}
```

| Pros | Cons |
|------|------|
| Perfect replay capability | Adds save complexity |
| Debug any saved game | — |

- [X] **Select Option B**

---

#### Option C: Accept non-reproducibility (current behavior)

| Pros | Cons |
|------|------|
| No changes | Bugs harder to reproduce |
| Simpler code | No replay capability |

- [ ] **Select Option C**

---

**Notes:**
```
Actually i'd like the whole sequence to be deterministic - the list of cards should be used to seed the RNG
```

---

## INC-002: CardBasedGradient Randomly Samples Grid Points

**Severity:** Low | **Type:** Reproducibility
**Location:** `CardBasedGradient.cs:86-115`

### Problem Description

The gradient's 16x16 sample grid is populated using random sampling. Two positions with the same gradient coordinates may get different signatures based on sampling order.

### Solution Options

#### Option A: Use deterministic sampling (seed from card signatures)
```csharp
public CardBasedGradient(List<CardSignature> cards, RandomNumberGenerator parentRng)
{
    var deterministicSeed = ComputeSeedFromCards(cards);
    _rng.Seed = deterministicSeed;
    // Now sampling is reproducible
}
```

| Pros | Cons |
|------|------|
| Same cards = same gradient | Changes behavior |
| Reproducible | May reduce variety |
| Simpler debugging | — |

- [X] **Select Option A**

---

#### Option B: Accept RNG-dependent gradients (current behavior)

| Pros | Cons |
|------|------|
| Maximum variety | Non-deterministic |
| No changes | — |

- [ ] **Select Option B**

---

**Notes:**
```
[Add your notes here]


```

---

# Informational / Design Decisions

These items are not bugs but documented behaviors that may be surprising. Check items you want to address or explicitly document.

---

## GOT-001: 4-Direction vs 8-Direction Neighbor Inconsistency

**Status:** Documented behavior

The codebase uses different neighbor patterns for different purposes:
- 8-directional: WFC propagation (for 2x2 window constraint)
- 4-directional: Blob tracking, pathfinding, exploration

- [ ] Add code comments explaining the rationale
- [ ] Create a constants file documenting which systems use which pattern
- [ ] Accept as-is (documented in audit)

---

## GOT-002: Soft Modifiers Apply Multiplicatively

**Status:** Documented behavior

Weights can vary by orders of magnitude due to multiplicative stacking.

- [ ] Add tooltips/documentation for tuning guidance
- [ ] Consider additive modifiers for some effects
- [ ] Accept as-is (allows powerful effects)

---

## GOT-003: Transition Spacing Constraint is Position-Agnostic

**Status:** Documented behavior / Design gap

The 2x2 window constraint doesn't consider biome boundaries.

- [ ] This is addressed by INC-001 solution
- [ ] Accept as-is (WFC manages it)

---

## UNI-001 to UNI-005: Other Unintuitive Behaviors

Check any you want to address:

- [X] **UNI-001:** Map size depends only on first card → Consider averaging all cards (it should, and i'm not even convinced the first card has any effect currently, or is that why we get weird 1 pixel gaps occasionally?)
- [ ] **UNI-002:** Biome selection uses equal 8D Euclidean distance → Consider weighted dimensions
- [ ] **UNI-003:** WFC retry seeds are deterministic → Accept (actually a feature for reproducibility)
- [ ] **UNI-004:** Trivially visible tiles skip movement → Add config flag for "walk everywhere" mode
- [X] **UNI-005:** Combat uses only first map seed → Blend all seeds for combat

---

# Summary & Prioritization

After completing this questionnaire, use this section to record your final decisions.

## Selected Solutions

| Issue | Selected Option | Implementation Priority |
|-------|-----------------|------------------------|
| BUG-001 | [ ] A / [ ] B / [ ] C | [ ] Now / [ ] Soon / [ ] Later |
| INC-001 | [ ] A / [ ] B / [ ] C / [ ] D | [ ] Now / [ ] Soon / [ ] Later |
| GOT-004 | [ ] A / [ ] B / [ ] C | [ ] Now / [ ] Soon / [ ] Later |
| BUG-004 | [ ] A / [ ] B / [ ] C / [ ] D | [ ] Now / [ ] Soon / [ ] Later |
| GOT-005 | [ ] A / [ ] B / [ ] C / [ ] D | [ ] Now / [ ] Soon / [ ] Later |
| BUG-002 | [ ] Defer to GOT-004 / [ ] B | [ ] Now / [ ] Soon / [ ] Later |
| BUG-003 | [ ] A / [ ] B / [ ] C | [ ] Now / [ ] Soon / [ ] Later |
| UNI-006 | [ ] A / [ ] B / [ ] C | [ ] Now / [ ] Soon / [ ] Later |
| INC-003 | [ ] A / [ ] B / [ ] C | [ ] Now / [ ] Soon / [ ] Later |
| INC-002 | [ ] A / [ ] B | [ ] Now / [ ] Soon / [ ] Later |

## Implementation Order

Based on dependencies, suggested implementation order:

1. **BUG-001** (prerequisite for correct biome tracking)
2. **INC-001** (core feature improvement)
3. **GOT-004 + BUG-002** (visual polish, dependent on each other)
4. **GOT-005** (gameplay balance)
5. **BUG-004** (AI improvement)
6. **Low priority items** as time permits

## Additional Notes

```
[Add overall thoughts, concerns, or additional items to address]




```

---

*Generated from [Map Generation Audit Report](./MAP_GENERATION_AUDIT.md)*
