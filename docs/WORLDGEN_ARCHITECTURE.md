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
| **Structures** | 🔲 Planned | Phase 3 |
| **Tile Variants** | 🔲 Planned | Phase 4 |

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
- `AutoTileResolver.cs` - Two-pass algorithm (compute then apply)
- `NeighborBitmask.cs` - 4-bit NESW bitmask utility (N=1, E=2, S=4, W=8)
- `AutoTileConfig.cs` - Resource mapping base tile → 16 edge variants
- `AutoTileFormat.cs` - Format enumeration (Corner16, Blob47)

**How auto-tiling works:**

```
For each tile:
  1. Compute 4-bit neighbor bitmask (same-type neighbors)
  2. Look up edge variant in AutoTileConfig
  3. Replace with variant (or keep base if no variant defined)

Example: grass tile with N+E neighbors → bitmask 3 → grass_ne variant
```

### Gradient System

**Implemented Files:**
- `BaselineGradient.cs` - Abstract base class for all gradients
- `CardBasedGradient.cs` - Creates gradients from input cards (primary implementation)

**Planned Gradient Types:**
- RadialGradient - Center-to-edge blending
- NoiseGradient - FastNoiseLite-based variation

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
- 16-variant edge tile support (Corner16 format)
- Two-pass algorithm for consistent results
- Editor UI for configuring variants
- Blob47 format support planned

### Phase 3: Weight Modifiers ✅ COMPLETE

**Goal**: Soft constraint system for context-aware tile selection

**Implemented Features:**
- Pipeline architecture (Chain of Responsibility)
- BiomeAffinityModifier (biome-specific tile weights)
- AdjacencyBoostModifier (neighbor matching, exponential boost)
- DecorationSpacingModifier (dual-radius exclusion)
- TileSelectionContext with full position/neighbor info
- WeightedTileSelector integration

### Phase 4: Structure System 🔲 PLANNED

**Goal**: Place landmarks and points of interest with Soft WFC integration

**Planned Approach:**

Structures will integrate with the weight modifier system:

```csharp
public class StructureProximityModifier : IWeightModifier
{
    public void ApplyModifier(TileSelectionContext context)
    {
        // Near shrine? Boost "sacred_stone" tiles
        // Near forge? Boost "ash", "coal" tiles
        // Inside building footprint? Use structure-specific tiles
    }
}
```

**Concepts:**

| Term | Definition |
|------|------------|
| **Stamp** | Fixed tile arrangement (well, shrine, small ruin) |
| **Procedural Structure** | Algorithm-generated (building, cave, forest cluster) |
| **Structure Zone** | Area around structure with modified tile weights |

**Planned Files:**

```
Scripts/Features/Worldgen/Structures/
├── StructureStamp.cs               # Fixed tile pattern resource
├── IProceduralStructure.cs         # Interface for generated structures
├── StructurePlacementRule.cs       # Biome/spacing requirements
├── StructurePlacer.cs              # Orchestrates placement
├── StructureProximityModifier.cs   # Weight modifier for structure zones
└── Implementations/
    ├── ForestClusterGenerator.cs
    ├── BuildingGenerator.cs
    └── CaveEntranceGenerator.cs
```

### Phase 5: Tile Variants 🔲 PLANNED

**Goal**: Visual variety without definition explosion

**Planned Approach:**

Tile variants as weight modifier source:

```csharp
public class TileVariantModifier : IWeightModifier
{
    // Boost specific variants based on context:
    // - "grass_flowers" more likely in spring/fertile areas
    // - "stone_mossy" more likely near water
    // - "dirt_cracked" more likely in dry biomes
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
│   ├── AutoTileResolver.cs
│   ├── NeighborBitmask.cs
│   ├── AutoTileConfig.cs
│   └── AutoTileFormat.cs
├── WeightModifiers/                 # ✅ IMPLEMENTED
│   ├── IWeightModifier.cs
│   ├── WeightModifierPipeline.cs
│   ├── WeightedTileSelector.cs
│   ├── TileSelectionContext.cs
│   ├── BiomeAffinityModifier.cs
│   ├── AdjacencyBoostModifier.cs
│   ├── DecorationSpacingModifier.cs
│   ├── BiomeTileAffinity.cs
│   └── TileAffinityEntry.cs
├── BlobGeneration/                  # ✅ IMPLEMENTED
│   └── TerrainBlobGenerator.cs
├── BaselineGradient.cs              # ✅ IMPLEMENTED
├── CardBasedGradient.cs             # ✅ IMPLEMENTED
├── Structures/                      # 🔲 PLANNED
│   ├── StructureStamp.cs
│   ├── IProceduralStructure.cs
│   ├── StructurePlacementRule.cs
│   ├── StructurePlacer.cs
│   └── StructureProximityModifier.cs
└── TileVariantPool.cs               # 🔲 PLANNED

Scripts/Features/Deckbuilder/Services/
└── SimpleMapGenerator.cs            # ✅ IMPLEMENTED (5-phase pipeline)

addons/tile_editor/
├── AutoTileConfigPanel.cs           # ✅ IMPLEMENTED
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

*Document Version: 2.0*
*Last Updated: 2025-12-29*
*Status: Soft WFC architecture established - Phases 1-3 complete*
