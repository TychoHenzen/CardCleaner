#if TOOLS
using System.Collections.Generic;
using System.Linq;
using CardCleaner.Scripts.Features.Worldgen.AutoTiling;
using Godot;
using static CardCleaner.Addons.TileEditor.TmxAtlasCompilerModels;

namespace CardCleaner.Addons.TileEditor;

internal sealed class TmxTransitionComposer
{
    private readonly TmxAtlasPacker _packer;

    internal TmxTransitionComposer(TmxAtlasPacker packer)
    {
        _packer = packer;
    }

    internal CompositionResult Generate(CompositionInput input)
    {
        var state = new CompositeAtlasState
        {
            Atlas = input.Atlas,
            CurrentY = input.PackedAtlasSize.Y,
            RowHeight = input.TargetTileSize,
            AtlasWidth = input.AtlasWidth,
            TargetTileSize = input.TargetTileSize
        };
        var transitionMap = new CompiledTransitionMap();

        foreach (var wangSet in input.CompositableWangSets)
            GenerateCompositableSet(wangSet, input.BaseTerrains, state, transitionMap);

        foreach (var wangSet in input.FixedWangSets)
            AddFixedWangSet(wangSet, input.PackResult.Mapping, transitionMap);

        return new CompositionResult
        {
            Atlas = state.Atlas,
            TransitionMap = transitionMap,
            CompositeCount = state.CompositeCount,
            CurrentX = state.CurrentX,
            CurrentY = state.CurrentY,
            RowHeight = state.RowHeight
        };
    }

    private void GenerateCompositableSet(
        TsxWangSetData wangSet,
        List<TsxTileData> baseTerrains,
        CompositeAtlasState state,
        CompiledTransitionMap transitionMap)
    {
        var wangSetId = TmxAtlasNames.ToSnakeCase(wangSet.Name);
        foreach (var baseTerrain in baseTerrains)
        {
            if (baseTerrain.Id == wangSetId)
                continue;

            var baseImage = _packer.ExtractTileRegion(baseTerrain, state.TargetTileSize);
            var borderImages = new Dictionary<int, Image>();
            foreach (var tileIds in wangSet.WangTiles.Values)
            {
                foreach (var tileId in tileIds)
                {
                    if (!borderImages.ContainsKey(tileId))
                    {
                        borderImages[tileId] = _packer.ExtractTileRegion(
                            wangSet,
                            tileId,
                            state.TargetTileSize);
                    }
                }
            }

            var variantCoords = CreateVariantCoordinates();
            for (var bitmask = 0; bitmask < 16; bitmask++)
            {
                GenerateBitmaskVariants(
                    wangSet,
                    baseImage,
                    borderImages,
                    bitmask,
                    variantCoords,
                    state);
            }

            GenerateDiagonalIfMissing(
                wangSet,
                baseTerrain.Id,
                baseImage,
                borderImages,
                5,
                1,
                4,
                variantCoords,
                state);
            GenerateDiagonalIfMissing(
                wangSet,
                baseTerrain.Id,
                baseImage,
                borderImages,
                10,
                8,
                2,
                variantCoords,
                state);
            transitionMap.AddTransitionWithVariants(
                wangSetId,
                baseTerrain.Id,
                "corner16",
                variantCoords.Select(list => list.ToArray()).ToArray());
        }
    }

    private void GenerateBitmaskVariants(
        TsxWangSetData wangSet,
        Image baseImage,
        Dictionary<int, Image> borderImages,
        int bitmask,
        List<Vector2I>[] variantCoords,
        CompositeAtlasState state)
    {
        if (wangSet.WangTiles.TryGetValue(bitmask, out var borderTileIds)
            && borderTileIds.Count > 0)
        {
            foreach (var borderTileId in borderTileIds)
            {
                var borderImage = borderImages[borderTileId];
                var compositeImage = TmxAtlasPacker.CompositeImages(baseImage, borderImage);
                AddPackedVariant(compositeImage, bitmask, variantCoords, state);
            }

            return;
        }

        AddPackedVariant(baseImage, bitmask, variantCoords, state);
    }

