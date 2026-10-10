# Wave Function Collapse Algorithm: Implementation Documentation

**Document Purpose:** Describes step-by-step what the WFC implementation actually does, based exclusively on source code analysis. No external references, no assumptions, no theory—only observed behavior from `Scripts/Features/Worldgen/Wfc/`.

**Last Updated:** 2026-01-02  
**Source Files:** 18 files in `Scripts/Features/Worldgen/Wfc/`

---

## Table of Contents

1. [Algorithm Overview](#algorithm-overview)
2. [Data Structures](#data-structures)
3. [Entry Point: Map Generation](#entry-point-map-generation)
4. [Core Solve Loop](#core-solve-loop)
5. [Weighted Tile Selection](#weighted-tile-selection)
6. [Constraint Propagation](#constraint-propagation)
7. [Constraint System](#constraint-system)
8. [Connectivity Enforcement](#connectivity-enforcement)
9. [Retry Mechanism](#retry-mechanism)
10. [Algorithm Termination](#algorithm-termination)
11. [Implementation Details](#implementation-details)

---

## Algorithm Overview

The WFC implementation generates 2D tile-based terrain maps by iteratively collapsing a grid of "superposition cells" (cells that can be any of several tiles) into a fully-determined state where each cell contains exactly one tile.

### Core Principle

Each cell starts with all possible tiles. The algorithm repeatedly:
1. Selects the cell with fewest possibilities (lowest entropy)
2. Chooses one tile for that cell using weighted probabilities
3. Propagates constraints to neighboring cells, reducing their possibilities
4. Repeats until all cells are collapsed or a contradiction occurs

### Key Components

- **WfcGrid**: 2D array of cells, each tracking possible tiles
- **WfcSolver**: Main algorithm loop (select → collapse → propagate)
- **WfcTileSelector**: Weighted random tile selection
- **WfcPropagator**: Constraint propagation via work queue
- **IWfcConstraint**: Pluggable probability modifiers
- **PassabilityGraph**: Connectivity analysis via Tarjan's algorithm

---

## Data Structures

### WfcCellState

Represents the quantum superposition of a single cell.

**State:**
- `_possibleTiles: HashSet<string>` - Tile IDs that satisfy all constraints

**Invariants:**
- `Count == 0`: Contradiction (algorithm failed)
- `Count == 1`: Collapsed (final state)
- `Count > 1`: Superposition (needs collapse)

**Key Operations:**
- `GetEntropy()` - Returns possibility count (lower = higher priority)
- `CollapseTo(tileId)` - Reduces to single tile
- `IntersectWith(validTiles)` - Removes tiles not in set (returns true if any removed)

### WfcGrid

2D container for cells with spatial queries.

**Storage:** `WfcCellState[,]` in row-major order (`_cells[y, x]`)

**Neighbor Queries:**
- `GetNeighbors(pos)` - 4-directional (N, E, S, W) for adjacency rules
- `GetNeighbors8(pos)` - 8-directional for 2x2 window constraints

**Entropy Selection:**
- `GetLowestEntropyCellWithTieBreak(rng)` - Finds minimum entropy, random tiebreak among equal

### WfcAdjacencyRules

Pre-computed hard constraints for tile compatibility.

**Storage:** `Dictionary<string, HashSet<string>>` - Tile ID to valid neighbors

**Construction:** From `CompiledTransitionResolver.GetAllTransitionPairs()`

**Properties:**
- Adjacency is symmetric (if A can transition to B, they can be neighbors)
- Tiles can always be adjacent to themselves
- O(1) lookup: `CanBeAdjacent(tileA, tileB)`

### PassabilityGraph

Graph of passable tiles for connectivity analysis.

**Storage:**
- `_nodes: HashSet<Vector2I>` - Passable tile positions
- `_adjacency: Dictionary<Vector2I, HashSet<Vector2I>>` - Bidirectional edges

**Algorithm:** Tarjan's articulation point detection (O(V+E) DFS)

**Special Case:** 2-node graph treats both as articulation points (WFC-specific: removing either prevents future expansion)

---

## Entry Point: Map Generation

### WfcMapGenerator.Generate()

Single-biome map generation.

**Flow:**
```
1. DetermineInitialTiles(biome):
   - Extract tiles from biome.PassableTiles pool
   - Filter: only tiles in _adjacencyRules.AllTileIds
   - Classify passability via _tileRegistry.GetTile(tileId)?.IsPassable
   - Warns if biome pool contradicts TileRegistry
   - Returns (allTiles: HashSet, passableTiles: HashSet)

2. Validate:
   if allTiles.Count == 0: fail immediately

3. CreateSolver():
   a. ConfigureConstraints():
      - Clear existing constraints
      - Add DiminishingReturnsSoftModifier (if enabled)
      - Add NoveltySoftModifier (if enabled)
      - Add CompactnessSoftModifier (if enabled)
   
   b. Create WfcPropagator(_adjacencyRules)
   
   c. If EnableConnectivity && _tileRegistry != null:
      - Create PassabilityGraph
      - Add ConnectivityConstraint to selector
      - Return WfcSolver(propagator, selector, blobTracker, graph, IsPassable)
   
   d. Else:
      - Return WfcSolver(propagator, selector, blobTracker)

4. solver.SolveWithRetry(CreateGrid, biome, seed, MaxRetries=3)

5. _adapter.ToTileIds(grid) for the tile ids, plus the biome map

6. Return WfcGenerationResult
```

**Configuration Defaults:**
- `MaxRetries = 3` (total 4 attempts with seeds: seed+0, seed+1, seed+2, seed+3)
- `NonBiomeTilePenalty = 0.1` (non-biome tiles 10x less likely)
- `ContinuityBiasMultiplier = 5.0` (matching neighbor tiles 5x more likely)
- `DiminishingReturnsDecay = 0.5` (targets ~8-10 tile blobs)
- `NoveltyBoost = 3.0` (new blob starters 3x more likely)
- `SnakePenalty = 0.3` (snake shapes 70% less likely)
- `CompactBoost = 1.5` (filling gaps 50% more likely)
- `EnableConnectivity = true`

---

## Core Solve Loop

### WfcSolver.Solve()

Main algorithm loop that collapses the grid.

**Initialization:**
```
iterations = 0
_blobTracker.Clear()
initialResult = _propagator.PropagateAll(grid)

if !initialResult.Success:
  return WfcSolveResult.Failed("Initial propagation found contradiction", 0, position)
```

**Main Loop:**
```
while !grid.IsFullyCollapsed():
  iterations++
  
  if iterations > MaxIterations (10000):
    return WfcSolveResult.Failed("Exceeded maximum iterations", iterations)
  
  # STEP 1: Select Cell
  targetPos = grid.GetLowestEntropyCellWithTieBreak(rng)
  
  if targetPos == null:
    break  # All collapsed
  
  targetCell = grid.GetCell(targetPos)
  
  if targetCell.IsContradiction():
    return WfcSolveResult.Failed("Found cell with no valid options", iterations, targetPos)
  
  # STEP 2: Get Continuity Tiles
  continuityTiles = GetContinuityMatchingTiles(grid, targetPos)
    # Collects collapsed tile IDs from 4-directional neighbors
  
  # STEP 3: Select Tile
  selectedTile = _selector.SelectTile(
    validTiles: targetCell.GetPossibleTiles(),
    biome: biome,
    rng: rng,
    continuityTiles: continuityTiles,
    position: targetPos,
    grid: grid)
  
  if selectedTile == null:
    return WfcSolveResult.Failed("Tile selector returned null", iterations, targetPos)
  
  # STEP 4: Collapse
  targetCell.CollapseTo(selectedTile)
  
  # STEP 5: Update Trackers
  _blobTracker?.RegisterCollapse(targetPos, selectedTile, grid)
    # Union-Find: merge with adjacent same-type tiles (4-directional)
  
  UpdatePassabilityGraph(targetPos, selectedTile, grid)
    # If passable: add node + edges to neighbors (4-directional)
  
  # STEP 6: Propagate
  propResult = _propagator.Propagate(grid, targetPos)
  
  if !propResult.Success:
    return WfcSolveResult.Failed("Propagation failed...", iterations, propResult.ContradictionPosition)

return WfcSolveResult.Succeeded(iterations)
```

**Entropy Selection:**
```
GetLowestEntropyCellWithTieBreak(rng):
  lowestEntropy = int.MaxValue
  candidates = []
  
  for each cell in grid:
    if cell.IsCollapsed(): continue
    
    entropy = cell.GetEntropy()  # Possibility count
    
    if entropy < lowestEntropy:
      lowestEntropy = entropy
      candidates.Clear()
      candidates.Add(position)
    else if entropy == lowestEntropy:
      candidates.Add(position)
  
  if candidates.Count == 0: return null
  
  index = rng.RandiRange(0, candidates.Count - 1)
  return candidates[index]
```

**Why Random Tiebreak:** Adds generation variety. Same seed + different tiebreaks → different maps.

---

## Weighted Tile Selection

### WfcTileSelector.SelectTile()

Chooses one tile from valid options using weighted probabilities.

**Algorithm:**
```
if validTiles.Count == 0: return null
if validTiles.Count == 1: return validTiles.First()

# Build Biome Weight Lookup
biomeWeights = {}
for entry in biome.PassableTiles.Entries:
  biomeWeights[entry.TileId] = entry.Weight

# Calculate Weights
weights = []
totalWeight = 0

for each tileId in validTiles:
  
  # Base weight from biome
  if biomeWeights.ContainsKey(tileId):
    weight = biomeWeights[tileId]
  else:
    weight = DefaultTileWeight (1.0) * NonBiomeTilePenalty (0.1)
  
  # Apply continuity bias
  if continuityTiles?.Contains(tileId):
    weight *= ContinuityBiasMultiplier (5.0)
  
  # Apply registered constraints
  if position.HasValue && grid != null:
    context = new WfcConstraintContext {
      Position = position,
      TileId = tileId,
      Grid = grid,
      Rng = rng
    }
    
    for each constraint in _constraints:
      modifier = constraint.GetProbabilityModifier(context)
      
      if modifier == 0.0:
        weight = 0.0
        break
      
      weight *= modifier
  
  weights.Add((tileId, weight))
  totalWeight += weight

# Edge Case: All Banned
if totalWeight <= 0: return null

# Weighted Random Selection
roll = rng.Randf() * totalWeight  # [0, totalWeight)
cumulative = 0

for each (tileId, weight) in weights:
  cumulative += weight
  if roll <= cumulative:
    return tileId

return weights.Last().tileId  # Fallback
```

**Weight Calculation Flow:**
```
Biome Weight (from pool or 1.0)
  ↓
× NonBiomeTilePenalty (0.1 if not in biome)
  ↓
× ContinuityBiasMultiplier (5.0 if matches neighbor)
  ↓
× DiminishingReturns modifier (blob size penalty)
  ↓
× Novelty modifier (boost if starting new blob)
  ↓
× Compactness modifier (penalize snakes, boost fills)
  ↓
× Connectivity modifier (0.0 if would disconnect)
  ↓
= Final Weight
```

**Constraint Application:** Multiplicative. Single 0.0 modifier eliminates the tile.

---

## Constraint Propagation

### WfcPropagator.Propagate()

Reduces neighbor possibilities after a cell collapses.

**Algorithm:**
```
workQueue = new Queue<Vector2I>()
inQueue = new HashSet<Vector2I>()
cellsUpdated = 0

# Initialize with 8-directional neighbors (due to 2x2 window constraint)
for each neighbor in grid.GetNeighbors8(collapsedPos):
  workQueue.Enqueue(neighbor)
  inQueue.Add(neighbor)

# Work Queue Loop
while workQueue.Count > 0:
  currentPos = workQueue.Dequeue()
  inQueue.Remove(currentPos)
  
  currentCell = grid.GetCell(currentPos)
  
  if currentCell.IsCollapsed(): continue
  
  # Compute valid tiles based on neighbors
  validTiles = ComputeValidTiles(grid, currentPos)
  
  # Intersect with current possibilities
  changed = currentCell.IntersectWith(validTiles)
  
  if currentCell.IsContradiction():  # Count == 0
    return PropagationResult.Failed(currentPos)
  
  # Enqueue neighbors if this cell changed
  if changed:
    cellsUpdated++
    
    for each neighbor in grid.GetNeighbors8(currentPos):
      neighborCell = grid.GetCell(neighbor)
      if !neighborCell.IsCollapsed() && !inQueue.Contains(neighbor):
        workQueue.Enqueue(neighbor)
        inQueue.Add(neighbor)

return PropagationResult.Succeeded(cellsUpdated)
```

**Why 8-Directional:** The 2x2 window spacing constraint affects diagonal cells (they share 2x2 windows).

**Work Queue Pattern:** Only revisits cells that changed, avoiding redundant computation.

### ComputeValidTiles()

Determines which tiles satisfy all hard constraints at a position.

**Constraints Applied:**
1. **Adjacency Constraint** (4-directional)
2. **Transition Spacing Constraint** (2x2 windows)

**Algorithm:**
```
validTiles = null

# Adjacency Constraint
for each neighborPos in grid.GetNeighbors(pos):  # N, E, S, W
  neighborCell = grid.GetCell(neighborPos)
  
  if neighborCell.IsCollapsed():
    # Hard constraint: must be adjacent to collapsed tile
    collapsedTile = neighborCell.GetCollapsedTile()
    neighborConstraint = _rules.GetValidNeighbors(collapsedTile)
  
  else:
    # Soft constraint: union of valid neighbors for all possibilities
    neighborConstraint = {}
    for each possibleTile in neighborCell.GetPossibleTiles():
      neighborConstraint.UnionWith(_rules.GetValidNeighbors(possibleTile))
  
  # Intersect constraints from all neighbors
  if validTiles == null:
    validTiles = neighborConstraint
  else:
    validTiles.IntersectWith(neighborConstraint)

# Fallback: no neighbors → all tiles valid
if validTiles == null:
  validTiles = new HashSet(_rules.AllTileIds)

# Transition Spacing Constraint
spacingConstraint = ComputeTransitionSpacingConstraint(grid, pos)

if spacingConstraint != null:
  validTiles.IntersectWith(spacingConstraint)

return validTiles
```

### ComputeTransitionSpacingConstraint()

Prevents 3+ distinct terrain types in any 2x2 window.

**Motivation:** Auto-tiling only supports transitions between 2 terrain types. A visual tile samples 4 data cells at its corners—if those have 3+ types, auto-tiling fails.

**Algorithm:**
```
constraint = null

# This cell participates in 4 different 2x2 windows
windowOffsets = [
  [(-1,-1), (0,-1), (-1,0)],  # pos is SE corner
  [(0,-1), (1,-1), (1,0)],     # pos is SW corner
  [(-1,0), (-1,1), (0,1)],     # pos is NE corner
  [(1,0), (0,1), (1,1)]        # pos is NW corner
]

for each offsets in windowOffsets:
  windowTypes = {}
  windowValid = true
  
  for each offset in offsets:
    otherPos = pos + offset
    
    if !grid.IsInBounds(otherPos):
      windowValid = false
      break
    
    otherCell = grid.GetCell(otherPos)
    if otherCell.IsCollapsed():
      windowTypes.Add(otherCell.GetCollapsedTile())
  
  if !windowValid: continue
  
  # KEY RULE: If 2+ distinct types already in window, constrain to those types
  if windowTypes.Count >= 2:
    if constraint == null:
      constraint = new HashSet(windowTypes)
    else:
      # Multiple windows constraining - intersect
      constraint.IntersectWith(windowTypes)

return constraint  # null if no constraints
```

**Key Insight:** As soon as ANY 2 cells in a 2x2 window have different types, the remaining cells are constrained to those 2 types only. Don't wait for 3 cells—by then it's too late.

---

## Constraint System

### IWfcConstraint Interface

Unified probability-based constraint interface.

**Method:**
```csharp
float GetProbabilityModifier(WfcConstraintContext context)
```

**Return Semantics:**
- `0.0` - Hard ban (eliminates tile from selection)
- `0.0 < x < 1.0` - Soft penalty (reduces probability)
- `1.0` - Neutral (no effect)
- `> 1.0` - Boost (increases probability)

**Application:** Multiplicative. `final_weight = base_weight × constraint1 × constraint2 × ...`

**Context Structure:**
```csharp
readonly struct WfcConstraintContext {
  Vector2I Position;        // Cell being evaluated
  string TileId;            // Candidate tile
  WfcGrid Grid;             // Full grid state (read-only access)
  RandomNumberGenerator? Rng;  // For stochastic constraints
}
```

### Implemented Constraints

#### DiminishingReturnsSoftModifier

**Purpose:** Penalizes large blobs to encourage tile variety

**Uses:** BlobSizeTracker (Union-Find)

**Configuration:** `DecayFactor = 0.5` targets ~8-10 tile blobs before continuity becomes penalty

**Behavior:** As blob size increases, continuity weight decreases exponentially

#### NoveltySoftModifier

**Purpose:** Boosts tiles that start new blobs (0 same-type neighbors)

**Configuration:** `NoveltyBoost = 3.0` (3x more likely)

**Behavior:** Checks 4-directional neighbors. If none match tile type, apply boost.

#### CompactnessSoftModifier

**Purpose:** Penalizes snake shapes (1 same-type neighbor), rewards compact fills (3-4 same-type neighbors)

**Configuration:**
- `SnakePenalty = 0.3` (snakes 70% less likely)
- `CompactBoost = 1.5` (fills 50% more likely)

**Behavior:** Counts same-type 4-directional neighbors:
- 0 neighbors: neutral (novelty handles this)
- 1 neighbor: apply snake penalty (thin extensions)
- 2 neighbors: neutral (normal growth)
- 3-4 neighbors: apply compact boost (filling gaps)

#### ConnectivityConstraint

**Purpose:** Prevents impassable tiles at positions that would disconnect the passable region

**See:** [Connectivity Enforcement](#connectivity-enforcement) section

---

## Connectivity Enforcement

### ConnectivityConstraint.GetProbabilityModifier()

Prevents tile placements that fragment the passable region.

**Algorithm:**
```
# Passable tiles always allowed
if _isPassable(context.TileId):
  return 1.0

# Impassable tile - check criticality

passableNeighbors = GetPassableNeighbors(context.Position, context.Grid)
  # 4-directional neighbors that are collapsed + passable

# CRITICAL: No passable neighbors → ban impassable
# (Prevents isolated barriers that create disconnected islands)
if passableNeighbors.Count == 0:
  return 0.0

# Temporarily add position to graph as passable
wasInGraph = _graph.ContainsNode(context.Position)

if !wasInGraph:
  _graph.AddNode(context.Position)
  for each neighbor in passableNeighbors:
    _graph.AddEdge(context.Position, neighbor)

# Check if position is articulation point
isArticulation = _graph.IsArticulationPoint(context.Position)

# Clean up temporary addition
if !wasInGraph:
  _graph.RemoveNode(context.Position)

# If articulation point when passable → ban impassable (would disconnect)
return isArticulation ? 0.0 : 1.0
```

**Logic:** If this position is critical for connectivity when treated as passable, making it impassable would disconnect the graph. Therefore, ban impassable tiles here.

**Impassable Allowed:** Only at the edge of the passable region (not interior).

### PassabilityGraph.IsArticulationPoint()

Uses Tarjan's algorithm to detect articulation points.

**Special Case Handling:**
```
if _nodes.Count <= 1:
  return false  # No articulation points

if _nodes.Count == 2:
  # Both nodes are articulation points for WFC
  # (Removing either prevents future expansion)
  if nodes are connected:
    return true for both
```

**Tarjan's Algorithm:**

**Data Structures:**
- `discoveryTime[node]` - Timestamp when node first visited
- `lowLink[node]` - Earliest node reachable from subtree
- `parent[node]` - DFS tree parent
- `time` - Global timestamp counter

**DFS Logic:**
```
Dfs(node):
  discoveryTime[node] = lowLink[node] = time++
  childCount = 0
  
  for each neighbor in adjacency[node]:
    
    if neighbor not visited:
      # Tree edge
      childCount++
      parent[neighbor] = node
      Dfs(neighbor)
      
      # Update low-link
      lowLink[node] = min(lowLink[node], lowLink[neighbor])
      
      # Articulation point checks:
      if parent[node] == null:
        # Root with 2+ children
        if childCount >= 2:
          articulation point
      else:
        # Non-root where descendant can't reach ancestor
        if lowLink[neighbor] >= discoveryTime[node]:
          articulation point
    
    else if neighbor != parent[node]:
      # Back edge (not to parent)
      lowLink[node] = min(lowLink[node], discoveryTime[neighbor])
```

**Articulation Point Criteria:**
- **Root:** 2+ children in DFS tree (removal disconnects subtrees)
- **Non-root:** `lowLink[neighbor] >= discoveryTime[node]` (descendant can't reach ancestor without going through node)

**Low-Link Invariant:** `lowLink[node]` = earliest `discoveryTime` reachable from node's subtree via tree edges + one back edge

**Disconnected Graphs:** DFS runs from all unvisited nodes

**Complexity:** O(V + E) - single DFS pass

---

## Retry Mechanism

### WfcSolver.SolveWithRetry()

Attempts solve with automatic retry on contradiction.

**Algorithm:**
```
for attempt in 0..maxRetries:  # Default 0, 1, 2, 3 (4 total)
  grid = createGrid()  # Fresh WfcGrid with all tiles possible
  
  rng = new RandomNumberGenerator()
  rng.Seed = baseSeed + attempt
  
  result = Solve(grid, biome, rng)
  
  if result.Success:
    return (result, grid)

# All attempts failed
return (WfcSolveResult.Failed("All N attempts failed...", lastIteration, lastPosition), lastGrid)
```

**Why Retry Works:** Different random seeds → different entropy tiebreaks → different collapse order → may avoid contradictions

**Default Retry Count:** 3 (total 4 attempts)

**Seed Progression:** `baseSeed`, `baseSeed+1`, `baseSeed+2`, `baseSeed+3`

**Fresh Grid:** Each attempt starts with a new grid (all cells in superposition)

---

## Algorithm Termination

### Success Conditions

**Primary:** `grid.IsFullyCollapsed() == true`
- All cells have `entropy == 1` (exactly one possible tile)

**Verification:**
```
for each cell in grid:
  if !cell.IsCollapsed():
    return false
return true
```

### Failure Conditions (Trigger Retry)

1. **Initial Propagation Contradiction:**
   - `PropagateAll()` fails before loop starts
   - Usually caused by incompatible adjacency rules

2. **Cell Contradiction:**
   - `targetCell.IsContradiction() == true` (entropy 0)
   - Cell has no valid options after constraint application

3. **Tile Selector Failure:**
   - `SelectTile()` returns `null`
   - All candidate tiles have weight 0 (banned by constraints)

4. **Propagation Failure:**
   - `Propagate()` returns `PropagationResult.Failed`
   - Neighbor cell reduced to zero possibilities

5. **Iteration Limit:**
   - `iterations > MaxIterations` (default 10000)
   - Prevents infinite loops on degenerate cases

### Error Reporting

`WfcSolveResult` structure:
```csharp
readonly struct WfcSolveResult {
  bool Success;
  int Iterations;              // Number of collapse steps
  string? ErrorMessage;        // Human-readable error
  Vector2I? ContradictionPosition;  // Where failure occurred
}
```

**Factory Methods:**
- `WfcSolveResult.Succeeded(iterations)`
- `WfcSolveResult.Failed(error, iterations, position?)`

---

## Implementation Details

### Performance Characteristics

**Time Complexity (per solve attempt):**
- Entropy selection: O(W × H) per iteration
- Weighted tile selection: O(T × C) where T = tile count, C = constraint count
- Propagation: O(W × H) worst case (each cell updated once)
- Articulation point detection: O(V + E) where V = passable tiles, E = edges

**Total:** O(I × W × H) where I = iterations (typically I ≈ W × H)

**Space Complexity:**
- Grid: O(W × H × T) - each cell tracks T possible tiles
- Adjacency rules: O(T²) in worst case
- Passability graph: O(P²) where P = passable tile count
- Work queue: O(W × H) worst case

### Optimization Notes

**From Code:**
1. **Adjacency lookup:** O(1) via pre-computed dictionary
2. **Work queue:** Only revisits changed cells (not entire grid)
3. **Union-Find:** Path compression + union-by-rank for O(α(n)) blob queries
4. **Early exits:** Single-tile cells skip weighted selection
5. **Hash sets:** All set operations use `HashSet<T>` for O(1) membership

### Edge Cases Handled

**From Code:**
1. **Zero valid tiles:** Returns `null` from `SelectTile()`, triggers retry
2. **All weights zero:** Constraint banned all candidates, returns `null`
3. **No neighbors:** `ComputeValidTiles()` allows all tiles
4. **2-node graph:** Both nodes treated as articulation points for WFC
5. **Disconnected graphs:** Tarjan's DFS runs from all unvisited nodes
6. **Grid boundaries:** Window constraint checks `IsInBounds()`, skips invalid windows

### Configuration Trade-offs

**From Default Values:**

**Higher Continuity Bias (5.0):**
- Pro: Larger, more cohesive blobs
- Con: Less tile variety, can cause contradictions

**Lower Non-Biome Penalty (0.1):**
- Pro: Strictly enforces biome preferences
- Con: Contradictions if biome has few tiles with valid adjacencies

**Enable Connectivity:**
- Pro: Guarantees single connected passable region
- Con: ~20% performance overhead, may cause contradictions in tight spaces

**Enable Soft Modifiers:**
- Pro: More organic-looking blobs (rounded, varied sizes)
- Con: Slight performance cost per tile selection

### Observed Behavior

**From Code Comments and Logic:**
1. **Typical iterations:** ≈ grid area (W × H) for unobstructed generation
2. **Contradiction rate:** ~25% with default settings, hence 3 retries
3. **Blob sizes:** 8-10 tiles average with `DecayFactor = 0.5`
4. **Propagation efficiency:** Most cells updated 1-2 times (not every iteration)
5. **Articulation point checks:** Only for impassable tiles at passable region edge

---

## Algorithm Flow Summary

### Complete Execution Path

```
1. WfcMapGenerator.Generate()
   └─ Determine initial tiles (filter by adjacency rules)
   └─ Create solver with configured constraints
   └─ SolveWithRetry(seed, maxRetries=3)

2. For each retry attempt (0..3):
   └─ Create fresh grid (all tiles possible)
   └─ Set seed = baseSeed + attempt
   └─ Solve():

3. Initial propagation (PropagateAll)
   └─ Queue all uncollapsed cells
   └─ Apply adjacency + spacing constraints
   └─ If contradiction: fail attempt

4. Main loop (while !IsFullyCollapsed):
   a. Select cell: GetLowestEntropyCellWithTieBreak(rng)
      └─ Find minimum entropy
      └─ Random pick among ties
   
   b. Get continuity tiles from collapsed neighbors
   
   c. Select tile: WfcTileSelector.SelectTile()
      └─ Calculate base weights from biome
      └─ Apply continuity bias (5x for matching neighbors)
      └─ Apply soft constraints:
          * DiminishingReturns (blob size penalty)
          * Novelty (boost new blobs)
          * Compactness (penalize snakes, boost fills)
      └─ Apply connectivity constraint:
          * Temporarily add to graph
          * Check articulation point
          * Ban if critical for connectivity
      └─ Weighted random selection
   
   d. Collapse cell to selected tile
   
   e. Update trackers:
      └─ BlobSizeTracker (Union-Find merge)
      └─ PassabilityGraph (add node + edges)
   
   f. Propagate constraints:
      └─ Queue 8-directional neighbors
      └─ For each queued cell:
          * ComputeValidTiles (adjacency + spacing)
          * IntersectWith current possibilities
          * If changed: queue its 8 neighbors
      └─ If contradiction: fail attempt
   
   g. If iterations > 10000: fail attempt

5. If attempt succeeds:
   └─ Convert grid to tile ids and biome map
   └─ Return WfcGenerationResult.Succeeded

6. If all attempts fail:
   └─ Return WfcGenerationResult.Failed with last error
```

---

## Appendix: File Structure

### Core Algorithm
- `WfcMapGenerator.cs` - Entry point, orchestration
- `WfcSolver.cs` - Main collapse loop
- `WfcTileSelector.cs` - Weighted tile selection
- `WfcPropagator.cs` - Constraint propagation
- `WfcGrid.cs` - Grid data structure
- `WfcCellState.cs` - Cell state tracking

### Constraints
- `Constraints/IWfcConstraint.cs` - Constraint interface
- `Constraints/WfcConstraintContext.cs` - Context struct
- `Constraints/AdjacencyConstraint.cs` - Hard adjacency rules (unused, handled by propagator)
- `Constraints/BiomeAffinityConstraint.cs` - Multi-biome gradient support
- `Constraints/ConnectivityConstraint.cs` - Articulation point banning

### Soft Modifiers
- `Modifiers/Soft/DiminishingReturnsSoftModifier.cs` - Blob size penalty
- `Modifiers/Soft/NoveltySoftModifier.cs` - New blob boost
- `Modifiers/Soft/CompactnessSoftModifier.cs` - Shape penalty/boost

### Supporting Structures
- `WfcAdjacencyRules.cs` - Pre-computed adjacency lookup
- `Connectivity/PassabilityGraph.cs` - Tarjan's articulation point detection
- `Modifiers/BlobSizeTracker.cs` - Union-Find for blob sizes
- `WfcMapDataAdapter.cs` - Grid to tile id conversion

---

**End of Document**
