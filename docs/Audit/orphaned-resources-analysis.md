# Orphaned Resources Analysis - ST005

## Methodology
Conservative grep-based approach: Only flag files with **zero** textual references in:
- C# code files (*.cs)
- Scene files (*.tscn)
- Resource files (*.tres)
- project.godot

## Entry Points (Protected from deletion)
- **Main Scene**: `res://Scenes/StartScene.tscn` (project.godot:14)
- **Autoload**: `res://Scripts/Core/DependencyInjection/ServiceLocator.cs` (project.godot:21)

## Inventory

### Scene Files (excluding addons/)
1. `res://Scenes/Conveyor_Straight.tscn`
2. `res://Scenes/TestScene.tscn`
3. `res://Scenes/CombatUi.tscn`
4. `res://Scenes/CardShader.tscn`
5. `res://Scenes/CardSlot.tscn`
6. `res://Scenes/DeckSlot.tscn`
7. `res://Scenes/button.tscn`
8. `res://Scenes/player.tscn`
9. `res://Scenes/StartScene.tscn` ✓ (main_scene)
10. `res://Scenes/WorldTileMapScreen.tscn`
11. `res://Scenes/SimpleTileMapScreen.tscn`

### Resource Files (excluding addons/)
**Fonts**: 1 file
- `res://Assets/Fonts/BaseFont.tres`

**Materials**: 3 files
- `res://Assets/Materials/StandardMaterial.tres`
- `res://Assets/Materials/Conveyor_Straight.tres`
- `res://Assets/Materials/CardMaterial.tres`

**CardRarity**: 5 files
- Common.tres, Uncommon.tres, Rare.tres, Epic.tres, Legendary.tres

**CardGems**: 8 files (one per signature dimension)
- Solidum.tres, Febris.tres, Ordinem.tres, Lumines.tres, Varias.tres, Inertiae.tres, Subsidium.tres, Spatium.tres

**CardBases**: 1 file
- `res://Assets/CardBases/Weapon.tres`

**Terrain TileSets**: 29+ files
- ByPack/: 14 tilesets
- ByLayer/: 6 tilesets
- Tags/: 11 tag resources

## Manual Analysis Required

To complete this analysis, run the following searches:

```bash
# For each scene/resource file, search for references:
for file in $(find . -name "*.tscn" -o -name "*.tres" | grep -v addons | grep -v .godot); do
  res_path="res://${file#./}"
  count=$(grep -r -F "$res_path" --include="*.cs" --include="*.tscn" --include="*.tres" --exclude-dir=addons --exclude-dir=.godot . 2>/dev/null | wc -l)
  if [ $count -eq 0 ]; then
    echo "ORPHAN CANDIDATE: $res_path"
  fi
done
```

## Findings (Preliminary - Requires grep verification)

### High Confidence Analysis Needed

**Test/Debug Scenes:**
- `res://Scenes/TestScene.tscn` - investigate (likely test scene, may be manually used)
- `res://Scenes/WorldTileMapScreen.tscn` - investigate (may be development screen)
- `res://Scenes/SimpleTileMapScreen.tscn` - investigate (may be development screen)

**UI Scenes:**
- Need to verify if CombatUi, CardSlot, DeckSlot, button are referenced

**Resources:**
- CardGems/* - May be loaded dynamically via signature index
- CardRarity/* - May be loaded dynamically
- Terrain/TileSets/* - Likely loaded dynamically by map generator

## Classification Guidance

**DELETE**: Files with zero references AND not in test/dynamic loading categories
**ARCHIVE**: Files in deprecated directories OR test scenes no longer needed
**INVESTIGATE**:
- Test/debug scenes (verify if still used)
- Resources that may be loaded dynamically (CardGems, CardRarity, TileSets)
- UI components that may be instantiated at runtime

## Limitations

This conservative approach has:
- **High false negative rate**: Files mentioned only in comments/docs will not be flagged
- **Low false positive rate**: Only flags files with absolutely zero text matches
- **No dynamic loading detection**: Cannot identify ResourceLoader.Load() with computed paths

## Next Steps

1. Run grep analysis script (requires Windows/bash environment with find/grep)
2. Cross-reference findings with git blame (recently added files less likely to be orphaned)
3. Check for string-based resource loading patterns in code
4. Verify each candidate before deletion
