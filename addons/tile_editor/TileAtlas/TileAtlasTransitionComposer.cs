#if TOOLS
using System;
using System.Collections.Generic;
using CardCleaner.Scripts.Features.Worldgen.AutoTiling;
using Godot;
using static CardCleaner.Addons.TileEditor.TileAtlasCompilerModels;

namespace CardCleaner.Addons.TileEditor;

internal sealed class TileAtlasTransitionComposer
{
    internal TileAtlasCompositionResult Generate(TileAtlasCompositionInput input)
    {
        var state = new CompositeAtlasState
        {
            Atlas = input.Atlas,
            CurrentY = input.PackedAtlasSize.Y,
            RowHeight = input.TileSize.Y,
            AtlasWidth = input.AtlasWidth,
            TileSize = input.TileSize.X
        };
        var transitionMap = new CompiledTransitionMap();

        GenerateCompositableTransitions(input, state, transitionMap);
        AddFixedTransitions(input.FixedAutoTiles, input.PackResult.Mapping, transitionMap);

        return new TileAtlasCompositionResult
        {
            Atlas = state.Atlas,
            TransitionMap = transitionMap,
            CompositeCount = state.CompositeCount,
            CurrentX = state.CurrentX,
            CurrentY = state.CurrentY,
            RowHeight = state.RowHeight
        };
    }

    private static void GenerateCompositableTransitions(
        TileAtlasCompositionInput input,
        CompositeAtlasState state,
        CompiledTransitionMap transitionMap)
    {
        foreach (var borderTile in input.CompositableAutoTiles)
        {
            var borderSource = input.TileSet.GetSource(borderTile.SourceId)
                as TileSetAtlasSource;
            if (borderSource?.Texture == null)
            {
                GD.PrintErr(
                    $"[TileAtlasCompiler] Missing source {borderTile.SourceId} "
                    + $"for {borderTile.Id}");
                continue;
            }

            var borderSourceImage = borderSource.Texture.GetImage();
            foreach (var baseTerrain in input.BaseTerrains)
            {
                if (baseTerrain.Id == borderTile.Id)
                    continue;

                var baseSource = input.TileSet.GetSource(baseTerrain.SourceId)
                    as TileSetAtlasSource;
                if (baseSource?.Texture == null)
                    continue;

                var baseImage = TileAtlasImageOperations.ExtractTileRegion(
                    baseSource.Texture.GetImage(),
                    GetBaseAtlasX(baseTerrain),
                    GetBaseAtlasY(baseTerrain),
                    input.TileSize.X,
                    baseTerrain.SourceScale);
                var variantCoords = GenerateVariants(
                    borderTile,
                    borderSourceImage,
                    baseImage,
                    state);
                transitionMap.AddTransition(
                    borderTile.Id,
                    baseTerrain.Id,
                    borderTile.AutoTileFormat,
                    variantCoords);
            }
        }
    }

    private static Vector2I[] GenerateVariants(
        EditableTile borderTile,
        Image borderSourceImage,
        Image baseImage,
        CompositeAtlasState state)
    {
        var variantCoords = new Vector2I[borderTile.ExpectedVariantCount];
        for (var index = 0; index < variantCoords.Length; index++)
        {
            var borderCoords = GetBorderCoordinates(borderTile, index);
            var borderImage = TileAtlasImageOperations.ExtractTileRegion(
                borderSourceImage,
                borderCoords.X,
                borderCoords.Y,
                state.TileSize,
                borderTile.SourceScale);
            var compositeImage = TileAtlasImageOperations.CompositeImages(
                baseImage,
                borderImage);
            if (!TryWriteComposite(state, compositeImage, out var atlasCoords))
                break;

            variantCoords[index] = atlasCoords;
        }

        return variantCoords;
    }

