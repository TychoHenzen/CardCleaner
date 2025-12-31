# Tiles & Biomes Audit Report

## Executive Summary

This document provides a comprehensive audit of CardCleaner's tile and biome system, analyzing appropriateness, completeness, consistency, and providing visual design guidance.

---

## 1. Current Biome Analysis

### 1.1 Biome Set Overview

| Biome | Signature Profile | Blocked % | Thematic Purpose |
|-------|------------------|-----------|------------------|
| **Plains** | Neutral, ordered, helpful | 15% | Open grassland, beginner-friendly |
| **Forest** | Cool, chaotic, dark, helpful | 40% | Dense woodland, high obstacle density |
| **Desert** | Hot, solid, ordered, bright | 12% | Arid wasteland, sparse obstacles |
| **Tundra** | Cold, ordered, bright | 22% | Frozen landscape, moderate hazards |
| **Swamp** | Cool, chaotic, dark, heavy | 35% | Treacherous wetland, many obstacles |
| **Mountains** | Very solid, cold, ordered | 38% | Rugged terrain, high obstacle density |

### 1.2 Biome Appropriateness Assessment

**Strengths:**
- Good coverage of classic fantasy terrain types
- Clear signature differentiation (hot/cold, chaotic/ordered)
- Reasonable blocked percentages matching thematic expectations
- Signature profiles align well with the 8D card signature system

**Gaps Identified:**

1. **No Water/Ocean Biome** - Water tiles only exist as decoration (swamp_shallow_water), not as a distinct biome
2. **No Cave/Underground Biome** - Mountains have cave_entrance but no interior cave system
3. **No Volcanic/Lava Biome** - Would use extreme Febris+ signature (hot + harmful)
4. **No Magical/Corrupted Biome** - Could use Ordinem- (chaotic) + Subsidium- (harmful) for corrupted zones

**Recommendation Priority:**
- P1: Consider adding water biome tiles for lakes/rivers within other biomes
- P2: Cave interior could be a sub-biome or structure type rather than full biome
- P3: Volcanic/magical biomes are nice-to-have for variety

---

## 2. Tile Definition Audit

### 2.1 Tiles Per Biome Summary

| Biome | Passable Tiles | Blocked Tiles | Total | Issues |
|-------|----------------|---------------|-------|--------|
| Plains | 6 | 3 | 9 | Missing shrub from JSON |
| Forest | 5 | 6 | 11 | Good variety |
| Desert | 6 | 5 | 11 | Good variety |
| Tundra | 5 | 5 | 10 | Good variety |
| Swamp | 5 | 5 | 10 | Good variety |
| Mountains | 5 | 5 | 10 | Good variety |
| Universal | 5 | 2 | 7 | Debug + generic tiles |

### 2.2 Consistency Issues Found

#### Issue 1: BiomeRegistry references non-existent tile
- `plains_shrub` is in BiomeRegistry blocked pool but exists in tiles.json
- **Status**: Actually exists - false alarm, tile is defined at line 265-279

#### Issue 2: Layer inconsistencies
Some tiles have questionable layer assignments:
- `swamp_reeds` - layer "terrain" but should arguably be "decoration"
- `tundra_frost` - layer "decoration" but is a ground cover (terrain)
- `swamp_shallow_water` - layer "terrain" but depth suggests it could be "effects"

#### Issue 3: Passability vs Elevation mismatches
- `forest_mushrooms` - passable=solid but elevation=0.2 (very low, could be passable)
- `swamp_lily_pads` - passable=solid but floating on water (could be passable)
- `desert_bones` - passable=solid but just decoration (could be passable)

### 2.3 Auto-tile Coverage

| Tile | Auto-tile Format | Variants | Status |
|------|-----------------|----------|--------|
| dirt | Corner16 | 16 | ✅ Complete |
| forest_clearing | Corner16 | 16 | ✅ Complete |
| forest_dense_trees | Corner16 | 16 | ✅ Complete |
| desert_sand | Corner16 | 16 | ✅ Complete |
| desert_dune | Corner16 | 16 | ✅ Complete |
| desert_cracked | Corner16 | 16 | ✅ Complete |
| desert_gravel | Corner16 | 16 | ✅ Complete |
| desert_rocky | Blob47 | 47 | ✅ Complete |

**Missing Auto-tile Candidates:**
- `tundra_snow` - Primary snow terrain should have edge transitions
- `swamp_mud` - Would benefit from edge blending
- `mountains_rock` - Could use cliff edge variants
- `plains_grass` - Main terrain tile should auto-tile

---

## 3. Tile Visual Design Guide

### 3.1 What is "Scree"?

