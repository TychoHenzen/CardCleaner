# World Generation Architecture

This document describes the architecture for CardCleaner's procedural world generation system, built on a **Soft WFC** approach using probability-based tile selection with composable weight modifiers.

## Table of Contents

1. [Core Concept: Soft WFC](#core-concept-soft-wfc)
2. [Current State](#current-state)
3. [Weight Modifier System](#weight-modifier-system)
4. [Generation Pipeline](#generation-pipeline)
5. [Implementation Phases](#implementation-phases)
6. [Data Definition Guide](#data-definition-guide)
7. [Technical Reference](#technical-reference)

---

## Core Concept: Soft WFC

### The Problem with Hard Constraints

Traditional Wave Function Collapse (WFC) uses **hard socket rules**: tile X at position (0,0) completely bans tiles A, B, C from position (0,1). This approach has drawbacks:

- **Constraint explosion**: Each tile needs explicit compatibility rules with every other tile
- **Brittle failures**: One incompatible placement can make entire regions unsolvable
- **Difficult to extend**: Adding new tiles requires updating many existing constraint definitions
- **Over-deterministic**: Maps feel "solved" rather than naturally varied

### Soft WFC: Probability Over Prohibition

CardCleaner uses **Soft WFC** - a probabilistic approach where:

```
Instead of: "Forest floor tiles are BANNED in mountain biomes"
We use:     "Forest floor tiles have 0.2x weight in mountain biomes"
```

**Key Principles:**

1. **No hard bans** - Every tile has some probability everywhere (even if tiny)
2. **Multiplicative weights** - Multiple factors combine: biome affinity × adjacency boost × spacing penalty
3. **Composable modifiers** - New rules added without changing existing ones
4. **Context-aware selection** - Same tile pool, different probabilities based on position and neighbors

### How It Works

```
┌─────────────────────────────────────────────────────────────────┐
│                    SOFT WFC TILE SELECTION                      │
├─────────────────────────────────────────────────────────────────┤
│  1. Initialize all candidate tiles with weight 1.0              │
│                                                                 │
│  2. Apply weight modifier pipeline:                             │
│     weight *= BiomeAffinityModifier    (forest floor → 1.5x)   │
│     weight *= AdjacencyBoostModifier   (matches neighbor → 2x) │
│     weight *= DecorationSpacing        (near decor → 0.3x)     │
│     weight *= StructureProximity       (near shrine → 1.8x)    │
│     ... additional modifiers ...                                │
│                                                                 │
│  3. Weighted random selection from final distribution           │
│     Tile A: 0.45, Tile B: 2.7, Tile C: 0.1 → B most likely     │
└─────────────────────────────────────────────────────────────────┘
```

### Comparison: Hard vs Soft Constraints

| Aspect | Hard WFC (Socket Rules) | Soft WFC (Weight Modifiers) |
|--------|-------------------------|----------------------------|
| Constraint type | Binary (allowed/banned) | Continuous (0.0 to ∞) |
| Failure mode | Contradiction → backtrack | Always solvable |
| Adding new tiles | Update all neighbor rules | Define affinity values |
| Map variety | Deterministic patterns | Probabilistic variation |
| Biome influence | Must encode in sockets | Natural weight adjustment |
| Performance | Constraint propagation | Simple multiplication |

---

## Current State

### Implemented Systems

| System | Status | Location |
|--------|--------|----------|
| **Biome System** | ✅ Complete | `Scripts/Features/Worldgen/Biomes/` |
| **Auto-Tiling** | ✅ Complete | `Scripts/Features/Worldgen/AutoTiling/` |
| **Weight Modifiers** | ✅ Complete | `Scripts/Features/Worldgen/WeightModifiers/` |
| **Map Generator** | ✅ Complete | `Scripts/Features/Deckbuilder/Services/SimpleMapGenerator.cs` |
| **Structures** | ✅ Complete | `Scripts/Features/Worldgen/Structures/` |
| **Tile Variants** | ✅ Complete | `Scripts/Features/Worldgen/VariantModifiers/` |

### Biome System

**Files:**
- `BiomeDefinition.cs` - Runtime biome model with signature and tile pools
- `BiomeData.cs` - JSON deserialization model for biome data from tiles.json
- `BiomeRegistry.cs` - Registry that loads biomes from tiles.json (data-driven)
- `BiomeMapGenerator.cs` - Implements `IBiomeProvider`, maps gradient positions to biomes
- `TilePool.cs` - Weighted random tile selection from pool
- `IBiomeProvider.cs` - Interface for biome/signature queries

**10 Biomes** (loaded from `Data/Tiles/tiles.json`):
- Plains, Forest, Desert, Tundra, Swamp, Mountains (original)
- Water, Cave, Volcanic, Magical (added in refactor)

**Data-Driven Loading:**

Biomes are defined in `tiles.json` under the `"biomes"` key:

```json
{
  "biomes": {
    "plains": {
      "displayName": "Plains",
      "signature": [0, 0, 0.2, 0.1, 0, 0, 0.2, 0],
      "blockedPercentage": 0.15,
      "passableTiles": { "grass": 0.40, "tall_grass": 0.25 },
      "blockedTiles": { "small_rock": 0.40, "boulder": 0.30 }
    }
  }
}
```

**How biome selection works:**

```csharp
// Each biome has an 8D affinity signature
// Position signature comes from card-based gradient
// Closest signature distance wins

var positionSignature = gradient.GetSignatureAt(position);
var selectedBiome = biomes
    .OrderBy(b => positionSignature.DistanceTo(b.AffinitySignature))
    .First();
```

### Auto-Tiling System

**Files:**
- `BitmaskType.cs` - Enum specifying neighbor computation algorithm (Corner4, Edge4, Full8)
- `VariantDefinition.cs` - Immutable record for variant spatial properties (size, offset)
- `AutoTileFormatDefinition.cs` - Complete format specification with allowed bitmasks and variant mappings
- `AutoTileFormatRegistry.cs` - Thread-safe registry for format definitions
- `BuiltInAutoTileFormats.cs` - Factory methods for Corner16, Edge16, Blob47
- `NeighborBitmaskCorner.cs` - 4-bit corner bitmask utility (NE=1, SE=2, SW=4, NW=8)
- `NeighborBitmask.cs` - 4-bit edge bitmask utility (N=1, E=2, S=4, W=8)
- `NeighborBitmask8.cs` - 8-bit blob bitmask utility for Blob47 format
- `DualGridAutoTile.cs` - Dual-grid technique for terrain transitions
- `AutoTileHelper.cs` - Convenience methods for auto-tile coordinate lookup

#### Flexible Auto-Tile Format System

The auto-tile system uses a **data-driven format registry** that supports both built-in and custom formats:

```csharp
// BitmaskType determines which neighbor computation algorithm is used
enum BitmaskType {
    Corner4,  // Diagonal corners only (NE, SE, SW, NW) → 0-15
    Edge4,    // Cardinal edges only (N, E, S, W) → 0-15
    Full8     // All 8 neighbors → 0-255 (47 valid blob combinations)
}

// VariantDefinition specifies atlas location and spatial properties
record struct VariantDefinition(
    Vector2I AtlasCoords,           // Position in atlas
    Vector2I Size = (1, 1),         // Cells occupied (for multi-cell variants)
    Vector2I Offset = (0, 0),       // Anchor offset (negative Y = extends upward)
    Vector2I? AtlasRegionSize = null // Non-standard source region size
);

// AutoTileFormatDefinition is a complete format specification
class AutoTileFormatDefinition {
    string Name;                     // "corner16", "hedge4", etc.
    BitmaskType BitmaskType;         // Algorithm to use
    HashSet<int> AllowedBitmasks;    // Which values are valid (omitted = forbidden)
    Dictionary<int, VariantDefinition> VariantMappings;
    bool IsBuiltIn;                  // True for built-in formats
}
```

**Built-in Formats:**

| Format | BitmaskType | Variants | Use Case |
|--------|-------------|----------|----------|
| corner16 | Corner4 | 16 | Standard diagonal terrain transitions |
| edge16 | Edge4 | 16 | Cardinal-only patterns (fences, roads) |
| blob47 | Full8 | 47 | Smooth blob terrain with corner rules |

**Custom Format Examples:**

```json
{
  "autoTileFormats": [
    {
      "name": "hedge4",
      "bitmaskType": "edge4",
      "variants": [
        { "bitmask": 0, "atlasCoords": {"x": 0, "y": 0} },
        { "bitmask": 1, "atlasCoords": {"x": 1, "y": 0} },
        // ... bitmask 15 (interior) intentionally omitted = forbidden
        // Creates 1-tile-wide hedges that never fill in
      ]
    },
    {
      "name": "wall_south",
      "bitmaskType": "edge4",
      "variants": [
        {
          "bitmask": 4,
          "atlasCoords": {"x": 0, "y": 5},
          "size": {"x": 1, "y": 3},
          "offset": {"x": 0, "y": -2}
        }
        // 3-tile-tall wall variant extending 2 cells above anchor
      ]
    }
  ]
}
```

#### Cell Reservation for Multi-Cell Variants

Variants with `Size` > (1,1) reserve additional cells during WFC collapse:

```
Variant with Size=(1,3), Offset=(0,-2):

  ┌─────────────┐
  │  Reserved   │ Y-2 (reserved by anchor)
  ├─────────────┤
  │  Reserved   │ Y-1 (reserved by anchor)
  ├─────────────┤
  │   Anchor    │ Y   (collapsed cell, stores tile)
  └─────────────┘

WfcCellState properties:
- ReservedBy: Vector2I? → anchor position of reserving variant
- IsReserved: bool → true if reserved by another cell
- IsExcludedFromSelection() → true if collapsed OR reserved
```

**How auto-tiling works:**

Per-tile `autoTileVariants` array stores atlas coordinates indexed by bitmask:
- **Corner16**: 16 entries indexed by 4-bit corner mask (0-15)
- **Blob47**: 47 entries indexed by constrained 8-bit mask
- **Edge16**: 16 entries indexed by 4-bit cardinal mask (0-15)

```
For each tile with auto-tile variants:
  1. Compute neighbor bitmask based on format's BitmaskType
  2. Check if bitmask is allowed (format.AllowedBitmasks)
  3. Look up atlas coords in tile's autoTileVariants array
  4. If variant is multi-cell, reserve additional cells
  5. Render using variant coords with size/offset applied

Example: dirt tile with NE+SE corners → bitmask 3 → autoTileVariants[3] coords
```

### Gradient System

**Implemented Files:**
- `BaselineGradient.cs` - Abstract base class for all gradients
- `CardBasedGradient.cs` - Creates gradients from input cards (primary implementation)
- `RadialGradient` - Center-to-edge signature blending with configurable falloff
- `NoiseGradient` - FastNoiseLite-based variation around a base signature

**Gradient modes by card count:**
- 1 card: Hypersphere sampling around single signature
- 2 cards: Capsule interpolation between signatures
- 3+ cards: Bezier curve through all signature points

---

## Weight Modifier System

The weight modifier system is the core of Soft WFC - it provides the probabilistic constraint mechanism.

### Architecture

```
┌─────────────────────────────────────────────────────────────────┐
│                 WEIGHT MODIFIER PIPELINE                        │
├─────────────────────────────────────────────────────────────────┤
│  TileSelectionContext                                           │
│  ├── Position: Vector2I                                         │
│  ├── PlacedTiles: IReadOnlyDictionary<Vector2I, string>        │
│  ├── CurrentBiome: BiomeDefinition                              │
│  ├── Rng: RandomNumberGenerator                                 │
│  └── Weights: Dictionary<string, float>  ← MUTABLE              │
├─────────────────────────────────────────────────────────────────┤
│  Pipeline applies modifiers in sequence:                        │
│                                                                 │
│  weights["grass"] = 1.0                                         │
│       ↓ BiomeAffinityModifier                                   │
│  weights["grass"] = 1.5  (forest biome boosts grass)           │
│       ↓ AdjacencyBoostModifier                                  │
│  weights["grass"] = 3.375  (2 grass neighbors: 1.5^2 × 1.5)    │
│       ↓ DecorationSpacingModifier                               │
│  weights["grass"] = 3.375  (not a decoration, unchanged)       │
│       ↓ ... more modifiers ...                                  │
│  Final weight used for random selection                         │
└─────────────────────────────────────────────────────────────────┘
```

### Core Interfaces

```csharp
/// <summary>
/// Modifiers apply multiplicative adjustments to tile weights in-place.
/// </summary>
public interface IWeightModifier
{
    void ApplyModifier(TileSelectionContext context);
}
```

### Implemented Modifiers

#### BiomeAffinityModifier

Applies per-biome multipliers to tile weights. This is how "forest floor tiles are boosted in forest biomes" works.

```csharp
// Configuration example:
var modifier = new BiomeAffinityModifier()
    .WithAffinity(new BiomeTileAffinity("forest")
        .Add("forest_floor", 1.8f)   // 80% boost in forests
        .Add("grass", 1.2f)          // 20% boost
        .Add("sand", 0.3f))          // 70% reduction
    .WithAffinity(new BiomeTileAffinity("desert")
        .Add("sand", 1.5f)
        .Add("forest_floor", 0.2f)); // 80% reduction in deserts
```

#### AdjacencyBoostModifier

Boosts tiles that match already-placed neighbors, creating natural clustering.

```csharp
// Default: 1.5x per matching neighbor
// 4 matching neighbors = 1.5^4 = 5.06x boost

var modifier = new AdjacencyBoostModifier(
    getTileGroup: AdjacencyBoostModifier.StripNumericSuffix
) { BoostPerNeighbor = 1.5f };

// "grass_1" and "grass_2" both count as "grass" group
```

#### DecorationSpacingModifier

Reduces decoration probability based on proximity to other decorations.

```csharp
var modifier = new DecorationSpacingModifier(
    decorationTileIds: new HashSet<string> { "tree", "rock", "flower" }
) {
    BaseRadius = 2,      // Any decoration within 2 tiles reduces weight
    SameTypeRadius = 4,  // Same decoration type has larger exclusion
    MinWeight = 0.1f     // Never reduce below 10%
};
```

### Creating Custom Modifiers

To add new soft constraints, implement `IWeightModifier`:

```csharp
/// <summary>
/// Example: Boost water tiles near rivers, reduce near mountains.
/// </summary>
public class RiverProximityModifier : IWeightModifier
{
    private readonly HashSet<Vector2I> _riverTiles;

    public void ApplyModifier(TileSelectionContext context)
    {
        var distanceToRiver = CalculateDistance(context.Position, _riverTiles);

        var waterBoost = distanceToRiver <= 3 ? 2.0f : 1.0f;

        if (context.Weights.TryGetValue("water", out var weight))
            context.Weights["water"] = weight * waterBoost;
    }
}
```

### Pipeline Configuration

```csharp
var pipeline = new WeightModifierPipeline()
    .AddModifier(biomeAffinityModifier)
    .AddModifier(adjacencyBoostModifier)
    .AddModifier(decorationSpacingModifier);
    // Add more as needed

var selector = new WeightedTileSelector(tileRegistry, pipeline);
```

---

## Generation Pipeline

### Two-Phase WFC with Dual-Layer Terrain

The current system uses a **two-phase Wave Function Collapse** approach that separates background and foreground terrain, solving the dual-grid bitmask conflict problem through a gap constraint.

```
┌─────────────────────────────────────────────────────────────────┐
│              TWO-PHASE WFC GENERATION PIPELINE                  │
├─────────────────────────────────────────────────────────────────┤
│  Phase 1: BACKGROUND LAYER (Simple Tiles Only)                  │
│    • WFC generates ONLY non-auto-tile terrain (gap tiles)       │
│    • Biome-influenced probability via BiomeAffinityConstraint   │
│    • SpatialCoherenceConstraint encourages contiguous regions   │
│    • Creates base visual layer seen through foreground gaps     │
├─────────────────────────────────────────────────────────────────┤
│  Phase 2: FOREGROUND LAYER (Auto-Tiles with Gap Constraint)     │
│    • WFC generates ALL tiles (auto-tiles + gap tiles)           │
│    • AutoTileGapConstraint enforces 8-way gap between           │
│      different auto-tile types                                  │
│    • Non-auto-tiles become empty (show background through)      │
│    • Gap tiles enable regions of different auto-tiles           │
│      to coexist without bitmask conflicts                       │
├─────────────────────────────────────────────────────────────────┤
│  Phase 3: MERGE + AUTO-TILING                                   │
│    • Foreground takes precedence where present                  │
│    • GenerateTerrainTransitions: dual-grid bitmask computation  │
│    • Each visual tile samples 4 data corners → 4-bit bitmask    │
│    • Dominance property selects "top terrain" per visual tile   │
├─────────────────────────────────────────────────────────────────┤
│  Phase 4: VALIDATION + POST-PROCESSING                          │
│    • BitmaskConsistencyValidator checks adjacent tile agreement │
│    • RegionAnalyzer measures spatial coherence metrics          │
│    • Select per-generation tile variants                        │
│    • Place player spawn and enemies                             │
└─────────────────────────────────────────────────────────────────┘
```

### The Gap Constraint

The `AutoTileGapConstraint` is the key to eliminating dual-grid bitmask conflicts:

```
┌─────────────────────────────────────────────────────────────────┐
│                    GAP CONSTRAINT RULES                          │
├─────────────────────────────────────────────────────────────────┤
│  8-NEIGHBOR CHECK (cardinal + diagonal):                        │
│                                                                 │
│  • Auto-tile A adjacent to same Auto-tile A → ALLOWED           │
│    (regions can grow)                                           │
│                                                                 │
│  • Auto-tile A adjacent to different Auto-tile B → BANNED       │
│    (hard constraint, weight = 0.0)                              │
│                                                                 │
│  • Gap tile adjacent to any tile → ALLOWED                      │
│    (gap tiles separate auto-tile regions)                       │
├─────────────────────────────────────────────────────────────────┤
│  WHY 8-WAY CHECK:                                                │
│                                                                 │
│  Dual-grid rendering samples 4 data corners for each visual     │
│  tile. Diagonal adjacency would place two different auto-tiles  │
│  in the same 2x2 visual window, causing bitmask conflicts.      │
│                                                                 │
│  Example of what the constraint prevents:                       │
│                                                                 │
│     Data Grid:          Visual Grid:                            │
│     ┌───┬───┐           ┌─────┐                                 │
│     │ A │ B │  →  X     │ ??? │  ← Both A and B contribute      │
│     ├───┼───┤           └─────┘    to this visual tile          │
│     │ B │ A │           CONFLICT: bitmask undefined             │
│     └───┴───┘                                                   │
│                                                                 │
│  With gap constraint (G = gap tile):                            │
│     ┌───┬───┐           ┌─────┐                                 │
│     │ A │ G │  →        │  A  │  ← Only A present               │
│     ├───┼───┤           └─────┘    bitmask well-defined         │
│     │ G │ B │                                                   │
│     └───┴───┘                                                   │
└─────────────────────────────────────────────────────────────────┘
```

### Dual-Grid Auto-Tiling

Visual tiles are offset by half a cell from the data grid:

```
Data Grid (NxN):              Visual Grid ((N+1)x(N+1)):
┌───┬───┬───┐                 ┌───┬───┬───┬───┐
│D00│D01│D02│                 │V00│V01│V02│V03│
├───┼───┼───┤                 ├───┼───┼───┼───┤
│D10│D11│D12│                 │V10│V11│V12│V13│
├───┼───┼───┤                 ├───┼───┼───┼───┤
│D20│D21│D22│                 │V20│V21│V22│V23│
└───┴───┴───┘                 ├───┼───┼───┼───┤
                              │V30│V31│V32│V33│
                              └───┴───┴───┴───┘

Each visual tile V[vy,vx] samples 4 data corners:
  NW = D[vy-1, vx-1]    NE = D[vy-1, vx]
  SW = D[vy,   vx-1]    SE = D[vy,   vx]

Bitmask bits (Corner16 format):
  NE=1, SE=2, SW=4, NW=8

If corner has the "top terrain" → bit is set
```

### Spatial Coherence

The `SpatialCoherenceConstraint` encourages tiles to form contiguous regions rather than a scattered pattern:

- Boosts probability of tiles matching nearby collapsed cells
- `RegionAnalyzer` measures quality via flood-fill region detection
- Target: 70%+ of tiles in regions >= 30 tiles

Metrics logged after generation:
```
[SpatialCoherence] 15 regions found (avg size: 42.3 tiles)
[SpatialCoherence] Region sizes: min=12, max=156
[SpatialCoherence] 78.5% of tiles in regions >= 30 tiles
```

---

## Implementation Phases

### Phase 1: Biome System ✅ COMPLETE

**Goal**: Cards create meaningful biome zones on the map

**Implemented Features:**
- 6 pre-configured biomes with signature affinities
- Gradient-based biome selection via signature distance
- Per-biome passable/blocked tile pools
- Configurable blocked percentage per biome

### Phase 2: Auto-Tiling ✅ COMPLETE

**Goal**: Smooth visual transitions for terrain tiles

**Implemented Features:**
- 4-bit neighbor bitmask system (NESW)
- Three auto-tile formats:
  - **Edge16**: 4-bit cardinal format (N=1, E=2, S=4, W=8) - 16 combinations
  - **Corner16**: 4-bit diagonal format (NE=1, SE=2, SW=4, NW=8) - 16 combinations
  - **Blob47**: 8-bit format with all 8 neighbors - 47 valid combinations
- Two-pass algorithm for consistent results
- Editor UI for configuring variants

### Phase 3: Weight Modifiers ✅ COMPLETE

**Goal**: Soft constraint system for context-aware tile selection

**Implemented Features:**
- Pipeline architecture (Chain of Responsibility)
- BiomeAffinityModifier (biome-specific tile weights)
- AdjacencyBoostModifier (neighbor matching, exponential boost)
- DecorationSpacingModifier (dual-radius exclusion)
- TileSelectionContext with full position/neighbor info
- WeightedTileSelector integration

### Phase 4: Structure System ✅ COMPLETE

**Goal**: Place landmarks and points of interest with Soft WFC integration

**Implemented Features:**
- StructureStamp resource for fixed tile patterns (well, shrine, ruin)
- IProceduralStructure interface for algorithm-generated structures
- StructureProximityModifier for tile weight adjustment near structures
- StructurePlacer service orchestrates placement with spacing rules
- Integration with SimpleMapGenerator (Phase 3.5 placement)
- Biome-aware placement with AllowedBiomes constraint
- Configurable influence radius and tile affinities per structure

**Key Files:**

```
Scripts/Features/Worldgen/Structures/
├── StructureStamp.cs               # Fixed tile pattern resource
├── StructureTileEntry.cs           # Offset + tile ID entry
├── IProceduralStructure.cs         # Interface for generated structures
├── IMapQuery.cs                    # Map query interface for generators
├── StructureResult.cs              # Generation result with tiles/influence
├── StructurePlacer.cs              # Orchestrates placement

Scripts/Features/Worldgen/WeightModifiers/
└── StructureProximityModifier.cs   # Weight modifier for structure zones
```

**Usage Example:**

```csharp
// Create stamp with tile affinities
var wellStamp = new StructureStamp
{
    Id = "well",
    Size = new Vector2I(3, 3),
    AllowedBiomes = ["plains", "forest"],
    MinSpacing = 10,
    InfluenceRadius = 5,
    TileAffinities = [new TileAffinityEntry("cobblestone", 1.8f)]
};

// Add to generator
mapGenerator.AddStructureStamp(wellStamp);
```

### Phase 5: Tile Variants ✅ COMPLETE

**Goal**: Visual variety without definition explosion

**Implemented Features:**
- VariationMode.Contextual for context-aware variant selection
- IVariantWeightModifier interface (separate from tile selection modifiers)
- BiomeVariantModifier (boost variants by biome type)
- ProximityVariantModifier (boost variants near/far from specific tiles)
- VariantWeightPipeline for chaining variant modifiers
- WeightedVariantSelector for selecting variants with weighted randomness
- Integration with SimpleMapGenerator (Phase 5 post-processing)

**Key Files:**

```
Scripts/Features/Worldgen/VariantModifiers/
├── IVariantWeightModifier.cs        # Interface for variant weight modifiers
├── VariantSelectionContext.cs       # Context with position, biome, neighbors, weights
├── BiomeVariantModifier.cs          # Boost variants by biome (grass_flowers in forest)
├── ProximityVariantModifier.cs      # Boost variants by tile proximity (mossy near water)
├── VariantWeightPipeline.cs         # Chain of responsibility for variant modifiers
└── WeightedVariantSelector.cs       # Weighted random variant selection
```

**How Contextual Variants Work:**

1. Tile placed during Phase 3 with VariationMode.Contextual
2. After Phase 4 (auto-tiling), Phase 5 selects contextual variants:
   - Initialize all variant weights to 1.0
   - Apply variant modifier pipeline (biome, proximity, etc.)
   - Weighted random selection from final weights
3. Selected variant index stored in SimpleMapData.ContextualVariants

**Usage Example:**

```csharp
// Configure variant modifiers
var biomeVariants = new BiomeVariantModifier()
    .WithPreference("grass", "forest", 2.0f, 1.0f, 0.5f)  // Variant 0 boosted in forest
    .WithPreference("grass", "desert", 0.3f, 0.5f, 2.0f); // Variant 2 boosted in desert

var proximityVariants = new ProximityVariantModifier()
    .WithRule("stone", variantIndex: 1, nearTileId: "water", radius: 3, multiplier: 2.5f)  // Mossy near water
    .WithInverseRule("dirt", variantIndex: 2, farFromTileId: "water", radius: 5, multiplier: 1.8f);  // Cracked far from water

// Build pipeline
var variantPipeline = new VariantWeightPipeline()
    .AddModifier(biomeVariants)
    .AddModifier(proximityVariants);

var variantSelector = new WeightedVariantSelector(variantPipeline);

// Use in map generator
var generator = new SimpleMapGenerator(rng, biomeProvider, tileRegistry,
    variantSelector: variantSelector);
```

**Tile Definition:**

```json
{
  "id": "grass",
  "variations": [
    {"x": 0, "y": 0},  // Variant 0: Lush grass
    {"x": 1, "y": 0},  // Variant 1: Normal grass
    {"x": 2, "y": 0}   // Variant 2: Dry grass
  ],
  "variationMode": "contextual"
}
```

### Phase 6: Card Input Refactoring 🔲 PLANNED

**Goal**: Separate deck purposes for clearer gameplay

| Deck | Purpose |
|------|---------|
| **Map Deck** | Cards consumed to generate the world |
| **Loadout Deck** | Cards that configure the player (not consumed) |

**Map Deck Effects:**
- Card count → map size
- Card rarity distribution → enemy count/difficulty
- Card signatures → biome gradient (existing)

---

## Data Definition Guide

### Defining a Biome

Biomes are defined in `Data/Tiles/tiles.json` under the `"biomes"` key:

```json
{
  "biomes": {
    "forest": {
      "displayName": "Forest",
      "signature": [0.0, -0.3, -0.3, -0.3, 0.0, 0.2, 0.3, 0.0],
      "blockedPercentage": 0.35,
      "passableTiles": {
        "grass": 0.40,
        "forest_floor": 0.35,
        "forest_moss": 0.25
      },
      "blockedTiles": {
        "forest_tree": 0.50,
        "forest_dense_trees": 0.30,
        "forest_stump": 0.20
      }
    },
    "volcanic": {
      "displayName": "Volcanic",
      "signature": [0.3, 0.9, -0.4, 0.4, 0.0, 0.3, -0.6, 0.0],
      "blockedPercentage": 0.30,
      "passableTiles": {
        "volcanic_rock": 0.30,
        "volcanic_cooled_lava": 0.25,
        "volcanic_obsidian": 0.20,
        "volcanic_ash": 0.15,
        "volcanic_lava_crack": 0.10
      },
      "blockedTiles": {
        "volcanic_lava": 0.60,
        "volcanic_magma_vent": 0.40
      }
    }
  }
}
```

**Signature Array Order:**
`[Solidum, Febris, Ordinem, Lumines, Varias, Inertiae, Subsidium, Spatium]`

Each value ranges from -1.0 to 1.0. See Appendix for signature element meanings.

**Signature Affinity Reference:**

| Biome | Key Signature Traits |
|-------|---------------------|
| Plains | Neutral temperature, ordered (Ordinem+), helpful |
| Forest | Cool (Febris-), helpful (Subsidium+), organic |
| Desert | Hot (Febris+), solid (Solidum+), ordered |
| Tundra | Cold (Febris-), ordered (Ordinem+), solid |
| Swamp | Cool (Febris-), chaotic (Ordinem-), heavy (Inertiae-) |
| Mountains | Very solid (Solidum+), cool, distant |
| Water | Fluid (Solidum-), cool (Febris-), light (Inertiae-) |
| Cave | Solid (Solidum+), dark (Lumines-), dense (Inertiae+) |
| Volcanic | Hot (Febris++), chaotic (Ordinem-), harmful (Subsidium-) |
| Magical | Chaotic (Ordinem-), bright (Lumines+), spatial (Varias+) |

### Defining Weight Modifiers

**BiomeTileAffinity:**

```csharp
// In code or as resource:
new BiomeTileAffinity("forest")
    .Add("forest_floor", 1.8f)  // 80% boost
    .Add("moss", 1.5f)          // 50% boost
    .Add("grass", 1.2f)         // 20% boost
    .Add("sand", 0.2f)          // 80% reduction
    .Add("stone", 0.5f);        // 50% reduction
```

**Rule of thumb for multipliers:**
- `2.0+` = Strong preference (tile very likely)
- `1.2-1.5` = Mild preference
- `1.0` = Neutral (no effect)
- `0.5-0.8` = Mild avoidance
- `0.1-0.3` = Strong avoidance (but never zero!)

### Defining Auto-Tile Configurations

#### Custom Auto-Tile Formats

Define custom formats in `tiles.json` under `autoTileFormats`:

```json
{
  "autoTileFormats": [
    {
      "name": "hedge4",
      "bitmaskType": "edge4",
      "variants": [
        { "bitmask": 0, "atlasCoords": {"x": 0, "y": 0} },
        { "bitmask": 1, "atlasCoords": {"x": 1, "y": 0} },
        { "bitmask": 2, "atlasCoords": {"x": 2, "y": 0} },
        { "bitmask": 3, "atlasCoords": {"x": 3, "y": 0} },
        { "bitmask": 4, "atlasCoords": {"x": 4, "y": 0} },
        { "bitmask": 5, "atlasCoords": {"x": 5, "y": 0} },
        { "bitmask": 6, "atlasCoords": {"x": 6, "y": 0} },
        { "bitmask": 7, "atlasCoords": {"x": 7, "y": 0} },
        { "bitmask": 8, "atlasCoords": {"x": 8, "y": 0} },
        { "bitmask": 9, "atlasCoords": {"x": 9, "y": 0} },
        { "bitmask": 10, "atlasCoords": {"x": 10, "y": 0} },
        { "bitmask": 11, "atlasCoords": {"x": 11, "y": 0} },
        { "bitmask": 12, "atlasCoords": {"x": 12, "y": 0} },
        { "bitmask": 13, "atlasCoords": {"x": 13, "y": 0} },
        { "bitmask": 14, "atlasCoords": {"x": 14, "y": 0} }
      ]
    }
  ]
}
```

**Format Properties:**

| Property | Type | Required | Description |
|----------|------|----------|-------------|
| `name` | string | Yes | Unique format name (case-insensitive) |
| `bitmaskType` | string | No | `"corner4"`, `"edge4"`, or `"full8"` (default: corner4) |
| `variants` | array | Yes | Variant definitions for each bitmask |

**Variant Properties:**

| Property | Type | Required | Description |
|----------|------|----------|-------------|
| `bitmask` | int | Yes | Bitmask value this variant handles |
| `atlasCoords` | {x, y} | Yes | Atlas position for this variant |
| `size` | {x, y} | No | Cells occupied (default: 1x1) |
| `offset` | {x, y} | No | Anchor offset (default: 0,0) |
| `atlasRegionSize` | {x, y} | No | Non-standard source region |

**BitmaskType Values:**

| Type | Neighbors Checked | Max Bitmask | Use Case |
|------|-------------------|-------------|----------|
| `corner4` | NE, SE, SW, NW (diagonal) | 15 | Standard terrain transitions |
| `edge4` | N, E, S, W (cardinal) | 15 | Fences, roads, pipes |
| `full8` | All 8 directions | 255 | Blob terrain (47 valid) |

**Bitmask Reference (Edge4 - NESW):**

| Bitmask | Neighbors | Typical Use |
|---------|-----------|-------------|
| 0 | None | Isolated single tile |
| 1 | N | South edge |
| 2 | E | West edge |
| 3 | N+E | SW inside corner |
| 4 | S | North edge |
| 5 | N+S | Horizontal corridor |
| 6 | E+S | NW inside corner |
| 7 | N+E+S | West edge (peninsula) |
| 8 | W | East edge |
| 9 | N+W | SE inside corner |
| 10 | E+W | Vertical corridor |
| 11 | N+E+W | South edge (peninsula) |
| 12 | S+W | NE inside corner |
| 13 | N+S+W | East edge (peninsula) |
| 14 | E+S+W | North edge (peninsula) |
| 15 | All | Interior (fully surrounded) |

**Bitmask Reference (Corner4 - Diagonal):**

| Bitmask | Neighbors | Description |
|---------|-----------|-------------|
| 0 | None | Isolated |
| 1 | NE | NE corner only |
| 2 | SE | SE corner only |
| 3 | NE+SE | East edge |
| ... | ... | ... |
| 15 | All | Interior (all corners) |

**Configuration via Tile Editor UI:**

1. Open Tile Editor dock → "Formats" tab
2. Create new format with name and bitmask type
3. Configure allowed bitmasks in the grid
4. Set variant atlas coords, size, and offset
5. Use format in tile definitions via `autoTileFormat` property

### Defining Stamps (Fixed Structures)

```
Resources/Structures/Stamps/
├── well.tres
├── shrine.tres
└── signpost.tres
```

**StructureStamp Properties:**

```csharp
Id: "well"
Size: Vector2I(3, 3)

Tiles:
  - Offset: Vector2I(0, 0), TileId: "well_nw"
  - Offset: Vector2I(1, 0), TileId: "well_n"
  - Offset: Vector2I(2, 0), TileId: "well_ne"
  // ... remaining tiles

AllowedBiomes: ["plains", "forest"]
SpawnWeight: 0.5
MinSpacing: 10  // Tiles away from other structures

// Zone of influence for weight modifiers:
InfluenceRadius: 5
TileAffinities:
  - TileId: "cobblestone", Multiplier: 1.8f
  - TileId: "grass", Multiplier: 0.6f
```

---

## Technical Reference

### Key Interfaces

```csharp
/// <summary>
/// Provides biome information at any map position.
/// </summary>
public interface IBiomeProvider
{
    BiomeDefinition GetBiomeAt(Vector2I position);
    CardSignature GetSignatureAt(Vector2I position);
}

/// <summary>
/// Weight modification rules for tile selection.
/// Implementations should multiply weights, never replace.
/// </summary>
public interface IWeightModifier
{
    void ApplyModifier(TileSelectionContext context);
}

/// <summary>
/// Generates procedural structures.
/// </summary>
public interface IProceduralStructure
{
    string Id { get; }
    StructureResult Generate(
        Vector2I position,
        int seed,
        BiomeDefinition biome,
        IMapQuery mapQuery);
}
```

Irregular Map reaches WFC only through `IWfcTerrainSolver`, the front door declared in `Scripts/Features/Worldgen/Wfc/`, and `IrregularMeshWfcSeamTest` fails when a file under `Scripts/Features/Worldgen/IrregularMesh/` names any other type declared there.

### Weighted Random Selection Algorithm

```csharp
// After pipeline applies all modifiers:
public static string? SelectWeighted(Dictionary<string, float> weights, RandomNumberGenerator rng)
{
    // 1. Sum positive weights
    var totalWeight = weights.Values.Where(w => w > 0).Sum();
    if (totalWeight <= 0) return null;

    // 2. Roll random value in [0, totalWeight)
    var roll = rng.Randf() * totalWeight;

    // 3. Find tile where cumulative weight exceeds roll
    var cumulative = 0f;
    foreach (var (tileId, weight) in weights)
    {
        if (weight <= 0) continue;
        cumulative += weight;
        if (roll <= cumulative) return tileId;
    }

    return null;
}
```

### File Organization

```
Scripts/Features/Worldgen/
├── Biomes/                          # ✅ IMPLEMENTED
│   ├── BiomeData.cs                 # JSON deserialization model
│   ├── BiomeDefinition.cs           # Runtime biome model
│   ├── BiomeRegistry.cs             # Data-driven biome loading
│   ├── BiomeMapGenerator.cs
│   ├── IBiomeProvider.cs
│   ├── TilePool.cs
│   └── BiomeDistributionCalculator.cs
├── AutoTiling/                      # ✅ IMPLEMENTED
│   ├── BitmaskType.cs               # Enum: Corner4, Edge4, Full8
│   ├── VariantDefinition.cs         # Immutable record for variant properties
│   ├── AutoTileFormatDefinition.cs  # Complete format specification
│   ├── AutoTileFormatRegistry.cs    # Thread-safe format registry
│   ├── BuiltInAutoTileFormats.cs    # Factory for Corner16, Edge16, Blob47
│   ├── AutoTileHelper.cs            # Bitmask computation and variant lookup
│   ├── BitmaskConsistencyValidator.cs # Validates dual-grid bitmask agreement
│   ├── DualGridAutoTile.cs          # Dual-grid visual tile computation
│   ├── NeighborBitmask.cs           # Edge16 (NESW cardinal)
│   ├── NeighborBitmaskCorner.cs     # Corner16 (diagonal)
│   └── NeighborBitmask8.cs          # Blob47 (8-direction)
├── WeightModifiers/                 # ✅ IMPLEMENTED
│   ├── IWeightModifier.cs
│   ├── WeightModifierPipeline.cs
│   ├── WeightedTileSelector.cs
│   ├── TileSelectionContext.cs
│   ├── BiomeAffinityModifier.cs
│   ├── AdjacencyBoostModifier.cs
│   ├── DecorationSpacingModifier.cs
│   ├── StructureProximityModifier.cs
│   ├── BiomeTileAffinity.cs
│   └── TileAffinityEntry.cs
├── BaselineGradient.cs              # ✅ IMPLEMENTED (includes RadialGradient, NoiseGradient)
├── CardBasedGradient.cs             # ✅ IMPLEMENTED
├── TilePlacement.cs                 # ✅ IMPLEMENTED
├── Structures/                      # ✅ IMPLEMENTED
│   ├── StructureStamp.cs
│   ├── StructureTileEntry.cs
│   ├── IProceduralStructure.cs
│   ├── IMapQuery.cs
│   ├── StructureResult.cs
│   └── StructurePlacer.cs
├── VariantModifiers/                # ✅ IMPLEMENTED
│   ├── IVariantWeightModifier.cs
│   ├── VariantSelectionContext.cs
│   ├── BiomeVariantModifier.cs
│   ├── ProximityVariantModifier.cs
│   ├── VariantWeightPipeline.cs
│   └── WeightedVariantSelector.cs
└── Wfc/                             # ✅ IMPLEMENTED (Two-Phase WFC)
    ├── WfcMapGenerator.cs           # High-level WFC orchestrator
    ├── WfcSolver.cs                 # Core WFC collapse algorithm
    ├── WfcGrid.cs                   # Grid of superposition cells
    ├── WfcPropagator.cs             # Constraint propagation
    ├── WfcTileSelector.cs           # Weighted tile selection with constraints
    ├── WfcAdjacencyRules.cs         # Hard adjacency constraint rules
    ├── RegionAnalyzer.cs            # Spatial coherence measurement
    ├── Constraints/                 # Soft probability modifiers
    │   ├── IWfcConstraint.cs
    │   ├── AutoTileGapConstraint.cs # 8-way gap between different auto-tiles
    │   ├── SpatialCoherenceConstraint.cs # Encourages contiguous regions
    │   ├── BiomeAffinityConstraint.cs    # Biome-based tile probability
    │   └── ConnectivityConstraint.cs     # Passable tile reachability
    ├── Connectivity/                # Passability graph for reachability
    │   └── PassabilityGraph.cs
    └── Modifiers/Soft/              # Soft weight adjustment modifiers
        ├── DiminishingReturnsSoftModifier.cs
        ├── NoveltySoftModifier.cs
        └── CompactnessSoftModifier.cs

Scripts/Features/Deckbuilder/Services/
└── SimpleMapGenerator.cs            # ✅ IMPLEMENTED (Two-Phase WFC pipeline)

addons/tile_editor/
├── BiomePoolPanel.cs                # Data-driven biome pool editing
├── TileBrowserPanel.cs              # Tile list with duplicate/delete buttons
├── TilePropertiesPanel.cs
├── TileAtlasPanel.cs
├── TileEditorDock.cs
└── TileEditorService.cs

Data/Tiles/
└── tiles.json                       # Tile + biome data (data-driven config)
```

---

## Appendix: Signature Element Reference

| Index | Name | Range | Low Value | High Value |
|-------|------|-------|-----------|------------|
| 0 | Solidum | -1 to 1 | Air/void | Rock/solid |
| 1 | Febris | -1 to 1 | Cold/water | Hot/fire |
| 2 | Ordinem | -1 to 1 | Entropy/chaos | Order/structure |
| 3 | Lumines | -1 to 1 | Dark | Light |
| 4 | Varias | -1 to 1 | Time-focused | Space-focused |
| 5 | Inertiae | -1 to 1 | Heavy/dense | Light/airy |
| 6 | Subsidium | -1 to 1 | Harmful | Helpful |
| 7 | Spatium | -1 to 1 | Nearby/close | Distant/far |

## Appendix: Biome Signature Values

| Biome | Sol | Feb | Ord | Lum | Var | Ine | Sub | Spa |
|-------|-----|-----|-----|-----|-----|-----|-----|-----|
| Plains | 0.0 | 0.0 | 0.2 | 0.1 | 0.0 | 0.0 | 0.2 | 0.0 |
| Forest | 0.0 | -0.3 | -0.3 | -0.3 | 0.0 | 0.2 | 0.3 | 0.0 |
| Desert | 0.3 | 0.7 | 0.3 | 0.4 | 0.0 | -0.2 | -0.2 | 0.3 |
| Tundra | 0.2 | -0.7 | 0.5 | 0.3 | 0.0 | 0.1 | 0.0 | -0.1 |
| Swamp | -0.2 | -0.2 | -0.5 | -0.5 | 0.0 | -0.3 | -0.1 | 0.0 |
| Mountains | 0.7 | -0.2 | 0.4 | 0.2 | 0.0 | 0.4 | 0.0 | 0.3 |
| Water | -0.8 | -0.3 | 0.2 | 0.3 | 0.0 | -0.5 | 0.1 | 0.0 |
| Cave | 0.6 | -0.1 | -0.2 | -0.7 | 0.0 | 0.5 | 0.0 | -0.3 |
| Volcanic | 0.3 | 0.9 | -0.4 | 0.4 | 0.0 | 0.3 | -0.6 | 0.0 |
| Magical | -0.2 | 0.1 | -0.6 | 0.5 | 0.5 | -0.3 | 0.3 | 0.4 |

---

*Document Version: 6.0*
*Last Updated: 2026-01-07*
*Status: Flexible auto-tile format system with data-driven definitions and multi-cell variant support*
