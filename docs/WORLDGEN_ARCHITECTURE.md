# World Generation Architecture

This document describes the target architecture for CardCleaner's procedural world generation system, including implementation phases and data definition guides.

## Table of Contents

1. [Current State](#current-state)
2. [Target Architecture](#target-architecture)
3. [Implementation Phases](#implementation-phases)
4. [Data Definition Guide](#data-definition-guide)
5. [Technical Reference](#technical-reference)

---

## Current State

### Two Parallel Systems

**SemanticWfc3dGenerator** (`Scripts/Features/Worldgen/SemanticWfc3dGenerator.cs`)
- Full 3D Wave Function Collapse implementation (~590 lines)
- 4 vertical layers: Terrain → Decoration → Structure → Effects
- 10-directional socket compatibility system (N/E/S/W, diagonals, Up/Down)
- Cross-layer constraints via `LayerConstraint`
- Gradient influence on tile weights via `GradientInfluenceComponent`
- **Status**: Complex, difficult to extend, socket management became overwhelming

**SimpleMapGenerator** (`Scripts/Features/Deckbuilder/Services/SimpleMapGenerator.cs`)
- Lightweight random placement (~270 lines)
- Basic spatial noise for terrain coherence (sin/cos waves)
- Signature-influenced tile selection
- Flood-fill connectivity guarantee
- **Status**: Fast but produces random-feeling maps without structure

### Existing Gradient System (Keep)

The gradient system is solid and will be reused:

| File | Purpose |
|------|---------|
| `BaselineGradient.cs` | Abstract base class for gradients |
| `CardBasedGradient.cs` | Creates gradients from 1-3+ cards (sphere/capsule/Bezier) |
| `RadialGradient.cs` | Center-to-edge blending |
| `NoiseGradient.cs` | FastNoiseLite-based variation |
| `GradientInfluenceComponent.cs` | Adjusts tile weights by signature similarity |

### Existing Tile System

**SemanticTile** (`Scripts/Features/Worldgen/SemanticTile.cs`)
- Godot Resource with tile metadata
- `TileLayer` assignment (Terrain/Decoration/Structure/Effects)
- `BaseWeight` for selection probability
- `CardSignature` for gradient affinity
- `SocketData` with 10-directional compatibility tags

**TileDefinition** (`Scripts/Features/Deckbuilder/Data/TileDefinition.cs`)
- Simpler tile metadata for SimpleMapGenerator
- ID, name, passability, atlas coordinates
- Used for rendering and registry lookup

---

## Target Architecture

### Design Principles

1. **Layered Pipeline**: Each stage does one thing well
2. **Data-Driven**: New content through Resources, not code
3. **Incremental Progress**: Each phase produces playable results
4. **Biome-Centric**: Biomes are the organizing principle for all content

### Generation Pipeline

```
┌─────────────────────────────────────────────────────────────────┐
│                    GENERATION PIPELINE                          │
├─────────────────────────────────────────────────────────────────┤
│  Stage 1: BIOME PLACEMENT                                       │
│    Input:  Card signatures from deck                            │
│    Output: 2D grid of BiomeType + signature values              │
│    Method: CardBasedGradient → signature → biome mapping        │
├─────────────────────────────────────────────────────────────────┤
│  Stage 2: TERRAIN GENERATION                                    │
│    Input:  Biome grid                                           │
│    Output: Base terrain tile grid                               │
│    Method: Per-biome tile palette with weighted selection       │
├─────────────────────────────────────────────────────────────────┤
│  Stage 3: TRANSITIONS                                           │
│    Input:  Terrain grid + biome grid                            │
│    Output: Transition overlay tiles                             │
│    Method: 4-bit neighbor bitmask auto-tiling                   │
├─────────────────────────────────────────────────────────────────┤
│  Stage 4: STRUCTURES                                            │
│    Input:  Terrain + biome data                                 │
│    Output: Structure tiles (buildings, forests, caves)          │
│    Method: Stamp placement + procedural generators              │
├─────────────────────────────────────────────────────────────────┤
│  Stage 5: ENTITIES                                              │
│    Input:  Complete map data                                    │
│    Output: Player spawn, enemy positions, items                 │
│    Method: Card rarity/quantity → counts, biome → types         │
└─────────────────────────────────────────────────────────────────┘
```

### What to Keep vs Replace

**Keep from WFC:**
- Layer concept (terrain, decoration, structure, effects)
- `GradientInfluenceComponent` for signature-based weighting
- SemanticTile as base for tile definitions

**Keep from Simple:**
- Connectivity guarantee algorithm (flood-fill + corridor creation)
- Signature-influenced tile selection concept
- Performance-oriented approach

**Replace/Simplify:**
- 10-directional sockets → simpler neighbor rules at biome level
- Per-tile constraint propagation → biome-level placement rules
- CompatibilityTag complexity → straightforward enum-based compatibility

---

## Implementation Phases

### Phase 1: Biome System Foundation

**Goal**: Cards create meaningful biome zones on the map

**New Files to Create:**

```
Scripts/Features/Worldgen/Biomes/
├── BiomeDefinition.cs      # Resource defining a biome
├── BiomeRegistry.cs        # Central registry of all biomes
├── BiomeMapGenerator.cs    # Converts gradient to biome grid
└── IBiomeProvider.cs       # Interface for querying biome at position
```

**BiomeDefinition Properties:**
- `BiomeType` enum identifier
- `CardSignature AffinitySignature` - which signature values favor this biome
- `TilePool PassableTiles` - weighted list of ground tiles
- `TilePool BlockedTiles` - weighted list of obstacle tiles
- `float BlockedPercentage` - density of obstacles
- `StructureRule[] AllowedStructures` - what can spawn here

**Integration Points:**
- Modify `SimpleMapGenerator` to accept `IBiomeProvider`
- Use `CardBasedGradient.GetSignatureAt()` → find closest biome by signature distance

**Acceptance Criteria:**
- [ ] Maps show distinct biome regions
- [ ] Different biomes use different tile sets
- [ ] Biome placement influenced by input cards

---

### Phase 2: Biome Transitions (Auto-tiling)

**Goal**: Smooth visual transitions between biomes

**Approach**: 4-bit neighbor bitmask

```
For each tile at biome boundary:
  Check 4 neighbors (N=1, E=2, S=4, W=8)
  Sum bits where neighbor is DIFFERENT biome
  → 16 possible edge combinations (0-15)
  → Each biome provides 16 transition sprites
```

**New Files:**

```
Scripts/Features/Worldgen/Transitions/
├── TransitionCalculator.cs     # Computes bitmask for each cell
├── TransitionTileSet.cs        # Maps bitmask → tile for a biome pair
└── TransitionRenderer.cs       # Handles overlay layer rendering
```

**Rendering Strategy:**

Option A: Two-layer rendering
```
Layer 0: Base terrain tile (full tile, e.g., dirt)
Layer 1: Transition overlay (transparent edges, e.g., grass border)
```

Option B: Pre-composed tiles
```
Single layer with 16 variants per biome-pair
(More tiles but simpler rendering)
```

**Recommendation**: Option A (two-layer) for flexibility

**Acceptance Criteria:**
- [ ] Biome edges have smooth transitions
- [ ] Grass-over-dirt style transparency works
- [ ] No visual artifacts at corners

---

### Phase 3: Structure System

**Goal**: Place landmarks and points of interest

**Concepts:**

| Term | Definition |
|------|------------|
| **Stamp** | Fixed tile arrangement (boulder, single tree, small ruin) |
| **Procedural Structure** | Algorithm-generated (building, cave, forest cluster) |
| **Placement Rule** | Constraints for where structures can appear |

**New Files:**

```
Scripts/Features/Worldgen/Structures/
├── StructureStamp.cs               # Fixed tile pattern resource
├── IProceduralStructure.cs         # Interface for generated structures
├── StructurePlacementRule.cs       # Biome/spacing/terrain requirements
├── StructurePlacer.cs              # Orchestrates structure placement
└── Implementations/
    ├── ForestGenerator.cs          # Clusters of trees
    ├── BuildingGenerator.cs        # Rectangular buildings
    └── CaveEntranceGenerator.cs    # Cave mouth with interior
```

**StructureStamp Properties:**
- `string Id`
- `TileOffset[]` - relative positions and tile IDs
- `Vector2I Size` - bounding box
- `BiomeType[] AllowedBiomes`
- `float SpawnWeight`
- `int MinSpacing` - minimum distance from other structures
- `TerrainRequirement` - e.g., "needs 3x3 passable area"

**Placement Algorithm:**
1. Divide map into placement regions
2. For each region, query allowed structures for that biome
3. Attempt placement with spacing checks
4. Write tiles to map

**Acceptance Criteria:**
- [ ] Stamps place correctly without overlapping
- [ ] Procedural structures generate valid layouts
- [ ] Structures respect biome boundaries
- [ ] Structures don't block map connectivity

---

### Phase 4: Tile Variations

**Goal**: Visual variety without definition explosion

**New File:**

```
Scripts/Features/Worldgen/TileVariantPool.cs
```

**TileVariantPool Properties:**
- `string BaseId` - logical tile ID (e.g., "grass")
- `TileVariant[] Variants` - array of visual variants
- Each variant: `TileDefinition Tile`, `float Weight`

**Usage:**
```csharp
// Instead of:
tileId = "grass";

// Use:
tileId = variantPool.SelectVariant("grass", rng);
// Returns "grass_1", "grass_2", etc. based on weights
```

**Acceptance Criteria:**
- [ ] Same logical tile can have multiple visual appearances
- [ ] Weights control variant distribution
- [ ] Backwards compatible with existing tile IDs

---

### Phase 5: Card Input Refactoring

**Goal**: Separate deck purposes for clearer gameplay

**Current State:**
- Two decks consumed as input (mapSeed + abilityCards)
- Both contribute to map generation somehow

**Target State:**

| Deck | Purpose |
|------|---------|
| **Map Deck** | Cards consumed to generate the world |
| **Loadout Deck** | Cards that configure the player (not consumed) |

**Map Deck Effects:**
- Card count → map size
- Card rarity distribution → enemy count/difficulty
- Card signatures → biome gradient (existing)

**Loadout Deck Effects:**
- Equipment cards → starting gear
- Skill cards → ability unlocks
- Behavior cards → AI modifiers

**New Files:**

```
Scripts/Features/Deckbuilder/Services/
├── MapParameterCalculator.cs    # Deck → size, enemy count, etc.
└── LoadoutApplicator.cs         # Deck → player configuration
```

**Acceptance Criteria:**
- [ ] Map deck clearly affects world generation
- [ ] Loadout deck affects player without affecting world
- [ ] UI clearly distinguishes the two purposes

---

## Data Definition Guide

This section explains how to define content for each system.

### Defining a Biome

Create a new `BiomeDefinition` resource in Godot:

```
Resources/Biomes/
├── forest.tres
├── desert.tres
├── swamp.tres
└── plains.tres
```

**Required Properties:**

```gdscript
# In Godot Inspector or .tres file:

BiomeType: Forest  # Enum value

AffinitySignature:
  Solidum: 0.0      # Neutral solidity
  Febris: -0.3      # Slightly cool
  Ordinem: -0.2     # Slightly chaotic
  Lumines: -0.1     # Slightly dark
  Varias: 0.0       # Neutral
  Inertiae: 0.2     # Slightly light/airy
  Subsidium: 0.3    # Helpful
  Spatium: 0.0      # Neutral distance

PassableTiles:
  - TileId: "grass", Weight: 0.6
  - TileId: "dirt", Weight: 0.3
  - TileId: "forest_floor", Weight: 0.1

BlockedTiles:
  - TileId: "tree_trunk", Weight: 0.7
  - TileId: "bush", Weight: 0.2
  - TileId: "rock", Weight: 0.1

BlockedPercentage: 0.35

AllowedStructures:
  - StructureId: "tree_cluster", SpawnChance: 0.3
  - StructureId: "fallen_log", SpawnChance: 0.1
  - StructureId: "forest_shrine", SpawnChance: 0.02
```

**Signature Affinity Explained:**

The biome with the smallest signature distance to the gradient at a position wins. Design signatures to create natural groupings:

| Biome | Key Signature Traits |
|-------|---------------------|
| Forest | Cool (Febris-), helpful (Subsidium+), organic |
| Desert | Hot (Febris+), solid (Solidum+), ordered |
| Swamp | Cool (Febris-), chaotic (Ordinem-), heavy (Inertiae-) |
| Plains | Neutral temperature, ordered (Ordinem+), light |

---

### Defining a Tile

Tiles are defined in the `TileRegistry` or as `TileDefinition` resources:

```
Resources/Tiles/
├── Terrain/
│   ├── grass.tres
│   ├── dirt.tres
│   └── stone.tres
├── Obstacles/
│   ├── tree.tres
│   └── rock.tres
└── Structures/
    ├── wall.tres
    └── floor.tres
```

**TileDefinition Properties:**

```gdscript
Id: "grass"
DisplayName: "Grass"
Passability: Passable  # Passable, Solid, PartiallyPassable
AtlasCoords: Vector2I(0, 0)
SourceId: 0
Layer: Terrain
IsTransparent: true
Elevation: 0
```

---

### Defining a Stamp (Fixed Structure)

Stamps are small fixed tile arrangements:

```
Resources/Structures/Stamps/
├── boulder_small.tres
├── tree_single.tres
├── well.tres
└── signpost.tres
```

**StructureStamp Properties:**

```gdscript
Id: "well"
Size: Vector2I(3, 3)

Tiles:
  # Relative positions and tile IDs
  - Offset: Vector2I(0, 0), TileId: "well_nw"
  - Offset: Vector2I(1, 0), TileId: "well_n"
  - Offset: Vector2I(2, 0), TileId: "well_ne"
  - Offset: Vector2I(0, 1), TileId: "well_w"
  - Offset: Vector2I(1, 1), TileId: "well_center"
  - Offset: Vector2I(2, 1), TileId: "well_e"
  - Offset: Vector2I(0, 2), TileId: "well_sw"
  - Offset: Vector2I(1, 2), TileId: "well_s"
  - Offset: Vector2I(2, 2), TileId: "well_se"

AllowedBiomes: [Plains, Forest]
SpawnWeight: 0.5
MinSpacing: 10  # Tiles away from other structures

Requirements:
  - Type: FlatGround
    Size: Vector2I(3, 3)
```

---

### Defining a Procedural Structure

Procedural structures are code-based generators:

```csharp
public class ForestGenerator : IProceduralStructure
{
    public string Id => "tree_cluster";

    public StructureResult Generate(
        Vector2I position,
        int seed,
        BiomeDefinition biome,
        IMapQuery mapQuery)
    {
        var result = new StructureResult();
        var rng = new RandomNumberGenerator { Seed = (ulong)seed };

        // Generate 3-7 trees in a cluster
        var treeCount = rng.RandiRange(3, 7);
        var radius = 3;

        for (int i = 0; i < treeCount; i++)
        {
            var offset = new Vector2I(
                rng.RandiRange(-radius, radius),
                rng.RandiRange(-radius, radius)
            );

            var treePos = position + offset;

            if (mapQuery.IsPassable(treePos))
            {
                result.AddTile(treePos, "tree_trunk");
            }
        }

        return result;
    }
}
```

---

### Defining Tile Variants

Group visual variants under a logical ID:

```
Resources/TileVariants/
├── grass_variants.tres
└── stone_variants.tres
```

**TileVariantPool Properties:**

```gdscript
BaseId: "grass"

Variants:
  - TileId: "grass_1", Weight: 0.4
  - TileId: "grass_2", Weight: 0.3
  - TileId: "grass_3", Weight: 0.2
  - TileId: "grass_flowers", Weight: 0.1
```

When the generator requests "grass", it randomly selects from these variants based on weights.

---

### Defining Transition Tiles

Transitions use a 4-bit bitmask system:

```
Bit layout (neighbor is DIFFERENT biome):
  N = 1, E = 2, S = 4, W = 8

Examples:
  0  = No transitions (interior tile)
  1  = North edge only
  3  = North + East (corner)
  15 = All sides different (island)
```

**TransitionTileSet Properties:**

```gdscript
FromBiome: Grass
ToBiome: Dirt  # The biome we're transitioning TO

# Map bitmask → overlay tile
Tiles:
  0:  null  # No transition needed
  1:  "grass_edge_n"
  2:  "grass_edge_e"
  3:  "grass_corner_ne"
  4:  "grass_edge_s"
  5:  "grass_edge_ns"  # Vertical strip
  6:  "grass_corner_se"
  7:  "grass_peninsula_e"
  8:  "grass_edge_w"
  9:  "grass_corner_nw"
  10: "grass_edge_ew"  # Horizontal strip
  11: "grass_peninsula_n"
  12: "grass_corner_sw"
  13: "grass_peninsula_w"
  14: "grass_peninsula_s"
  15: "grass_island"
```

---

## Technical Reference

### Key Interfaces

```csharp
/// Provides biome information at any map position
public interface IBiomeProvider
{
    BiomeDefinition GetBiomeAt(Vector2I position);
    CardSignature GetSignatureAt(Vector2I position);
}

/// Queries map state during generation
public interface IMapQuery
{
    bool IsPassable(Vector2I position);
    bool IsInBounds(Vector2I position);
    string? GetTileAt(Vector2I position);
    BiomeDefinition? GetBiomeAt(Vector2I position);
}

/// Generates procedural structures
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

### Biome Selection Algorithm

```csharp
public BiomeDefinition SelectBiome(CardSignature signature, BiomeDefinition[] biomes)
{
    BiomeDefinition? best = null;
    float bestDistance = float.MaxValue;

    foreach (var biome in biomes)
    {
        var distance = signature.DistanceTo(biome.AffinitySignature);
        if (distance < bestDistance)
        {
            bestDistance = distance;
            best = biome;
        }
    }

    return best ?? biomes[0];
}
```

### Transition Bitmask Calculation

```csharp
public int CalculateTransitionMask(Vector2I position, BiomeType myBiome, IBiomeProvider provider)
{
    int mask = 0;

    if (GetBiomeType(position + Vector2I.Up) != myBiome)    mask |= 1;  // North
    if (GetBiomeType(position + Vector2I.Right) != myBiome) mask |= 2;  // East
    if (GetBiomeType(position + Vector2I.Down) != myBiome)  mask |= 4;  // South
    if (GetBiomeType(position + Vector2I.Left) != myBiome)  mask |= 8;  // West

    return mask;
}
```

### File Locations Summary

```
Scripts/Features/Worldgen/
├── Biomes/
│   ├── BiomeDefinition.cs
│   ├── BiomeRegistry.cs
│   ├── BiomeMapGenerator.cs
│   └── IBiomeProvider.cs
├── Transitions/
│   ├── TransitionCalculator.cs
│   ├── TransitionTileSet.cs
│   └── TransitionRenderer.cs
├── Structures/
│   ├── StructureStamp.cs
│   ├── IProceduralStructure.cs
│   ├── StructurePlacementRule.cs
│   ├── StructurePlacer.cs
│   └── Implementations/
│       ├── ForestGenerator.cs
│       ├── BuildingGenerator.cs
│       └── CaveEntranceGenerator.cs
├── TileVariantPool.cs
├── Gradients/
│   └── (existing gradient files)
└── (existing WFC files - to be deprecated)

Resources/
├── Biomes/
│   ├── forest.tres
│   ├── desert.tres
│   └── ...
├── Tiles/
│   └── ...
├── Structures/
│   ├── Stamps/
│   └── Procedural/
└── TileVariants/
    └── ...
```

---

## Migration Strategy

### Phase 1 Transition

1. Create `IBiomeProvider` interface
2. Create initial `BiomeDefinition` resources for existing terrain types
3. Modify `SimpleMapGenerator` to accept optional `IBiomeProvider`
4. When provider is null, use current behavior (backwards compatible)
5. When provider exists, use biome-based tile selection

### Deprecation Path

The WFC system (`SemanticWfc3dGenerator`) will not be immediately removed. Instead:

1. Mark as `[Obsolete]` with message pointing to new system
2. Keep for reference during development
3. Remove after Phase 3 is complete and validated

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

*Document Version: 1.0*
*Last Updated: 2025-12-25*
*Status: Planning Phase - Pre-implementation*
