# Flexible Auto-Tile Format System

## Problem Statement

The current tile editor has three hardcoded auto-tile formats (Corner16, Edge16, Blob47) that cannot express:

1. **Custom bitmask patterns**: 1-tile-wide hedges (forbid interior), partial edges, cardinal-only
2. **Variable-height variants**: Wall bottom edges (bitmasks 2,4,6) being 3 tiles tall
3. **Grid offsets**: Tilesets authored with half-tile shifts
4. **Non-standard tile sizes**: 24x24, 32x32 grids

## Solution Overview

Data-driven auto-tile format system with:
- **Per-variant spatial properties**: Size (cells occupied), Offset (anchor point), AtlasRegionSize
- **Custom bitmask filtering**: Define which bitmask values are allowed
- **WFC cell reservation**: Tall variants reserve cells above them
- **Tileset-level config**: Base tile size and grid offset

## Key Data Structures

```csharp
// Which neighbor computation algorithm to use
enum BitmaskType { Corner4, Edge4, Full8 }

// Full specification for a single auto-tile variant
record VariantDefinition(
    Vector2I AtlasCoords,           // Position in atlas
    Vector2I Size = default,        // Cells occupied (default 1x1)
    Vector2I Offset = default,      // Anchor offset (default 0,0)
    Vector2I? AtlasRegionSize = null // Source size if non-standard
);

// Complete format definition
class AutoTileFormatDefinition {
    string Name;
    BitmaskType BitmaskType;
    bool IsBuiltIn;
    HashSet<int> AllowedBitmasks;
    Dictionary<int, VariantDefinition> VariantMappings;
}

// Tileset-level spatial config
class TilesetConfig {
    Vector2I BaseTileSize;  // 16x16, 24x24, etc.
    Vector2 GridOffset;     // Half-tile shift support
}
```

