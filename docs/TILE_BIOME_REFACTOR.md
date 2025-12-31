# Tile & Biome System Refactor

## Executive Summary

This document describes a comprehensive refactor of CardCleaner's tile and biome system to:
1. Make all biome configuration data-driven (eliminate hardcoded BiomeRegistry)
2. Add 4 new biomes: Water, Cave, Volcanic, Magical
3. Consolidate visually-duplicate tiles into canonical versions
4. Remove obsolete BlobGeneration system (replaced by WFC)
5. Add tile editor functionality for tile management

**Status**: Planning Document
**Last Updated**: 2025-12-31

---

## Table of Contents

1. [Current State Analysis](#1-current-state-analysis)
2. [Problems with Current State](#2-problems-with-current-state)
3. [Target State Vision](#3-target-state-vision)
4. [Work Packages](#4-work-packages)
5. [Technical Specifications](#5-technical-specifications)
6. [Migration Strategy](#6-migration-strategy)
7. [Risk Analysis](#7-risk-analysis)

---

## 1. Current State Analysis

### 1.1 Tile Data Flow

```
tiles.json
    │
    ▼
TileDataLoader.cs ──► TileRegistry.cs ──► Game Systems
    │                      │
    ▼                      ▼
BiomeRegistry.cs      TileDefinition
(HARDCODED)           (Runtime model)
```

**Problem**: BiomeRegistry.cs contains hardcoded tile IDs and biome configurations that should be data.

### 1.2 Key Files

| File | Purpose | Issues |
|------|---------|--------|
| `Data/Tiles/tiles.json` | Tile definitions | ✅ Data-driven |
| `Scripts/Core/Enumeration/BiomeType.cs` | Biome enum | ❌ Hardcoded enum limits biomes |
| `Scripts/Features/Worldgen/Biomes/BiomeRegistry.cs` | Biome→tile mappings | ❌ Hardcoded strings |
| `Scripts/Features/Worldgen/Biomes/BiomeDefinition.cs` | Biome model | ✅ Good, uses TilePool |
| `Scripts/Features/Worldgen/Biomes/TilePool.cs` | Weighted tile selection | ✅ Good |
| `addons/tile_editor/BiomePoolPanel.cs` | Editor UI for biomes | ❌ Hardcoded biome list |
| `addons/tile_editor/BlobSettingsPanel.cs` | Blob generation settings | ❌ Obsolete, remove |
| `Scripts/Features/Worldgen/BlobGeneration/TerrainBlobGenerator.cs` | Noise-based clustering | ❌ Obsolete, WFC handles this |

### 1.3 Current Biomes

```csharp
// BiomeType.cs - HARDCODED
public enum BiomeType
{
    Plains = 0,
    Forest = 1,
    Desert = 2,
    Tundra = 3,
    Swamp = 4,
    Mountains = 5
}
```

### 1.4 Biome Registry Pattern (Current)

```csharp
// BiomeRegistry.cs - HARDCODED EXAMPLE
private void RegisterPlains()
{
    var passable = new TilePool();
    passable.Add("plains_grass", 0.40f);      // ❌ Hardcoded strings
    passable.Add("plains_grass_tall", 0.25f); // ❌ Can't edit without rebuild
    // ...
}
```

### 1.5 Tile Counts by Biome

| Biome | Passable | Blocked | Total |
|-------|----------|---------|-------|
| Plains | 5 | 3 | 8 |
| Forest | 5 | 5 | 10 |
| Desert | 5 | 5 | 10 |
| Tundra | 5 | 5 | 10 |
| Swamp | 5 | 5 | 10 |
| Mountains | 5 | 5 | 10 |
| **Universal** | 3 | 2 | 5 |
| **Total** | 33 | 30 | **63** |

### 1.6 Duplicate Tile Analysis

**"Green Grassy" tiles (visually similar):**
- `plains_grass` - Short green grass
- `plains_grass_tall` - Taller grass (DISTINCT - keep)
- `forest_floor` - Brown soil with leaves (DISTINCT - keep)
- `forest_clearing` - Grassy clearing (DUPLICATE of plains_grass)
- `forest_moss` - Green moss (DISTINCT - keep)
- `mountains_alpine_grass` - Short grass (DUPLICATE of plains_grass)

**Recommendation**: Keep `grass`, `tall_grass`, `forest_floor`, `moss`. Delete `forest_clearing`, `alpine_grass` or merge via biome tags.

**"Brown Rocky/Gravel" tiles (visually similar):**
- `desert_gravel` - Small loose stones
- `mountains_gravel` - Small loose stones (DUPLICATE)
- `mountains_scree` - Angular broken rock (DISTINCT - keep)
- `mountains_rock` - Exposed rock (DISTINCT - keep)
- `mountains_slate` - Layered stone (DISTINCT - keep)
- `desert_rocky` - Exposed bedrock (DUPLICATE of mountains_rock)
- `desert_cracked` - Dried mud (DISTINCT - keep)

**Recommendation**: Keep `gravel` (universal), `rock`, `slate`, `scree`, `cracked_earth`. Use biome tags.

---

## 2. Problems with Current State

### 2.1 Hardcoded Biome Data

**Problem**: BiomeRegistry.cs contains ~200 lines of hardcoded tile ID strings.

```csharp
// Can't add biomes without code changes
passable.Add("plains_grass", 0.40f);  // Magic strings
blocked.Add("plains_shrub", 0.30f);   // No validation
```

**Impact**:
- Can't add/edit biomes without recompiling
- Tile editor can't modify biome pools effectively
- Typos in tile IDs silently fail

### 2.2 Hardcoded BiomeType Enum

**Problem**: Adding a new biome requires:
1. Add to `BiomeType.cs` enum
2. Add signature constants to `BiomeRegistry.cs`
3. Add `Register{Biome}()` method
4. Update `BiomePoolPanel.cs` hardcoded string array
5. Rebuild

**Impact**: High friction for adding Water, Cave, Volcanic, Magical biomes.

### 2.3 Obsolete BlobGeneration System

**Problem**: `BlobGeneration/` folder and `BlobSettingsPanel` exist but:
- WFC (Wave Function Collapse) now handles terrain coherence
- BlobGeneration settings stored in tiles.json but not used meaningfully
- Editor panel takes up space with unused functionality

**Files to remove**:
```
Scripts/Features/Worldgen/BlobGeneration/TerrainBlobGenerator.cs
addons/tile_editor/BlobSettingsPanel.cs
tiles.json "blobGeneration" section
```

### 2.4 Tile Editor Missing Features

**Missing**:
- Duplicate tile button (copy tile with new ID)
- Delete tile button (remove tile from JSON)
- Editable tile IDs (currently read-only)
- Biome pool weight editing (currently view-only)

### 2.5 Duplicate Tiles Waste Atlas Space

**Problem**: 6+ tiles are visually identical or near-identical across biomes.

**Current approach**: Separate tile per biome
**Better approach**: One canonical tile with `"biomes": ["plains", "forest", "mountains"]`

---

## 3. Target State Vision

### 3.1 Unified Data Model

All tile AND biome data lives in `tiles.json`:

```json
{
  "$schema": "./tiles.schema.json",
  "version": "2.0",
  "tileset": "res://Assets/Terrain/TileSets/ByPack/FantasyDreamland.tres",

  "biomes": {
    "plains": {
      "displayName": "Plains",
      "signature": [0, 0, 0.2, 0.1, 0, 0, 0.2, 0],
      "blockedPercentage": 0.15,
      "passableTiles": {
        "grass": 0.40,
        "tall_grass": 0.25,
        "wildflowers": 0.15,
        "fertile_soil": 0.10,
        "path": 0.10
      },
      "blockedTiles": {
        "small_rock": 0.40,
        "boulder": 0.30,
        "shrub": 0.30
      }
    },
    "water": {
      "displayName": "Water",
      "signature": [-0.5, -0.3, 0.2, 0.3, 0, -0.5, 0.1, 0],
      "blockedPercentage": 0.80,
      "passableTiles": { "shallow_water": 0.60, "shore": 0.40 },
      "blockedTiles": { "deep_water": 0.70, "coral": 0.20, "seaweed": 0.10 }
    }
    // ... more biomes
  },

  "tiles": [
    {
      "id": "grass",
      "name": "Grass",
      "passability": "passable",
      "layer": "terrain",
      "biomes": ["plains", "forest", "mountains"],
      // ... rest of tile definition
    }
  ]
}
```

### 3.2 Dynamic Biome System

**No more enum!** Biomes are loaded from data:

```csharp
public class BiomeRegistry
{
    private readonly Dictionary<string, BiomeDefinition> _biomes = new();

    public void LoadFromJson(JsonElement biomesSection) { ... }
    public BiomeDefinition? GetBiome(string id) => _biomes.GetValueOrDefault(id);
    public IEnumerable<string> GetAllBiomeIds() => _biomes.Keys;
}
```

### 3.3 Tile Editor Enhancements

**New buttons on tile browser**:
- 📋 **Duplicate** - Create copy with new ID
- 🗑️ **Delete** - Remove tile from JSON (with confirmation)
- ✏️ **Rename ID** - Change tile ID (updates all biome references)

**New biome pool editing**:
- Editable weights per tile
- Drag-and-drop tile ordering
- Add/remove tiles from pools
- Create new biomes

### 3.4 Consolidated Tile Set

**Before** (63 tiles):
```
plains_grass, plains_grass_tall, forest_floor, forest_clearing,
forest_moss, mountains_alpine_grass, desert_gravel, mountains_gravel, ...
```

**After** (~50 tiles + 20 new):
```
grass, tall_grass, forest_floor, moss, gravel, rock, slate, scree, ...
+ shallow_water, deep_water, shore, rapids, coral, seaweed
+ cave_floor, stalactite, crystal, glowing_moss, underground_lake
+ lava, obsidian, ash, volcanic_rock, magma_vent
+ enchanted_grass, rune_circle, void_rift, mana_pool
```

### 3.5 New Biomes

| Biome | Theme | Signature Profile | Key Tiles |
|-------|-------|-------------------|-----------|
| **Water** | Lakes, rivers, ocean | Cool, fluid, helpful | shallow_water, deep_water, shore, coral |
| **Cave** | Underground, dark | Solid, dark, dense | cave_floor, stalactite, crystal, rubble |
| **Volcanic** | Lava, fire, danger | Hot, chaotic, harmful | lava, obsidian, ash, magma_vent |
| **Magical** | Enchanted, mystical | Chaotic, bright, spatial | enchanted_grass, rune_circle, void_rift |

---

## 4. Work Packages

Each work package is designed to be completed in a single `/solve` session.

### WP1: Remove BlobGeneration System
**Priority**: P1 (Cleanup before new work)
**Estimated Complexity**: Low
**Dependencies**: None

**Scope**:
1. Delete `Scripts/Features/Worldgen/BlobGeneration/` folder
2. Delete `addons/tile_editor/BlobSettingsPanel.cs`
3. Remove `BlobSettingsPanel` tab from `TileEditorDock.cs`
4. Remove `blobGeneration` section from `tiles.json`
5. Remove `BlobConfig`, `EditableBlobConfig` from `TileEditorService.cs`
6. Remove `BlobConfigModified` event
7. Update any code that references `TerrainBlobGenerator`

**Acceptance Criteria**:
- [ ] No files in BlobGeneration folder
- [ ] No "Blob Settings" tab in tile editor
- [ ] tiles.json has no blobGeneration key
- [ ] Project compiles without errors
- [ ] Tests pass

---

### WP2: Data-Driven Biomes (Schema + Loader)
**Priority**: P1 (Foundation for other work)
**Estimated Complexity**: Medium
**Dependencies**: WP1

**Scope**:
1. Update `tiles.schema.json` to include biomes section
2. Create `BiomeData` model for JSON deserialization
3. Update `TileDataLoader.cs` to parse biomes section
4. Modify `BiomeRegistry.cs` to load from data instead of hardcoded
5. Update `BiomeDefinition.cs` if needed
6. Add biomes section to `tiles.json` with current 6 biomes
7. Remove hardcoded `Register{Biome}()` methods

**Schema Addition**:
```json
{
  "biomes": {
    "type": "object",
    "additionalProperties": {
      "type": "object",
      "properties": {
        "displayName": { "type": "string" },
        "signature": { "type": "array", "items": { "type": "number" }, "minItems": 8, "maxItems": 8 },
        "blockedPercentage": { "type": "number", "minimum": 0, "maximum": 1 },
        "passableTiles": { "type": "object", "additionalProperties": { "type": "number" } },
        "blockedTiles": { "type": "object", "additionalProperties": { "type": "number" } }
      },
      "required": ["displayName", "signature", "blockedPercentage", "passableTiles", "blockedTiles"]
    }
  }
}
```

**Acceptance Criteria**:
- [ ] Biomes load from tiles.json
- [ ] BiomeRegistry has no hardcoded tile IDs
- [ ] All existing tests pass
- [ ] Game generates maps correctly

---

### WP3: Remove BiomeType Enum
**Priority**: P1 (Required for new biomes)
**Estimated Complexity**: Medium
**Dependencies**: WP2

**Scope**:
1. Replace `BiomeType` enum with `string` biome IDs throughout codebase
2. Update `BiomeDefinition.Type` from enum to string
3. Update `BiomeMapGenerator` to use string IDs
4. Update WFC components to use string biome IDs
5. Update tests to use string IDs
6. Delete `BiomeType.cs` enum file

**Search/Replace Pattern**:
```
BiomeType.Plains  →  "plains"
BiomeType.Forest  →  "forest"
BiomeType type    →  string biomeId
```

**Files to Update** (from grep):
- 44+ files reference BiomeType
- Key files: WfcMapGenerator, WfcTileSelector, BiomeMapGenerator, SimpleMapGenerator

**Acceptance Criteria**:
- [ ] BiomeType.cs deleted
- [ ] All biome references use strings
- [ ] New biomes can be added without code changes
- [ ] All tests pass

---

### WP4: Tile Editor - Duplicate/Delete Buttons
**Priority**: P2 (Editor UX)
**Estimated Complexity**: Low
**Dependencies**: None (can parallel with WP2-3)

**Scope**:
1. Add "Duplicate" button to tile browser panel
2. Add "Delete" button to tile browser panel
3. Implement duplicate logic (prompt for new ID)
4. Implement delete logic (confirmation dialog)
5. Add ID validation (snake_case, unique)
6. Update TileEditorService with new methods

**UI Mockup**:
```
[Tile Entry]  [📋 Dup] [🗑️ Del]
```

**Acceptance Criteria**:
- [ ] Can duplicate tile with new ID
- [ ] Can delete tile (with confirmation)
- [ ] IDs validated for format and uniqueness
- [ ] Changes persist to tiles.json

---

### WP5: Tile Editor - Biome Pool Management
**Priority**: P2 (Editor UX)
**Estimated Complexity**: Medium
**Dependencies**: WP2

**Scope**:
1. Update BiomePoolPanel to load biomes from data
2. Add weight editing (spinbox next to each tile)
3. Add "Add Biome" button
4. Add tile ID editing in biome context
5. Save biome changes back to tiles.json

**Acceptance Criteria**:
- [ ] Biome list populated from tiles.json
- [ ] Can edit tile weights in pools
- [ ] Can create new biomes
- [ ] Changes persist to tiles.json

---

### WP6: Add Water Biome + Tiles
**Priority**: P2 (New content)
**Estimated Complexity**: Medium
**Dependencies**: WP2, WP3

**Scope**:
1. Find water tile candidates in FantasyDreamland tileset
2. Add tile definitions to tiles.json:
   - `shallow_water` (passable)
   - `deep_water` (blocked)
   - `shore` (passable)
   - `rapids` (passable, mountains context)
   - `coral` (blocked, decoration)
   - `seaweed` (passable, decoration)
3. Add "water" biome to tiles.json biomes section
4. Define signature: cool, fluid, helpful

**Signature Design**:
```
[Solidum, Febris, Ordinem, Lumines, Varias, Inertiae, Subsidium, Spatium]
[-0.5,   -0.3,   0.2,     0.3,     0.2,    -0.5,     0.1,       0]
  ↑       ↑       ↑        ↑        ↑        ↑         ↑
  fluid   cool   ordered  bright  flowing  light    helpful
```

**Acceptance Criteria**:
- [ ] 6+ water tiles defined
- [ ] Water biome in tiles.json
- [ ] Tiles appear in tile editor
- [ ] Map generation can use water biome

---

### WP7: Add Cave Biome + Tiles
**Priority**: P3 (New content)
**Estimated Complexity**: Medium
**Dependencies**: WP2, WP3

**Scope**:
1. Find cave tile candidates in tileset
2. Add tile definitions:
   - `cave_floor` (passable)
   - `stalactite` (blocked)
   - `crystal_formation` (blocked)
   - `glowing_moss` (passable, decoration)
   - `underground_lake` (blocked)
   - `rubble` (passable)
   - `cave_wall` (blocked)
3. Add "cave" biome definition

**Signature Design**:
```
[0.6, -0.1, -0.2, -0.7, 0, 0.5, 0, -0.3]
  ↑     ↑     ↑      ↑   ↑   ↑    ↑    ↑
solid  cold  chaotic dark  -  dense  -  enclosed
```

---

### WP8: Add Volcanic Biome + Tiles
**Priority**: P3 (New content)
**Estimated Complexity**: Medium
**Dependencies**: WP2, WP3

**Scope**:
1. Find volcanic tile candidates
2. Add tile definitions:
   - `lava` (blocked, dangerous)
   - `cooled_lava` (passable)
   - `obsidian` (passable)
   - `ash` (passable)
   - `volcanic_rock` (passable)
   - `magma_vent` (blocked)
   - `lava_crack` (passable, decoration)
3. Add "volcanic" biome definition

**Signature Design**:
```
[0.3, 0.9, -0.4, 0.4, 0, 0.3, -0.6, 0]
  ↑    ↑     ↑    ↑   ↑   ↑     ↑    ↑
solid HOT  chaotic bright - dense harmful
```

---

### WP9: Add Magical Biome + Tiles
**Priority**: P3 (New content)
**Estimated Complexity**: Medium
**Dependencies**: WP2, WP3

**Scope**:
1. Find magical/mystical tile candidates
2. Add tile definitions:
   - `enchanted_grass` (passable)
   - `rune_circle` (passable, effect)
   - `crystal_spire` (blocked)
   - `void_rift` (blocked)
   - `mana_pool` (passable)
   - `arcane_glyph` (passable, decoration)
3. Add "magical" biome definition

**Signature Design**:
```
[-0.2, 0.1, -0.6, 0.5, 0.5, -0.3, 0.3, 0.4]
   ↑    ↑     ↑    ↑    ↑     ↑     ↑    ↑
fluid mild chaotic bright spatial light helpful distant
```

---

### WP10: Consolidate Duplicate Tiles
**Priority**: P2 (Cleanup)
**Estimated Complexity**: Medium
**Dependencies**: WP2, WP5

**Scope**:
1. Identify canonical versions of duplicate tiles
2. Update biome tags on canonical tiles
3. Remove duplicate tile definitions
4. Update biome pool references

**Consolidation Plan**:

| Keep | Delete | Action |
|------|--------|--------|
| `grass` (from plains_grass) | `forest_clearing`, `alpine_grass` | Add biomes: ["plains", "forest", "mountains"] |
| `gravel` (from desert_gravel) | `mountains_gravel` | Add biomes: ["desert", "mountains"] |
| `rock` (from mountains_rock) | `desert_rocky` | Add biomes: ["mountains", "desert"] |
| `tall_grass` (from plains_grass_tall) | - | Keep, distinct |
| `moss` (from forest_moss) | - | Keep, distinct |
| `forest_floor` | - | Keep, distinct brown soil |

**Acceptance Criteria**:
- [ ] ~10 tiles removed
- [ ] Remaining tiles have appropriate biome tags
- [ ] No broken biome pool references
- [ ] Map generation unchanged

---

### WP11: Update Worldgen Architecture Doc
**Priority**: P3 (Documentation)
**Estimated Complexity**: Low
**Dependencies**: WP1-10

**Scope**:
1. Update docs/WORLDGEN_ARCHITECTURE.md
2. Document new biome system
3. Remove BlobGeneration references
4. Add new biome descriptions
5. Update tile categories

---

## 5. Technical Specifications

### 5.1 tiles.json v2.0 Schema

```json
{
  "$schema": "http://json-schema.org/draft-07/schema#",
  "type": "object",
  "required": ["version", "tileset", "tiles"],
  "properties": {
    "version": { "const": "2.0" },
    "tileset": { "type": "string" },
    "biomes": {
      "type": "object",
      "additionalProperties": {
        "$ref": "#/definitions/biome"
      }
    },
    "tiles": {
      "type": "array",
      "items": { "$ref": "#/definitions/tile" }
    }
  },
  "definitions": {
    "biome": {
      "type": "object",
      "required": ["displayName", "signature", "blockedPercentage", "passableTiles", "blockedTiles"],
      "properties": {
        "displayName": { "type": "string" },
        "signature": {
          "type": "array",
          "items": { "type": "number", "minimum": -1, "maximum": 1 },
          "minItems": 8,
          "maxItems": 8
        },
        "blockedPercentage": { "type": "number", "minimum": 0, "maximum": 1 },
        "passableTiles": {
          "type": "object",
          "additionalProperties": { "type": "number", "minimum": 0 }
        },
        "blockedTiles": {
          "type": "object",
          "additionalProperties": { "type": "number", "minimum": 0 }
        }
      }
    },
    "tile": {
      "type": "object",
      "required": ["id", "name", "passability", "atlasCoords", "sourceId", "layer"],
      "properties": {
        "id": { "type": "string", "pattern": "^[a-z][a-z0-9_]*$" },
        "name": { "type": "string" },
        "passability": { "enum": ["passable", "solid", "partially_passable"] },
        "layer": { "enum": ["terrain", "decoration", "structure", "effects"] },
        "biomes": { "type": "array", "items": { "type": "string" } }
      }
    }
  }
}
```

### 5.2 BiomeRegistry Refactor

**Current** (hardcoded):
```csharp
public void RegisterDefaultBiomes()
{
    RegisterPlains();   // 50 lines of Add() calls
    RegisterForest();   // 50 lines of Add() calls
    // ...
}
```

**Target** (data-driven):
```csharp
public void LoadFromData(Dictionary<string, BiomeJsonData> biomeData)
{
    foreach (var (id, data) in biomeData)
    {
        var passable = new TilePool();
        foreach (var (tileId, weight) in data.PassableTiles)
            passable.Add(tileId, weight);

        var blocked = new TilePool();
        foreach (var (tileId, weight) in data.BlockedTiles)
            blocked.Add(tileId, weight);

        var definition = new BiomeDefinition(
            id,
            data.DisplayName,
            new CardSignature(data.Signature),
            passable,
            blocked,
            data.BlockedPercentage);

        Register(definition);
    }
}
```

### 5.3 Signature Reference

The 8D card signature system:

| Index | Name | Description | Range |
|-------|------|-------------|-------|
| 0 | Solidum | Solidity | -1 (fluid) to +1 (rock) |
| 1 | Febris | Temperature | -1 (ice) to +1 (fire) |
| 2 | Ordinem | Order | -1 (chaotic) to +1 (ordered) |
| 3 | Lumines | Luminance | -1 (dark) to +1 (bright) |
| 4 | Varias | Manifold | -1 (time) to +1 (space) |
| 5 | Inertiae | Density | -1 (heavy) to +1 (light) |
| 6 | Subsidium | Helpfulness | -1 (harmful) to +1 (helpful) |
| 7 | Spatium | Distance | -1 (near) to +1 (distant) |

---

## 6. Migration Strategy

### 6.1 Execution Order

```
Phase 1: Cleanup
  └─ WP1: Remove BlobGeneration (standalone)

Phase 2: Foundation
  ├─ WP2: Data-Driven Biomes (schema + loader)
  └─ WP3: Remove BiomeType Enum (requires WP2)

Phase 3: Editor UX (parallel track)
  ├─ WP4: Duplicate/Delete Buttons (standalone)
  └─ WP5: Biome Pool Management (requires WP2)

Phase 4: New Content
  ├─ WP6: Water Biome (requires WP3)
  ├─ WP7: Cave Biome (requires WP3)
  ├─ WP8: Volcanic Biome (requires WP3)
  └─ WP9: Magical Biome (requires WP3)

Phase 5: Consolidation
  └─ WP10: Consolidate Duplicates (requires WP5)

Phase 6: Documentation
  └─ WP11: Update Architecture Doc (after all)
```

### 6.2 Suggested /solve Order

1. `/solve WP1` - Remove BlobGeneration (quick win, cleanup)
2. `/solve WP2` - Data-Driven Biomes (foundation)
3. `/solve WP4` - Tile Editor Buttons (can parallel)
4. `/solve WP3` - Remove BiomeType Enum (big refactor)
5. `/solve WP5` - Biome Pool Management (editor complete)
6. `/solve WP6` - Water Biome (first new biome)
7. `/solve WP10` - Consolidate Duplicates (cleanup)
8. `/solve WP7, WP8, WP9` - Remaining biomes
9. `/solve WP11` - Documentation

---

## 7. Risk Analysis

### 7.1 High Risk

| Risk | Mitigation |
|------|------------|
| WP3 touches 44+ files | Careful regex replace, comprehensive tests |
| Biome pool changes break map generation | Verify maps generate correctly after each WP |
| Tile consolidation breaks saved games | No saved games exist yet (pre-release) |

### 7.2 Medium Risk

| Risk | Mitigation |
|------|------------|
| New biome signatures unbalanced | Can tune post-implementation |
| Tile editor changes cause data loss | Backup tiles.json before each WP |
| Schema migration breaks tile loading | Version field enables migration path |

### 7.3 Low Risk

| Risk | Mitigation |
|------|------------|
| BlobGeneration removal breaks something | WFC already handles terrain coherence |
| New tiles not visually distinct | Review atlas visually before adding |

---

## Appendix A: Tile Inventory (Current)

<details>
<summary>Click to expand full tile list</summary>

### Universal Tiles (5)
- debug_path, debug_target
- dirt (autotile)
- wall, stone, glass

### Plains Tiles (8)
- plains_grass, plains_grass_tall, plains_wildflowers
- plains_fertile_soil, plains_path
- plains_small_rock, plains_boulder, plains_shrub

### Forest Tiles (10)
- forest_floor (autotile), forest_moss (autotile), forest_leaves
- forest_clearing (autotile), forest_undergrowth
- forest_tree, forest_dense_trees (autotile), forest_fallen_log
- forest_mushrooms, forest_stump

### Desert Tiles (10)
- desert_sand (autotile), desert_dune (autotile), desert_cracked (autotile)
- desert_gravel (autotile), desert_rocky (blob47 autotile)
- desert_rock, desert_outcrop, desert_cactus
- desert_bones, desert_skull

### Tundra Tiles (10)
- tundra_snow, tundra_packed_snow, tundra_frost
- tundra_permafrost, tundra_ice_path
- tundra_ice_block, tundra_frozen_rock, tundra_evergreen
- tundra_frozen_lake, tundra_snowdrift

### Swamp Tiles (10)
- swamp_mud, swamp_shallow_water, swamp_reeds
- swamp_moss, swamp_peat
- swamp_murky_pool, swamp_dead_tree, swamp_gnarled_roots
- swamp_lily_pads, swamp_fog_hollow

### Mountains Tiles (10)
- mountains_rock, mountains_gravel, mountains_slate
- mountains_scree, mountains_alpine_grass
- mountains_cliff, mountains_boulder, mountains_ore
- mountains_alpine_shrub, mountains_cave_entrance

</details>

---

## Appendix B: Signature Profiles (New Biomes)

| Biome | Sol | Feb | Ord | Lum | Var | Ine | Sub | Spa |
|-------|-----|-----|-----|-----|-----|-----|-----|-----|
| Plains | 0 | 0 | 0.2 | 0.1 | 0 | 0 | 0.2 | 0 |
| Forest | 0 | -0.3 | -0.3 | -0.3 | 0 | 0.2 | 0.3 | 0 |
| Desert | 0.3 | 0.7 | 0.3 | 0.4 | 0 | -0.2 | -0.2 | 0.3 |
| Tundra | 0.2 | -0.7 | 0.5 | 0.3 | 0 | 0.1 | 0 | -0.1 |
| Swamp | -0.2 | -0.2 | -0.5 | -0.5 | 0 | -0.3 | -0.1 | 0 |
| Mountains | 0.7 | -0.2 | 0.4 | 0.2 | 0 | 0.4 | 0 | 0.3 |
| **Water** | -0.5 | -0.3 | 0.2 | 0.3 | 0.2 | -0.5 | 0.1 | 0 |
| **Cave** | 0.6 | -0.1 | -0.2 | -0.7 | 0 | 0.5 | 0 | -0.3 |
| **Volcanic** | 0.3 | 0.9 | -0.4 | 0.4 | 0 | 0.3 | -0.6 | 0 |
| **Magical** | -0.2 | 0.1 | -0.6 | 0.5 | 0.5 | -0.3 | 0.3 | 0.4 |

---

*Document created: 2025-12-31*
*For questions, review the work packages and execute via `/solve WP{N}`*