**Definition**: Scree (also called talus) is a collection of broken rock fragments that accumulate at the base of crags, mountain cliffs, or valley shoulders. It forms through mechanical weathering (freeze-thaw cycles, rock falls).

**Visual Characteristics:**
- Angular rock fragments (unlike rounded river gravel)
- Varying sizes: small pebbles to fist-sized stones
- Gray/brown coloration matching parent rock
- Often slopes downward (accumulation angle ~35°)
- No vegetation, barren appearance

**In-Game Representation:**
- Loose, angular rocks scattered on ground
- Slightly elevated texture suggesting instability
- Typically passable but slower terrain
- Found at cliff bases, on mountain slopes

### 3.2 Tile Visual Reference Guide

#### Plains Biome
| Tile | Visual Description |
|------|-------------------|
| plains_grass | Short green grass, bright and healthy |
| plains_grass_tall | Waist-high grass swaying, partially obscuring |
| plains_wildflowers | Colorful flowers (yellow, purple, red) amid grass |
| plains_fertile_soil | Dark brown tilled or rich soil |
| plains_path | Worn dirt path, compressed earth |
| plains_small_rock | Single gray rock, knee-height |
| plains_boulder | Large gray boulder, chest-height or taller |
| plains_shrub | Green bush with leaves, waist-height |

#### Forest Biome
| Tile | Visual Description |
|------|-------------------|
| forest_floor | Brown soil with fallen leaves and twigs |
| forest_moss | Green moss covering ground, spongy texture |
| forest_leaves | Thick layer of fallen autumn leaves |
| forest_clearing | Grassy opening in canopy, sunlit |
| forest_undergrowth | Ferns, small plants, dense ground cover |
| forest_tree | Single tree trunk with canopy overhead |
| forest_dense_trees | Cluster of multiple trees, very dark |
| forest_fallen_log | Horizontal dead tree, moss-covered |
| forest_mushrooms | Cluster of mushrooms (red caps, white stems) |
| forest_stump | Cut tree stump, showing rings |

#### Desert Biome
| Tile | Visual Description |
|------|-------------------|
| desert_sand | Smooth golden sand dunes |
| desert_dune | Taller sand ridge with wind-shaped edge |
| desert_cracked | Dried mud/clay with crack patterns |
| desert_gravel | Small loose stones, gray/tan color |
| desert_rocky | Exposed bedrock, flat stone surface |
| desert_rock | Standing rock formation, weathered |
| desert_outcrop | Larger rock formation, cliff-like |
| desert_cactus | Green saguaro or prickly pear cactus |
| desert_bones | Animal skeleton, sun-bleached white |
| desert_skull | Large skull (cow/horse), dramatic prop |

#### Tundra Biome
| Tile | Visual Description |
|------|-------------------|
| tundra_snow | Fresh white snow, smooth surface |
| tundra_packed_snow | Compressed snow, icy blue tint |
| tundra_frost | Ground with ice crystals, sparkly |
| tundra_permafrost | Frozen soil with ice veins visible |
| tundra_ice_path | Smooth ice surface, reflective |
| tundra_ice_block | Large ice cube/formation, translucent |
| tundra_frozen_rock | Snow-capped rock, icy |
| tundra_evergreen | Pine/spruce tree, snow on branches |
| tundra_frozen_lake | Flat ice sheet, cracks visible |
| tundra_snowdrift | Piled snow, wind-formed mound |

#### Swamp Biome
| Tile | Visual Description |
|------|-------------------|
| swamp_mud | Dark brown wet mud, bubbling |
| swamp_shallow_water | Murky green-brown water, visible bottom |
| swamp_reeds | Tall reed plants, brown/green |
| swamp_moss | Dark green hanging/ground moss |
| swamp_peat | Dark brown organic soil, spongy |
| swamp_murky_pool | Deep dark water, opaque |
| swamp_dead_tree | Leafless gray tree, twisted |
| swamp_gnarled_roots | Exposed tree roots, tangled |
| swamp_lily_pads | Green pads with white flowers |
| swamp_fog_hollow | Low-lying fog effect |

#### Mountains Biome
| Tile | Visual Description |
|------|-------------------|
| mountains_rock | Gray exposed rock, flat-ish |
| mountains_gravel | Loose gray stones, smaller than scree |
| mountains_slate | Layered gray stone, flat plates |
| mountains_scree | Angular broken rock fragments (see 3.1) |
| mountains_alpine_grass | Short tough grass, sparse |
| mountains_cliff | Vertical rock face, impassable |
| mountains_boulder | Large rounded gray boulder |
| mountains_ore | Rock with visible metal veins (gold/copper) |
| mountains_alpine_shrub | Small hardy bush, wind-stunted |
| mountains_cave_entrance | Dark opening in rock, mysterious |