    private static bool TryWriteComposite(
        CompositeAtlasState state,
        Image compositeImage,
        out Vector2I atlasCoords)
    {
        atlasCoords = Vector2I.Zero;
        if (state.CurrentX + state.TileSize > state.AtlasWidth)
        {
            state.CurrentX = 0;
            state.CurrentY += state.RowHeight;
        }

        if (state.CurrentY + state.TileSize > TileAtlasCompilerConstants.MaxAtlasSize)
        {
            GD.PrintErr("[TileAtlasCompiler] Atlas size exceeded during composite generation");
            return false;
        }

        if (state.CurrentY + state.TileSize > state.Atlas.GetHeight())
        {
            var newHeight = Math.Min(
                state.Atlas.GetHeight() * 2,
                TileAtlasCompilerConstants.MaxAtlasSize);
            state.Atlas = ExpandAtlas(state.Atlas, newHeight);
        }

        state.Atlas.BlitRect(
            compositeImage,
            new Rect2I(0, 0, state.TileSize, state.TileSize),
            new Vector2I(state.CurrentX, state.CurrentY));
        atlasCoords = new Vector2I(
            state.CurrentX / state.TileSize,
            state.CurrentY / state.TileSize);
        state.CurrentX += state.TileSize;
        state.CompositeCount++;
        return true;
    }

    private static Image ExpandAtlas(Image atlas, int newHeight)
    {
        var expandedAtlas = Image.CreateEmpty(
            atlas.GetWidth(),
            newHeight,
            false,
            Image.Format.Rgba8);
        expandedAtlas.Fill(new Color(0, 0, 0, 0));
        expandedAtlas.BlitRect(
            atlas,
            new Rect2I(0, 0, atlas.GetWidth(), atlas.GetHeight()),
            Vector2I.Zero);
        return expandedAtlas;
    }

    private static int GetBaseAtlasX(EditableTile baseTerrain)
    {
        return GetSolidFillCoordinates(baseTerrain)?.X ?? baseTerrain.AtlasX;
    }

    private static int GetBaseAtlasY(EditableTile baseTerrain)
    {
        return GetSolidFillCoordinates(baseTerrain)?.Y ?? baseTerrain.AtlasY;
    }

    private static Vector2I? GetSolidFillCoordinates(EditableTile tile)
    {
        if (!tile.HasAutoTileVariants || tile.AutoTileVariants == null)
            return null;
        if (tile.AutoTileVariants.Length <= 15 || !tile.AutoTileVariants[15].HasValue)
            return null;

        return tile.AutoTileVariants[15]!.Value;
    }

    private static Vector2I GetBorderCoordinates(EditableTile tile, int index)
    {
        if (tile.AutoTileVariants != null
            && index < tile.AutoTileVariants.Length
            && tile.AutoTileVariants[index].HasValue)
        {
            return tile.AutoTileVariants[index]!.Value;
        }

        return new Vector2I(tile.AtlasX, tile.AtlasY);
    }

    private static void AddFixedTransitions(
        List<EditableTile> fixedTiles,
        Dictionary<string, AtlasTileMapping> mapping,
        CompiledTransitionMap transitionMap)
    {
        foreach (var fixedTile in fixedTiles)
        {
            if (string.IsNullOrEmpty(fixedTile.OuterTerrainId))
                continue;

            var variantCoords = BuildFixedVariantCoordinates(fixedTile, mapping);
            transitionMap.AddTransition(
                fixedTile.Id,
                fixedTile.OuterTerrainId,
                fixedTile.AutoTileFormat,
                variantCoords);
        }
    }

    private static Vector2I[] BuildFixedVariantCoordinates(
        EditableTile tile,
        Dictionary<string, AtlasTileMapping> mapping)
    {
        var variantCoords = new Vector2I[tile.ExpectedVariantCount];
        for (var index = 0; index < variantCoords.Length; index++)
            variantCoords[index] = ResolveFixedVariant(tile, index, mapping);
        return variantCoords;
    }

    private static Vector2I ResolveFixedVariant(
        EditableTile tile,
        int index,
        Dictionary<string, AtlasTileMapping> mapping)
    {
        if (tile.AutoTileVariants != null
            && index < tile.AutoTileVariants.Length
            && tile.AutoTileVariants[index].HasValue)
        {
            var coordinates = tile.AutoTileVariants[index]!.Value;
            var sourceKey = tile.SourceId.ToString();
            var coordinateKey = $"{coordinates.X},{coordinates.Y}";
            if (mapping.TryGetValue(sourceKey, out var sourceMapping)
                && sourceMapping.TryGetValue(coordinateKey, out var rect))
            {
                return new Vector2I(rect.X, rect.Y);
            }
        }

        return new Vector2I(tile.AtlasX, tile.AtlasY);
    }
}
#endif
