#if TOOLS
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;
using Godot;
using JsonTileData = CardCleaner.Addons.TileEditor.TileEditorJsonModels.TileData;
using static CardCleaner.Addons.TileEditor.TileEditorJsonModels;

namespace CardCleaner.Addons.TileEditor;

public partial class TileEditorService
{
    private static JsonSerializerOptions CreateJsonOptions() => new()
    {
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true
    };

    private static JsonSerializerOptions CreateWriteOptions() => new()
    {
        WriteIndented = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    public void LoadTiles()
    {
        ResetLoadedData();
        var absolutePath = ProjectSettings.GlobalizePath(TilesPath);
        if (!File.Exists(absolutePath))
        {
            GD.PrintErr($"[TileEditorService] File not found: {absolutePath}");
            EmitSignal(SignalName.TilesLoaded);
            return;
        }

        try
        {
            var data = JsonSerializer.Deserialize<TileRegistryData>(
                File.ReadAllText(absolutePath),
                CreateJsonOptions());
            if (data?.Tiles == null)
            {
                GD.PrintErr("[TileEditorService] Invalid JSON structure");
                EmitSignal(SignalName.TilesLoaded);
                return;
            }

            _version = data.Version ?? "1.0";
            TilesetPath = data.Tileset ?? DefaultTilesetPath;
            LoadTileSet();
            LoadTileDefinitions(data.Tiles);
            GD.Print($"[TileEditorService] Loaded {_tiles.Count} tiles");
            LoadBiomes(data.Biomes);
            LoadCustomAutoTileFormats(data.AutoTileFormats);
            EmitLoadSignals();
        }
        catch (Exception ex)
        {
            GD.PrintErr($"[TileEditorService] Error loading tiles: {ex.Message}");
            EmitLoadSignals();
        }
    }

    private void ResetLoadedData()
    {
        _tiles.Clear();
        _biomes.Clear();
        _customAutoTileFormats.Clear();
        Features.Worldgen.AutoTiling.AutoTileFormatRegistry.ClearCustomFormats();
    }

    private void LoadTileSet()
    {
        if (ResourceLoader.Exists(TilesetPath))
        {
            _tileSet = ResourceLoader.Load<TileSet>(TilesetPath);
            GD.Print($"[TileEditorService] Loaded TileSet from {TilesetPath}");
            return;
        }

        GD.PrintErr($"[TileEditorService] TileSet not found: {TilesetPath}");
    }

    private void LoadTileDefinitions(List<JsonTileData> tileData)
    {
        foreach (var data in tileData)
        {
            var tile = CreateTile(data);
            if (tile != null)
                _tiles[tile.Id] = tile;
        }
    }

    private static EditableTile? CreateTile(JsonTileData data)
    {
        if (string.IsNullOrEmpty(data.Id))
            return null;

        var tile = CreateBaseTile(data);

        LoadAutoTileVariants(tile, data);
        if (data.DecorationDensity.HasValue)
            tile.DecorationDensity = Math.Clamp(data.DecorationDensity.Value, 0f, 1f);
        if (!string.IsNullOrEmpty(data.AutoTileFormat))
            tile.AutoTileFormat = data.AutoTileFormat;
        LoadVariations(tile, data);
        LoadAnimation(tile, data);
        ApplyTileMetadata(tile, data);
        return tile;
    }

    private static EditableTile CreateBaseTile(JsonTileData data)
    {
        return new EditableTile
        {
            Id = data.Id!,
            Name = GetStringOrDefault(data.Name, ""),
            Passability = GetStringOrDefault(data.Passability, "passable"),
            AtlasX = GetCoordinateX(data.AtlasCoords, 0),
            AtlasY = GetCoordinateY(data.AtlasCoords, 0),
            SourceId = GetIntOrDefault(data.SourceId, 4),
            Layer = GetStringOrDefault(data.Layer, "terrain"),
            Elevation = GetFloatOrDefault(data.Elevation, 0f),
            IsTransparent = GetBoolOrDefault(data.IsTransparent, true),
            Biomes = GetBiomes(data.Biomes),
            SizeX = GetCoordinateX(data.Size, 1),
            SizeY = GetCoordinateY(data.Size, 1),
            SourceScale = GetFloatOrDefault(data.SourceScale, 1.0f)
        };
    }

    private static string GetStringOrDefault(string? value, string fallback)
    {
        return value ?? fallback;
    }

    private static int GetCoordinateX(Vector2IData? value, int fallback)
    {
        return value?.X ?? fallback;
    }

    private static int GetCoordinateY(Vector2IData? value, int fallback)
    {
        return value?.Y ?? fallback;
    }

    private static int GetIntOrDefault(int? value, int fallback)
    {
        return value ?? fallback;
    }

    private static float GetFloatOrDefault(float? value, float fallback)
    {
        return value ?? fallback;
    }

    private static bool GetBoolOrDefault(bool? value, bool fallback)
    {
        return value ?? fallback;
    }

    private static List<string> GetBiomes(List<string>? value)
    {
        return value?.ToList() ?? new List<string>();
    }

    private static void LoadAutoTileVariants(EditableTile tile, JsonTileData data)
    {
        if (data.AutoTileVariants == null || !data.AutoTileVariants.Any(value => value != null))
            return;

        var variantCount = data.AutoTileFormat == "blob47" ? 47 : 16;
        tile.AutoTileVariants = new Vector2I?[variantCount];
        for (var index = 0; index < Math.Min(variantCount, data.AutoTileVariants.Length); index++)
        {
            var variant = data.AutoTileVariants[index];
            if (variant != null)
                tile.AutoTileVariants[index] = new Vector2I(variant.X, variant.Y);
        }
    }

    private static void LoadVariations(EditableTile tile, JsonTileData data)
    {
        if (data.Variations == null || data.Variations.Length == 0)
            return;

        tile.Variations = data.Variations
            .Select(variation => new Vector2I(variation.X, variation.Y))
            .ToArray();
        if (!string.IsNullOrEmpty(data.VariationMode))
            tile.VariationMode = data.VariationMode.ToLowerInvariant();
    }

    private static void LoadAnimation(EditableTile tile, JsonTileData data)
    {
        if (data.Animation?.Frames == null || data.Animation.Frames.Length == 0)
            return;

        tile.AnimationFrames = data.Animation.Frames
            .Select(frame => new Vector2I(frame.X, frame.Y))
            .ToArray();
        tile.AnimationFrameDuration = data.Animation.FrameDuration;
    }

    private static void ApplyTileMetadata(EditableTile tile, JsonTileData data)
    {
        if (!string.IsNullOrEmpty(data.TileMode))
            tile.TileMode = data.TileMode.ToLowerInvariant();
        else
            tile.TileMode = ComputeTileModeFromData(tile);

        tile.Dominance = data.Dominance;
        tile.InnerTerrainId = data.InnerTerrain;
        tile.OuterTerrainId = data.OuterTerrain;
        tile.Description = data.Description;
    }

    private void LoadBiomes(Dictionary<string, BiomeDataJson>? biomeData)
    {
        if (biomeData == null)
            return;

        foreach (var (biomeId, data) in biomeData)
        {
            _biomes[biomeId] = new EditableBiome
            {
                Id = biomeId,
                DisplayName = data.DisplayName ?? biomeId,
                Signature = data.Signature ?? new float[8],
                BlockedPercentage = data.BlockedPercentage,
                PassableTiles = data.PassableTiles ?? new Dictionary<string, float>(),
                BlockedTiles = data.BlockedTiles ?? new Dictionary<string, float>()
            };
        }

        GD.Print($"[TileEditorService] Loaded {_biomes.Count} biomes");
    }

    private static string ComputeTileModeFromData(EditableTile tile)
    {
        if (tile.HasAnimation)
            return "animated";

        if (tile.HasAutoTileVariants)
        {
            if (tile.HasVariations && tile.VariationMode == "pergeneration")
                return "permapvariationautotile";
            return "autotile";
        }

        if (tile.HasVariations)
            return tile.VariationMode == "pergeneration"
                ? "permapvariations"
                : "pertilevariations";

        return "plain";
    }

    private void EmitLoadSignals()
    {
        EmitSignal(SignalName.TilesLoaded);
        EmitSignal(SignalName.BiomesLoaded);
        EmitSignal(SignalName.AutoTileFormatsLoaded);
    }
}
#endif
