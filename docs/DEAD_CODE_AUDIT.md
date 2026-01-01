# Dead Code Audit Report - CardCleaner

**Date**: 2026-01-01
**Scope**: Production code (Scripts/) only used in tests (Tests/) or documentation

## Executive Summary

- **Total Production Files**: 147 .cs files
- **Total Test Files**: 102 .cs files
- **Critical Findings**: 3 entire subsystems are test/docs-only
- **Orphaned Services**: 1 service registered but never resolved
- **WFC System**: CONFIRMED INTEGRATED (not dead code)

---

## CRITICAL: Entire Subsystems Only Used in Tests/Docs

### 1. WeightModifiers System (10 files)
**Location**: `Scripts/Features/Worldgen/WeightModifiers/`

| File | Status |
|------|--------|
| IWeightModifier.cs | Dead - interface only used by siblings |
| WeightModifierPipeline.cs | Dead - only in README.md |
| WeightedTileSelector.cs | Dead - only in README.md |
| BiomeAffinityModifier.cs | Dead - never instantiated |
| AdjacencyBoostModifier.cs | Dead - never instantiated |
| DecorationSpacingModifier.cs | Dead - never instantiated |
| BiomeTileAffinity.cs | Dead - [GlobalClass] no .tres |
| TileAffinityEntry.cs | Dead - [GlobalClass] no .tres |
| TileSelectionContext.cs | Dead - only passed to unused |
| Examples/NoiseVariationModifier.cs | Dead - example code |
| StructureProximityModifier.cs | Dead - used by dead StructurePlacer |

### 2. VariantModifiers System (6 files)
**Location**: `Scripts/Features/Worldgen/VariantModifiers/`

| File | Status |
|------|--------|
| IVariantWeightModifier.cs | Dead |
| VariantWeightPipeline.cs | Dead |
| WeightedVariantSelector.cs | Dead |
| BiomeVariantModifier.cs | Dead |
| ProximityVariantModifier.cs | Dead |
| VariantSelectionContext.cs | Dead |

### 3. Structures System (6 files)
**Location**: `Scripts/Features/Worldgen/Structures/`

| File | Status |
|------|--------|
| StructurePlacer.cs | Dead - only in tests |
| StructureStamp.cs | Dead - [GlobalClass] no .tres |
| StructureResult.cs | Dead - only used by dead Placer |
| StructureTileEntry.cs | Dead - [GlobalClass] no .tres |
| IProceduralStructure.cs | Dead - never implemented |
| IMapQuery.cs | Dead - never implemented |

---

## MEDIUM: Orphaned Services & Classes

### CompatibilityTagRegistry
- **File**: `Scripts/Core/Services/CompatibilityTagRegistry.cs`
- **Issue**: Registered but never resolved
- **Note**: CompatibilityTag.cs itself IS used (.tres files exist)

### TilePlacement
- **File**: `Scripts/Features/Worldgen/TilePlacement.cs`
- **Issue**: [GlobalClass] with no .tres files

### AutoTileHelper (partial)
- **File**: `Scripts/Features/Worldgen/AutoTiling/AutoTileHelper.cs`
- **Unused Methods**: ApplyToTileMap(), GetAutoTileCoords(), ComputeBitmask()

---

## CONFIRMED: WFC System is Integrated

Production call chain:
```
GameSessionService.GenerateMap()
  -> new WfcMapGenerator(transitionResolver)
  -> SimpleMapGenerator receives WfcMapGenerator
  -> GenerateTerrainViaWfc()
  -> WfcMapGenerator.GenerateMultiBiome()
```

All 8 WFC files (WfcGrid, WfcCellState, WfcPropagator, WfcSolver, WfcTileSelector, WfcAdjacencyRules, WfcMapGenerator, WfcMapDataAdapter) are production code.

---

## Recommended Cleanup

### Safe to Delete
- `Scripts/Features/Worldgen/VariantModifiers/` (entire folder)
- `Scripts/Features/Worldgen/WeightModifiers/` (entire folder)
- `Scripts/Features/Worldgen/Structures/` (entire folder)
- `Scripts/Features/Worldgen/TilePlacement.cs`
- Related test files

### Service Cleanup
- Remove ICompatibilityTagRegistry registration from ServiceLocator.cs

### SimpleMapGenerator Cleanup
- Remove _structurePlacer field and code paths
- Remove _variantSelector field and code paths
- Remove AddStructureStamp() and ClearStructureStamps() methods
- Simplify constructor

### Documentation Cleanup
- Update WORLDGEN_ARCHITECTURE.md to remove references to deleted systems
