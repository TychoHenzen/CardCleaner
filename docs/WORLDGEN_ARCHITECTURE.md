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
| **Terrain Blobs** | ✅ Complete | `Scripts/Features/Worldgen/BlobGeneration/` |
| **Map Generator** | ✅ Complete | `Scripts/Features/Deckbuilder/Services/SimpleMapGenerator.cs` |
| **Structures** | ✅ Complete | `Scripts/Features/Worldgen/Structures/` |
| **Tile Variants** | ✅ Complete | `Scripts/Features/Worldgen/VariantModifiers/` |

### Biome System

**Files:**
- `BiomeDefinition.cs` - Resource defining biome properties
- `BiomeRegistry.cs` - Registry with 6 pre-configured biomes (Plains, Forest, Desert, Tundra, Swamp, Mountains)
- `BiomeMapGenerator.cs` - Implements `IBiomeProvider`, maps gradient positions to biomes
- `TilePool.cs` - Weighted random tile selection from pool
- `IBiomeProvider.cs` - Interface for biome/signature queries

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
- `AutoTileFormat.cs` - Format enumeration (Corner16, Edge16, Blob47)
- `NeighborBitmaskCorner.cs` - 4-bit corner bitmask utility (NE=1, SE=2, SW=4, NW=8)
- `NeighborBitmask8.cs` - 8-bit blob bitmask utility for Blob47 format
- `DualGridAutoTile.cs` - Dual-grid technique for terrain transitions
- `AutoTileHelper.cs` - Convenience methods for auto-tile coordinate lookup

**How auto-tiling works:**

Per-tile `autoTileVariants` array stores atlas coordinates indexed by bitmask:
- **Corner16**: 16 entries indexed by 4-bit corner mask (0-15)
- **Blob47**: 47 entries indexed by constrained 8-bit mask
- **Edge16**: 16 entries indexed by 4-bit cardinal mask (0-15)

```
For each tile with auto-tile variants:
  1. Compute neighbor bitmask based on format
  2. Look up atlas coords in tile's autoTileVariants array
  3. Render using variant coords (or base coords if null)

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
    .WithAffinity(new BiomeTileAffinity(BiomeType.Forest)
        .Add("forest_floor", 1.8f)   // 80% boost in forests
        .Add("grass", 1.2f)          // 20% boost
        .Add("sand", 0.3f))          // 70% reduction
    .WithAffinity(new BiomeTileAffinity(BiomeType.Desert)
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

### Current 5-Phase Pipeline

```
┌─────────────────────────────────────────────────────────────────┐
│                    GENERATION PIPELINE                          │
├─────────────────────────────────────────────────────────────────┤
│  Phase 1: BIOME-BASED PASSABILITY                               │
│    • Query biome at each position via gradient                  │
│    • Randomly assign passable/blocked based on BlockedPercentage│
│    • Output: biomeMap[y,x] and isPassable[y,x]                 │
├─────────────────────────────────────────────────────────────────┤
│  Phase 2: CELLULAR AUTOMATA SMOOTHING                           │
│    • Count passable neighbors (4-directional)                   │
│    • Apply threshold: passable if N >= SmoothingThreshold       │
│    • Respects biome boundaries (different biome = blocked)      │
│    • Creates larger contiguous regions                          │
├─────────────────────────────────────────────────────────────────┤
│  Phase 3: TILE PLACEMENT                                        │
│    • For each position, select tile using Soft WFC:             │
│      1. WeightedTileSelector with modifier pipeline             │
│      2. Fallback: TerrainBlobGenerator (noise clustering)       │
│      3. Fallback: biome.SelectPassableTile()                    │
│    • Handle multi-tile placements with space validation         │
├─────────────────────────────────────────────────────────────────┤
│  Phase 4: CONNECTIVITY GUARANTEE                                │
│    • Flood-fill to identify connected components                │
│    • Create corridors between disconnected regions              │
│    • Ensure single connected component                          │
├─────────────────────────────────────────────────────────────────┤
│  Phase 5: POST-PROCESSING                                       │
│    • Generate terrain transition overlays (edge decorations)    │
│    • Apply auto-tiling (select edge variants)                   │
│    • Select per-generation tile variants                        │
│    • Place player spawn and enemies                             │
└─────────────────────────────────────────────────────────────────┘
```

### Future: Entropy-Based Cell Selection

The current implementation processes cells sequentially. A future enhancement would use WFC-style entropy-based ordering:

```
Instead of: for y in 0..height, for x in 0..width: place_tile(x, y)