---

## 4. Missing Tile Categories

### 4.1 Water Features (High Priority)
Current water is limited to swamp context. Need:

| Suggested Tile | Description | Biome Association |
|---------------|-------------|-------------------|
| water_shallow | Clear shallow water, riverbed visible | Universal |
| water_deep | Deep blue water, impassable | Universal |
| water_rapids | White water, fast-moving | Mountains |
| water_pond | Still water, reflective | Plains, Forest |
| water_shore | Sand/gravel at water's edge | Universal |

### 4.2 Transition Tiles (Medium Priority)
For smooth biome borders:

| Suggested Tile | Description | Transitions Between |
|---------------|-------------|---------------------|
| grass_to_sand | Sparse grass on sandy soil | Plains ↔ Desert |
| snow_on_grass | Frost-touched grass | Plains ↔ Tundra |
| muddy_grass | Waterlogged grass | Plains ↔ Swamp |
| rocky_grass | Grass with exposed rock | Plains ↔ Mountains |
| forest_edge | Tree line with open grass | Plains ↔ Forest |

### 4.3 Interactive Elements (Lower Priority)
Tiles that could have gameplay effects:

| Suggested Tile | Description | Effect |
|---------------|-------------|--------|
| quicksand | Desert trap | Slows movement |
| thin_ice | Breakable ice | Chance to fall through |
| hot_springs | Steaming water | Healing zone |
| magic_circle | Glowing runes | Teleport/buff point |
| campfire_remains | Old campfire | Story/resource point |

---

## 5. Recommendations Summary

### 5.1 Immediate Fixes
1. **Review passability of decoration tiles**: `forest_mushrooms`, `swamp_lily_pads`, `desert_bones` should likely be passable
2. **Add auto-tiling to primary terrain tiles**: `tundra_snow`, `swamp_mud`, `mountains_rock`, `plains_grass`
3. **Standardize layer assignments**: Create clear criteria for terrain vs decoration

### 5.2 Content Additions
1. **Water tiles**: Add universal water features for rivers/lakes
2. **Transition tiles**: Create biome-edge tiles for smoother boundaries
3. **More variations**: Add variant coordinates to tiles that currently lack them

### 5.3 System Improvements
1. **Define clear tile categories**:
   - Terrain: Ground surface, can have auto-tiling
   - Decoration: Visual interest, usually passable
   - Structure: Obstacles, usually solid
   - Effects: Overlays, transparent

2. **Add biome transition rules**: Define which biomes can border each other and how

### 5.4 Visual Consistency
1. **Document art style**: Ensure all tiles match the FantasyDreamland tileset aesthetic
2. **Create tile palette**: Define allowed color ranges per biome
3. **Establish scale**: Ensure tile sizes are consistent (1 tile = X meters)

---

## 6. Appendix: Complete Tile List

### By Passability
**Passable (36 tiles)**:
- Debug: debug_path, debug_target
- Generic: dirt
- Plains: plains_grass, plains_grass_tall, plains_wildflowers, plains_fertile_soil, plains_path
- Forest: forest_floor, forest_moss, forest_leaves, forest_clearing, forest_undergrowth
- Desert: desert_sand, desert_dune, desert_cracked, desert_gravel, desert_rocky
- Tundra: tundra_snow, tundra_packed_snow, tundra_frost, tundra_permafrost, tundra_ice_path
- Swamp: swamp_mud, swamp_shallow_water, swamp_reeds, swamp_moss, swamp_peat
- Mountains: mountains_rock, mountains_gravel, mountains_slate, mountains_scree, mountains_alpine_grass

**Solid (26 tiles)**:
- Generic: wall, stone, glass
- Plains: plains_small_rock, plains_boulder, plains_shrub
- Forest: forest_tree, forest_dense_trees, forest_fallen_log, forest_mushrooms, forest_stump
- Desert: desert_rock, desert_outcrop, desert_cactus, desert_bones, desert_skull
- Tundra: tundra_ice_block, tundra_frozen_rock, tundra_evergreen, tundra_frozen_lake, tundra_snowdrift
- Swamp: swamp_murky_pool, swamp_dead_tree, swamp_gnarled_roots, swamp_lily_pads, swamp_fog_hollow
- Mountains: mountains_cliff, mountains_boulder, mountains_ore, mountains_alpine_shrub, mountains_cave_entrance

---

*Audit completed: 2025-12-30*
*Document version: 1.0*