    private void GenerateDiagonalIfMissing(
        TsxWangSetData wangSet,
        string baseTerrainId,
        Image baseImage,
        Dictionary<int, Image> borderImages,
        int targetBitmask,
        int northCornerBitmask,
        int southCornerBitmask,
        List<Vector2I>[] variantCoords,
        CompositeAtlasState state)
    {
        if (wangSet.WangTiles.TryGetValue(targetBitmask, out var existingTiles)
            && existingTiles.Count > 0)
            return;
        if (!TryGetFirstTile(wangSet, northCornerBitmask, out var northTileId)
            || !TryGetFirstTile(wangSet, southCornerBitmask, out var southTileId))
            return;

        variantCoords[targetBitmask].Clear();
        var northImage = borderImages[northTileId];
        var southImage = borderImages[southTileId];
        var intermediate = TmxAtlasPacker.CompositeImages(baseImage, northImage);
        var compositeImage = TmxAtlasPacker.CompositeImages(intermediate, southImage);
        AddPackedVariant(compositeImage, targetBitmask, variantCoords, state);

        GD.Print(
            $"[TmxAtlasCompiler] Generated diagonal bitmask {targetBitmask} "
            + $"for {wangSet.Name} on {baseTerrainId}");
    }

    private static bool TryGetFirstTile(
        TsxWangSetData wangSet,
        int bitmask,
        out int tileId)
    {
        if (wangSet.WangTiles.TryGetValue(bitmask, out var tileIds) && tileIds.Count > 0)
        {
            tileId = tileIds[0];
            return true;
        }

        tileId = 0;
        return false;
    }

    private void AddPackedVariant(
        Image compositeImage,
        int bitmask,
        List<Vector2I>[] variantCoords,
        CompositeAtlasState state)
    {
        var result = _packer.PackCompositeToAtlas(
            state.Atlas,
            compositeImage,
            state.TargetTileSize,
            state.CurrentX,
            state.CurrentY,
            state.RowHeight,
            state.AtlasWidth);
        state.Atlas = result.Atlas;
        state.CurrentX = result.CurrentX + state.TargetTileSize;
        state.CurrentY = result.CurrentY;
        variantCoords[bitmask].Add(new Vector2I(
            result.CurrentX / state.TargetTileSize,
            result.CurrentY / state.TargetTileSize));
        state.CompositeCount++;
    }

    private static void AddFixedWangSet(
        TsxWangSetData wangSet,
        Dictionary<string, Dictionary<string, TileAtlasRect>> mapping,
        CompiledTransitionMap transitionMap)
    {
        var variantCoords = CreateVariantCoordinates();
        var hasAnyVariants = false;
        for (var bitmask = 0; bitmask < 16; bitmask++)
        {
            if (!wangSet.WangTiles.TryGetValue(bitmask, out var tileIds)
                || tileIds.Count == 0)
                continue;

            foreach (var tileId in tileIds)
                hasAnyVariants |= AddMappedVariant(wangSet, tileId, variantCoords[bitmask], mapping);
        }

        if (!hasAnyVariants)
            return;

        var outerTerrain = string.IsNullOrEmpty(wangSet.OuterTerrain)
            ? "*"
            : wangSet.OuterTerrain;
        transitionMap.AddTransitionWithVariants(
            TmxAtlasNames.ToSnakeCase(wangSet.Name),
            outerTerrain,
            "corner16",
            variantCoords.Select(list => list.ToArray()).ToArray());
    }

    private static bool AddMappedVariant(
        TsxWangSetData wangSet,
        int tileId,
        List<Vector2I> variants,
        Dictionary<string, Dictionary<string, TileAtlasRect>> mapping)
    {
        var atlasX = tileId % wangSet.Columns;
        var atlasY = tileId / wangSet.Columns;
        var coordKey = $"{atlasX},{atlasY}";
        if (!mapping.TryGetValue(wangSet.TsxPath, out var sourceMapping)
            || !sourceMapping.TryGetValue(coordKey, out var rect))
            return false;

        variants.Add(new Vector2I(rect.X, rect.Y));
        return true;
    }

    private static List<Vector2I>[] CreateVariantCoordinates()
    {
        var variantCoords = new List<Vector2I>[16];
        for (var i = 0; i < variantCoords.Length; i++)
            variantCoords[i] = new List<Vector2I>();
        return variantCoords;
    }
}
#endif
