#if TOOLS
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using Godot;
using JsonTileData = CardCleaner.Addons.TileEditor.TileEditorJsonModels.TileData;
using static CardCleaner.Addons.TileEditor.TileEditorJsonModels;

namespace CardCleaner.Addons.TileEditor;

public partial class TileEditorService
{
    public TileEditorOperationResult SaveTiles()
    {
        return SaveTiles(compileAtlas: true);
    }

    public TileEditorOperationResult SaveTiles(bool compileAtlas)
    {
        foreach (var tile in _tiles.Values)
        {
            var (valid, message) = ValidateTile(tile);
            if (!valid)
                return new TileEditorOperationResult(
                    false,
                    $"Tile '{tile.Id}': {message}");
        }

        try
        {
            WriteTileRegistry(BuildRegistryData());
            GD.Print($"[TileEditorService] Saved {_tiles.Count} tiles to {TilesPath}");
            if (compileAtlas)
            {
                var atlasResult = SaveAtlasAfterTiles();
                return atlasResult;
            }

            return new TileEditorOperationResult(true, "Saved successfully");
        }
        catch (Exception ex)
        {
            GD.PrintErr($"[TileEditorService] Error saving tiles: {ex.Message}");
            return new TileEditorOperationResult(false, ex.Message);
        }
    }

    private TileRegistryData BuildRegistryData()
    {
        return new TileRegistryData
        {
            Schema = "./tiles.schema.json",
            Version = _version,
            Tileset = TilesetPath,
            Biomes = BuildBiomeData(),
            AutoTileFormats = BuildAutoTileFormatData(),
            Tiles = _tiles.Values.Select(BuildTileData).ToList()
        };
    }

    private Dictionary<string, BiomeDataJson>? BuildBiomeData()
    {
        if (_biomes.Count == 0)
            return null;

        return _biomes.ToDictionary(
            pair => pair.Key,
            pair => new BiomeDataJson
            {
                DisplayName = pair.Value.DisplayName,
                Signature = pair.Value.Signature,
                BlockedPercentage = pair.Value.BlockedPercentage,
                PassableTiles = NormalizeWeights(pair.Value.PassableTiles),
                BlockedTiles = NormalizeWeights(pair.Value.BlockedTiles)
            });
    }

    private List<AutoTileFormatDataJson>? BuildAutoTileFormatData()
    {
        if (_customAutoTileFormats.Count == 0)
            return null;

        return _customAutoTileFormats.Select(BuildAutoTileFormatData).ToList();
    }

    private static AutoTileFormatDataJson BuildAutoTileFormatData(EditableAutoTileFormat format)
    {
        var nonDefaultSizes = format.VariantMappings
            .Where(pair => pair.Value.SizeX != 1 || pair.Value.SizeY != 1)
            .ToDictionary(
                pair => pair.Key.ToString(),
                pair => new Vector2IData
                {
                    X = pair.Value.SizeX,
                    Y = pair.Value.SizeY
                });

        return new AutoTileFormatDataJson
        {
            Name = format.Name,
            BitmaskType = format.BitmaskType switch
            {
                Features.Worldgen.AutoTiling.BitmaskType.Edge4 => "edge4",
                Features.Worldgen.AutoTiling.BitmaskType.Full8 => "full8",
                _ => "corner4"
            },
            AllowedBitmasks = format.AllowedBitmasks.OrderBy(bitmask => bitmask).ToArray(),
            VariantSizes = nonDefaultSizes.Count > 0 ? nonDefaultSizes : null
        };
    }

    private static JsonTileData BuildTileData(EditableTile tile)
    {
        return new JsonTileData
        {
            Id = tile.Id,
            Name = tile.Name,
            Passability = tile.Passability.ToLowerInvariant(),
            AtlasCoords = new Vector2IData { X = tile.AtlasX, Y = tile.AtlasY },
            SourceId = tile.SourceId,
            Layer = tile.Layer.ToLowerInvariant(),
            Elevation = tile.Elevation,
            IsTransparent = tile.IsTransparent,
            Biomes = tile.Biomes.Count > 0 ? tile.Biomes : null,
            Size = tile.SizeX != 1 || tile.SizeY != 1
                ? new Vector2IData { X = tile.SizeX, Y = tile.SizeY }
                : null,
            SourceScale = tile.SourceScale != 1.0f ? tile.SourceScale : null,
            AutoTileVariants = BuildAutoTileVariants(tile),
            DecorationDensity = tile.DecorationDensity < 1.0f
                ? tile.DecorationDensity
                : null,
            AutoTileFormat = tile.AutoTileFormat != "corner16"
                ? tile.AutoTileFormat
                : null,
            Variations = tile.HasVariations
                ? tile.Variations!.Select(ToVector2IData).ToArray()
                : null,
            VariationMode = tile.VariationMode != "perinstance"
                ? tile.VariationMode
                : null,
            Animation = BuildAnimationData(tile),
            TileMode = tile.TileMode != "plain" ? tile.TileMode : null,
            Dominance = tile.Dominance,
            InnerTerrain = tile.InnerTerrainId,
            OuterTerrain = tile.OuterTerrainId,
            Description = tile.Description
        };
    }

    private static Vector2IData?[]? BuildAutoTileVariants(EditableTile tile)
    {
        if (!tile.HasAutoTileVariants)
            return null;

        return tile.AutoTileVariants!
            .Select(variant => variant.HasValue
                ? new Vector2IData { X = variant.Value.X, Y = variant.Value.Y }
                : null)
            .ToArray();
    }

    private static AnimationData? BuildAnimationData(EditableTile tile)
    {
        if (!tile.HasAnimation)
            return null;

        return new AnimationData
        {
            Frames = tile.AnimationFrames!.Select(ToVector2IData).ToArray(),
            FrameDuration = tile.AnimationFrameDuration
        };
    }

    private static Vector2IData ToVector2IData(Vector2I coordinates)
    {
        return new Vector2IData { X = coordinates.X, Y = coordinates.Y };
    }

    private static Dictionary<string, float> NormalizeWeights(Dictionary<string, float> weights)
    {
        if (weights.Count == 0)
            return weights;

        var sum = weights.Values.Sum();
        if (sum <= 0)
            return weights;

        return weights.ToDictionary(
            pair => pair.Key,
            pair => (float)Math.Round(pair.Value / sum, 2));
    }

    private static void WriteTileRegistry(TileRegistryData data)
    {
        var json = JsonSerializer.Serialize(data, CreateWriteOptions());
        var absolutePath = ProjectSettings.GlobalizePath(TilesPath);
        File.WriteAllText(absolutePath, json);
    }

    private TileEditorOperationResult SaveAtlasAfterTiles()
    {
        var atlasResult = CompileAtlas();
        if (atlasResult.Success)
            return new TileEditorOperationResult(true, "Saved successfully");

        GD.PrintErr(
            $"[TileEditorService] Atlas compilation failed: {atlasResult.Message}");
        return new TileEditorOperationResult(
            true,
            $"Saved tiles, but atlas compilation failed: {atlasResult.Message}");
    }
}
#endif