Use: while uncollapsed_cells remain:
       1. Find cell with lowest entropy (fewest high-probability options)
       2. Collapse that cell (weighted random selection)
       3. Recompute neighbor entropies
       4. Repeat
```

This would create more coherent patterns because high-certainty cells (cells where the context strongly suggests one tile) are placed first, propagating their influence outward.

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
    AllowedBiomes = [BiomeType.Plains, BiomeType.Forest],
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
    .WithPreference("grass", BiomeType.Forest, 2.0f, 1.0f, 0.5f)  // Variant 0 boosted in forest
    .WithPreference("grass", BiomeType.Desert, 0.3f, 0.5f, 2.0f); // Variant 2 boosted in desert

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

Create `BiomeDefinition` resources:

```
Resources/Biomes/
├── forest.tres
├── desert.tres
├── swamp.tres
└── plains.tres
```

**Properties:**

```csharp
BiomeType: Forest

AffinitySignature:
  Solidum: 0.0      // Neutral solidity
  Febris: -0.3      // Slightly cool
  Ordinem: -0.2     // Slightly chaotic
  Lumines: -0.1     // Slightly dark
  Varias: 0.0       // Neutral
  Inertiae: 0.2     // Slightly light/airy
  Subsidium: 0.3    // Helpful
  Spatium: 0.0      // Neutral distance

PassableTiles:
  - TileId: "grass", Weight: 0.6
  - TileId: "dirt", Weight: 0.3
  - TileId: "forest_floor", Weight: 0.1

BlockedTiles:
  - TileId: "tree_trunk", Weight: 0.7
  - TileId: "bush", Weight: 0.2
  - TileId: "rock", Weight: 0.1

BlockedPercentage: 0.35
```

**Signature Affinity Reference:**

| Biome | Key Signature Traits |
|-------|---------------------|
| Forest | Cool (Febris-), helpful (Subsidium+), organic |
| Desert | Hot (Febris+), solid (Solidum+), ordered |
| Swamp | Cool (Febris-), chaotic (Ordinem-), heavy (Inertiae-) |
| Plains | Neutral temperature, ordered (Ordinem+), light |

### Defining Weight Modifiers

**BiomeTileAffinity:**

```csharp
// In code or as resource:
new BiomeTileAffinity(BiomeType.Forest)
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

**Bitmask Reference (NESW):**

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

**Configuration via Tile Editor UI:**

1. Open Tile Editor dock → "Auto-Tiling" tab
2. Create new config for a base terrain tile
3. Assign variant tiles to each bitmask slot
4. Leave slots empty to use base tile

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

AllowedBiomes: [Plains, Forest]
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
│   ├── BiomeDefinition.cs
│   ├── BiomeRegistry.cs
│   ├── BiomeMapGenerator.cs
│   ├── IBiomeProvider.cs
│   ├── TilePool.cs
│   └── BiomeDistributionCalculator.cs
├── AutoTiling/                      # ✅ IMPLEMENTED
│   ├── AutoTileFormat.cs
│   ├── AutoTileHelper.cs
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
├── BlobGeneration/                  # ✅ IMPLEMENTED
│   └── TerrainBlobGenerator.cs
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
└── VariantModifiers/                # ✅ IMPLEMENTED
    ├── IVariantWeightModifier.cs
    ├── VariantSelectionContext.cs
    ├── BiomeVariantModifier.cs
    ├── ProximityVariantModifier.cs
    ├── VariantWeightPipeline.cs
    └── WeightedVariantSelector.cs

Scripts/Features/Deckbuilder/Services/
└── SimpleMapGenerator.cs            # ✅ IMPLEMENTED (5-phase pipeline)

addons/tile_editor/
├── BlobSettingsPanel.cs
├── BiomePoolPanel.cs
├── TilePropertiesPanel.cs
├── TileAtlasPanel.cs
├── TileEditorDock.cs
└── TileEditorService.cs

Data/Tiles/
└── tiles.json                       # All tile data + auto-tile configs
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

---

*Document Version: 3.0*
*Last Updated: 2025-12-30*
*Status: Soft WFC architecture complete - Phases 1-5 implemented*