## Example Custom Formats

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
        // ... bitmask 15 (interior) intentionally omitted = forbidden
      ]
    },
    {
      "name": "wall_south",
      "bitmaskType": "edge4",
      "variants": [
        { "bitmask": 4, "atlasCoords": {"x": 0, "y": 5}, "size": {"x": 1, "y": 3}, "offset": {"x": 0, "y": -2} },
        { "bitmask": 6, "atlasCoords": {"x": 1, "y": 5}, "size": {"x": 1, "y": 3}, "offset": {"x": 0, "y": -2} },
        { "bitmask": 5, "atlasCoords": {"x": 2, "y": 5}, "size": {"x": 1, "y": 3}, "offset": {"x": 0, "y": -2} }
      ]
    }
  ]
}
```

---

## Implementation Stages

Each stage is designed to fit within ~100k tokens for `/solve` execution.

---

### Stage 1: Core Data Model & JSON Schema

**Goal**: Create the foundational types and make them loadable from tiles.json.

**Subtasks**:

1. **Create BitmaskType enum** (`Scripts/Features/Worldgen/AutoTiling/BitmaskType.cs`)
   - `Corner4`, `Edge4`, `Full8` values
   - Maps to existing NeighborBitmaskCorner, NeighborBitmask, NeighborBitmask8

2. **Create VariantDefinition record** (`Scripts/Features/Worldgen/AutoTiling/VariantDefinition.cs`)
   - AtlasCoords, Size (default 1x1), Offset (default 0,0), AtlasRegionSize (nullable)
   - Immutable record type

3. **Create AutoTileFormatDefinition class** (`Scripts/Features/Worldgen/AutoTiling/AutoTileFormatDefinition.cs`)
   - Name, BitmaskType, IsBuiltIn, AllowedBitmasks, VariantMappings
   - `GetVariant(int bitmask)` method
   - `GetExpectedVariantCount()` method

4. **Create AutoTileFormatRegistry** (`Scripts/Features/Worldgen/AutoTiling/AutoTileFormatRegistry.cs`)
   - Static registry with `Register()`, `TryGet()`, `GetAll()`
   - Case-insensitive name lookup
   - Thread-safe initialization

5. **Create BuiltInAutoTileFormats** (`Scripts/Features/Worldgen/AutoTiling/BuiltInAutoTileFormats.cs`)
   - `CreateCorner16()`, `CreateEdge16()`, `CreateBlob47()` factory methods
   - Each returns fully configured AutoTileFormatDefinition
   - Auto-registered on first registry access

6. **Create TilesetConfig class** (`Scripts/Features/Deckbuilder/Tiles/TilesetConfig.cs`)
   - BaseTileSize (Vector2I), GridOffset (Vector2)
   - JSON serialization support

7. **Update tiles.json schema** (`Data/Tiles/tiles.schema.json`)
   - Add `tilesetConfig` object definition
   - Add `autoTileFormats` array definition
   - Document all new fields

8. **Update TileDataLoader for new sections** (`Scripts/Core/Services/TileDataLoader.cs`)
   - Parse `tilesetConfig` section (backward compatible defaults)
   - Parse `autoTileFormats` section and register to registry
   - Handle legacy `autoTileFormat` enum values

**Acceptance Criteria**:
- [ ] All new types compile and have XML docs
- [ ] Built-in formats registered automatically
- [ ] `AutoTileFormatRegistry.TryGet("corner16")` returns valid format
- [ ] tiles.json with `autoTileFormats` section loads without error
- [ ] Legacy tiles.json (no new sections) still loads

**Files Modified**:
- NEW: `Scripts/Features/Worldgen/AutoTiling/BitmaskType.cs`
- NEW: `Scripts/Features/Worldgen/AutoTiling/VariantDefinition.cs`
- NEW: `Scripts/Features/Worldgen/AutoTiling/AutoTileFormatDefinition.cs`
- NEW: `Scripts/Features/Worldgen/AutoTiling/AutoTileFormatRegistry.cs`
- NEW: `Scripts/Features/Worldgen/AutoTiling/BuiltInAutoTileFormats.cs`
- NEW: `Scripts/Features/Deckbuilder/Tiles/TilesetConfig.cs`
- MODIFY: `Data/Tiles/tiles.schema.json`
- MODIFY: `Scripts/Core/Services/TileDataLoader.cs`

**Run Command**: `/solve Stage 1: Implement core data model (BitmaskType, VariantDefinition, AutoTileFormatDefinition, AutoTileFormatRegistry, BuiltInAutoTileFormats, TilesetConfig) and update TileDataLoader to parse autoTileFormats and tilesetConfig from tiles.json. See docs/FLEXIBLE_AUTOTILE_PLAN.md Stage 1 for details.`

---

### Stage 2: Runtime Integration

**Goal**: Wire the new format system into TileDefinition, AutoTileHelper, and basic WFC constraints.

**Subtasks**:

1. **Update TileDefinition** (`Scripts/Features/Deckbuilder/Tiles/TileDefinition.cs`)
   - Change `AutoTileFormat` from enum to `string? AutoTileFormatName`
   - Add `GetAutoTileFormat()` → resolves from registry
   - Add `GetVariantDefinition(int bitmask)` → returns full VariantDefinition
   - Update `ExpectedVariantCount` to delegate to format
   - Keep `GetAutoTileCoords(int bitmask)` working for backward compat

2. **Update AutoTileHelper** (`Scripts/Features/Worldgen/AutoTiling/AutoTileHelper.cs`)
   - `ComputeBitmask()` uses `format.BitmaskType` to select algorithm
   - Add `GetAutoTileVariant()` returning VariantDefinition
   - Remove switch on enum, use registry lookup
   - Handle null/unknown format gracefully

3. **Update AutoTileGapConstraint** (`Scripts/Features/Worldgen/Wfc/Constraints/AutoTileGapConstraint.cs`)
   - Use `format.AllowedBitmasks` for validation
   - Formats missing bitmask 15 naturally enforce 1-tile-wide
   - Preserve existing behavior for built-in formats

4. **Update related constraint classes** (if any reference AutoTileFormat enum)
   - BitmaskConsistencyValidator
   - SpatialCoherenceConstraint (if applicable)

5. **Backward compatibility**
   - Ensure `"autoTileFormat": "corner16"` still works
   - Ensure `"autoTileFormat": "blob47"` still works
   - Test with existing tiles.json

**Acceptance Criteria**:
- [ ] TileDefinition.GetAutoTileFormat() returns correct definition
- [ ] AutoTileHelper.ComputeBitmask() works for all 3 built-in formats
- [ ] Custom format with missing bitmasks returns null for those values
- [ ] Existing tiles render correctly (no regression)
- [ ] WFC generation still works with built-in formats

**Files Modified**:
- MODIFY: `Scripts/Features/Deckbuilder/Tiles/TileDefinition.cs`
- MODIFY: `Scripts/Features/Worldgen/AutoTiling/AutoTileHelper.cs`
- MODIFY: `Scripts/Features/Worldgen/Wfc/Constraints/AutoTileGapConstraint.cs`
- MODIFY: `Scripts/Features/Worldgen/AutoTiling/BitmaskConsistencyValidator.cs` (if needed)
- DELETE or DEPRECATE: `Scripts/Features/Worldgen/AutoTiling/AutoTileFormat.cs` (enum)

**Run Command**: `/solve Stage 2: Integrate format registry into runtime. Update TileDefinition to use AutoTileFormatName string with registry lookup. Update AutoTileHelper to use BitmaskType for algorithm selection. Update WFC constraints to use AllowedBitmasks. See docs/FLEXIBLE_AUTOTILE_PLAN.md Stage 2 for details.`

---

### Stage 3: Multi-Cell Variants & Rendering

**Goal**: Support variants that occupy multiple cells with WFC reservation and correct rendering.

**Subtasks**:

1. **Add cell reservation to WfcCell** (`Scripts/Features/Worldgen/Wfc/WfcCell.cs` or similar)
   - Add `ReservedBy: Vector2I?` property (position of cell that reserved this one)
   - Add `IsReserved` property
   - Reserved cells should not be available for tile placement

2. **Update WFC solver for reservation** (`Scripts/Features/Worldgen/Wfc/WfcMapGenerator.cs`)
   - After collapsing a cell, check variant's Size and Offset
   - If Size > 1x1, calculate which cells are covered
   - Mark covered cells as reserved
   - Handle boundary conditions (don't reserve outside map)

3. **Update constraint evaluation for reserved cells**
   - Reserved cells excluded from entropy calculation
   - Constraints treat reserved cells appropriately

4. **Update SimpleMapGenerator rendering** (`Scripts/Features/Deckbuilder/Services/SimpleMapGenerator.cs`)
   - Use VariantDefinition.Size and Offset for tile placement
   - Apply offset when setting cell position
   - Handle AtlasRegionSize for non-standard source sizes

5. **Update TilesetConfig usage in rendering**
   - Apply BaseTileSize when calculating tile positions
   - Apply GridOffset to rendering coordinate system

6. **Z-ordering for tall variants**
   - Ensure tall variants render in correct order
   - Variants extending upward should render behind tiles above them

**Acceptance Criteria**:
- [ ] Variant with Size=1x3, Offset=0,-2 reserves 2 cells above placement
- [ ] Reserved cells are skipped during WFC collapse
- [ ] Tall variants render at correct position with offset applied
- [ ] No visual overlap/z-fighting issues
- [ ] Map boundaries handled correctly (no out-of-bounds reservation)

**Files Modified**:
- MODIFY: `Scripts/Features/Worldgen/Wfc/WfcCell.cs` (or equivalent)
- MODIFY: `Scripts/Features/Worldgen/Wfc/WfcMapGenerator.cs`
- MODIFY: `Scripts/Features/Deckbuilder/Services/SimpleMapGenerator.cs`
- MODIFY: Rendering-related files as needed

**Run Command**: `/solve Stage 3: Implement multi-cell variant support. Add cell reservation to WFC (ReservedBy property, mark cells covered by tall variants). Update rendering to use VariantDefinition.Size/Offset and TilesetConfig.BaseTileSize. See docs/FLEXIBLE_AUTOTILE_PLAN.md Stage 3 for details.`

---

### Stage 4: Editor UI - Format Management

**Goal**: Add UI for viewing, creating, and editing auto-tile format definitions.

**Subtasks**:

1. **Create AutoTileFormatEditorPanel** (`addons/tile_editor/AutoTileFormatEditorPanel.cs`)
   - New panel/tab in tile editor
   - List view of all formats from registry
   - Built-in formats shown but not editable/deletable
   - Add/Delete buttons for custom formats

2. **Create format properties editor**
   - Name text field
   - BitmaskType dropdown (Corner4, Edge4, Full8)
   - Displays current variant count

3. **Create BitmaskConfigGrid component** (`addons/tile_editor/BitmaskConfigGrid.cs`)
   - Visual grid showing all possible bitmask values
   - 16 cells for Corner4/Edge4, 47 for Blob (pre-filtered), expandable for Full8
   - Each cell shows neighbor diagram (which directions are set)
   - Checkbox to toggle allowed/disallowed
   - For Full8, group by edge count to reduce overwhelm

4. **Wire into TileEditorDock** (`addons/tile_editor/TileEditorDock.cs`)
   - Add "Formats" tab to tab container
   - Initialize with registry data

5. **Save custom formats to tiles.json**
   - TileEditorService saves autoTileFormats section
   - Only saves custom formats (not built-ins)

**Acceptance Criteria**:
- [ ] Formats tab visible in tile editor
- [ ] All registered formats listed
- [ ] Can create new custom format with name and type
- [ ] BitmaskConfigGrid shows correct patterns for bitmask type
- [ ] Can toggle bitmasks allowed/disallowed
- [ ] Changes saved to tiles.json

**Files Modified**:
- NEW: `addons/tile_editor/AutoTileFormatEditorPanel.cs`
- NEW: `addons/tile_editor/BitmaskConfigGrid.cs`
- MODIFY: `addons/tile_editor/TileEditorDock.cs`
- MODIFY: `addons/tile_editor/TileEditorService.cs`

**Run Command**: `/solve Stage 4: Create editor UI for auto-tile formats. Add AutoTileFormatEditorPanel with format list, add/delete buttons. Create BitmaskConfigGrid showing toggleable bitmask patterns. Wire into TileEditorDock as new tab. See docs/FLEXIBLE_AUTOTILE_PLAN.md Stage 4 for details.`

---

### Stage 5: Editor UI - Variant Mapping & Preview

**Goal**: Complete the editor with variant configuration and updated preview.

**Subtasks**:

1. **Create VariantMappingEditor component** (`addons/tile_editor/VariantMappingEditor.cs`)
   - List of allowed bitmasks (from BitmaskConfigGrid selection)
   - For each: atlas coord picker, size input (WxH), offset input (X,Y)
   - Preview thumbnail of variant texture
   - Validation (coords within atlas bounds)

2. **Update AutoTilePreviewPanel** (`addons/tile_editor/AutoTilePreviewPanel.cs`)
   - Format dropdown populated from registry (not hardcoded)
   - Respects AllowedBitmasks (only shows valid variants)
   - Renders tall variants at correct size/position
   - Shows variant metadata on hover (size, offset)

3. **Add TilesetConfig editor UI**
   - In toolbar or dedicated section
   - BaseTileSize input (16x16, 24x24, 32x32, custom)
   - GridOffset Vector2 input
   - Changes reflected immediately in preview

4. **Update TilePropertiesPanel** (`addons/tile_editor/TilePropertiesPanel.cs`)
   - Format dropdown populated from registry
   - Shows format details when selected

5. **Update EditableTile for new properties**
   - Store format name instead of enum string
   - Store variant definitions if custom

**Acceptance Criteria**:
- [ ] Can configure atlas coords, size, offset per variant
- [ ] Preview panel shows custom formats correctly
- [ ] Tall variants preview at correct proportions
- [ ] TilesetConfig changes affect preview
- [ ] All changes persist to tiles.json

**Files Modified**:
- NEW: `addons/tile_editor/VariantMappingEditor.cs`
- MODIFY: `addons/tile_editor/AutoTilePreviewPanel.cs`
- MODIFY: `addons/tile_editor/TilePropertiesPanel.cs`
- MODIFY: `addons/tile_editor/TileEditorService.cs`
- MODIFY: `addons/tile_editor/EditableTile.cs`

**Run Command**: `/solve Stage 5: Complete editor UI. Create VariantMappingEditor for per-variant atlas/size/offset config. Update AutoTilePreviewPanel for custom formats and tall variants. Add TilesetConfig UI. See docs/FLEXIBLE_AUTOTILE_PLAN.md Stage 5 for details.`

---

### Stage 6: Tests & Documentation

**Goal**: Comprehensive test coverage and documentation.

**Subtasks**:

1. **Unit tests for data model**
   - VariantDefinition construction
   - AutoTileFormatDefinition validation
   - Registry operations

2. **Unit tests for runtime**
   - AutoTileHelper with custom formats
   - BitmaskType algorithm selection
   - Disallowed bitmask handling

3. **Integration tests for WFC**
   - Cell reservation with tall variants
   - Constraint behavior with custom formats
   - Map generation with mixed formats

4. **JSON serialization tests**
   - Round-trip for custom formats
   - Legacy format migration
   - TilesetConfig parsing

5. **Update WORLDGEN_ARCHITECTURE.md**
   - Document new format system
   - Add examples of custom formats
   - Explain cell reservation

**Acceptance Criteria**:
- [ ] >80% coverage on new code
- [ ] All tests pass
- [ ] Documentation updated

**Files Modified**:
- NEW: `Tests/Features/Worldgen/AutoTiling/AutoTileFormatDefinitionTest.cs`
- NEW: `Tests/Features/Worldgen/AutoTiling/AutoTileFormatRegistryTest.cs`
- NEW: `Tests/Features/Worldgen/AutoTiling/VariantDefinitionTest.cs`
- NEW: `Tests/Features/Worldgen/Wfc/CellReservationTest.cs`
- MODIFY: `docs/WORLDGEN_ARCHITECTURE.md`

**Run Command**: `/solve Stage 6: Write comprehensive tests for flexible auto-tile system. Cover data model, runtime integration, WFC cell reservation, JSON serialization. Update WORLDGEN_ARCHITECTURE.md. See docs/FLEXIBLE_AUTOTILE_PLAN.md Stage 6 for details.`

---

## Dependency Graph

```
Stage 1 (Core Data Model)
    ↓
Stage 2 (Runtime Integration)
    ↓
Stage 3 (Multi-Cell Variants)
    ↓
Stage 4 (Editor UI - Formats)
    ↓
Stage 5 (Editor UI - Variants)
    ↓
Stage 6 (Tests & Docs)
```

Each stage builds on the previous. Stage 4-5 can potentially run in parallel after Stage 3.

## Risk Mitigation

| Risk | Mitigation |
|------|------------|
| Cell reservation perf | Profile WFC, optimize if >10% slowdown |
| Full8 UI overwhelm | Group by edge count, add search/filter |
| Z-order issues | Use Y-sort or explicit z-index |
| Legacy data loss | Extensive backward compat testing |
| Atlas compilation | Defer AtlasRegionSize to future if complex |

## Success Metrics

1. Can define "hedge4" format that forbids interior (bitmask 15)
2. Can define "wall_south" with 3-tile-tall variants
3. Can load 24x24 tileset with GridOffset
4. Existing tiles.json loads without modification
5. WFC generates valid maps with custom formats
