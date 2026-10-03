using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using CardCleaner.Scripts.Core.Interfaces;
using CardCleaner.Scripts.Core.Services;
using Godot;

namespace CardCleaner.Scripts.Core.Services.CompiledAtlas;

internal static class CompiledAtlasTileSetBuilder
{
    private const string DefaultTransitionMapPath = "res://Data/CompiledAtlas/transition_map.json";

    internal static TileSet Build(
        CompiledAtlasLoader.AtlasMappingData mapping,
        Texture2D atlasTexture,
        JsonSerializerOptions jsonOptions)
    {
        var atlasInfo = mapping.Atlas!;
        var tileSet = new TileSet
        {
            TileSize = new Vector2I(atlasInfo.TileSize, atlasInfo.TileSize)
        };
        var atlasSource = new TileSetAtlasSource
        {
            Texture = atlasTexture,
            TextureRegionSize = tileSet.TileSize,
            UseTexturePadding = false
        };

        CreateMappedTiles(atlasSource, mapping.Sources);

        var transitionTilesCreated = CreateTransitionMapTiles(atlasSource, jsonOptions);
        ILog.Print(
            $"[CompiledAtlasLoader] Created {transitionTilesCreated} additional tiles " +
            "from transition_map");

        tileSet.AddSource(atlasSource, 0);
        return tileSet;
    }

    private static void CreateMappedTiles(
        TileSetAtlasSource atlasSource,
        Dictionary<string, Dictionary<string, CompiledAtlasLoader.TileAtlasRect>>? sources)
    {
        var tilesCreated = 0;
        var tilesSkipped = 0;

        if (sources == null)
        {
            LogMappedTileCounts(tilesCreated, tilesSkipped);
            return;
        }

        foreach (var coordMappings in sources.Values)
        {
            foreach (var rect in coordMappings.Values)
            {
                AddMappedTile(atlasSource, rect, ref tilesCreated, ref tilesSkipped);
            }
        }

        LogMappedTileCounts(tilesCreated, tilesSkipped);
    }

    private static void AddMappedTile(
        TileSetAtlasSource atlasSource,
        CompiledAtlasLoader.TileAtlasRect rect,
        ref int tilesCreated,
        ref int tilesSkipped)
    {
        var atlasCoords = new Vector2I(rect.X, rect.Y);
        var tileSize = new Vector2I(rect.W, rect.H);

        if (atlasSource.HasTile(atlasCoords))
        {
            tilesSkipped++;
            return;
        }

        try
        {
            atlasSource.CreateTile(atlasCoords, tileSize);
            tilesCreated++;
        }
        catch (Exception tileEx)
        {
            ILog.Print(
                $"[CompiledAtlasLoader] Failed to create tile at {atlasCoords} " +
                $"size {tileSize}: {tileEx.Message}");
        }
    }

    private static void LogMappedTileCounts(int tilesCreated, int tilesSkipped)
    {
        ILog.Print(
            $"[CompiledAtlasLoader] Created {tilesCreated} tiles from atlas_mapping, " +
            $"skipped {tilesSkipped} existing");
    }

    private static int CreateTransitionMapTiles(
        TileSetAtlasSource atlasSource,
        JsonSerializerOptions jsonOptions)
    {
        var absolutePath = ProjectSettings.GlobalizePath(DefaultTransitionMapPath);
        if (!File.Exists(absolutePath))
        {
            ILog.Print($"[CompiledAtlasLoader] Transition map not found: {absolutePath}");
            return 0;
        }

        if (!TryReadTransitionMap(absolutePath, jsonOptions, out var transitionMap))
            return 0;

        var transitions = transitionMap?.GetTransitions();
        if (transitions == null)
        {
            ILog.Print("[CompiledAtlasLoader] Transition map has no transitions");
            return 0;
        }

        return CreateTransitionTiles(atlasSource, transitions);
    }

    private static bool TryReadTransitionMap(
        string absolutePath,
        JsonSerializerOptions jsonOptions,
        out TransitionMapData? transitionMap)
    {
        transitionMap = null;

        try
        {
            var json = File.ReadAllText(absolutePath);
            transitionMap = JsonSerializer.Deserialize<TransitionMapData>(json, jsonOptions);
            return true;
        }
        catch (Exception ex)
        {
            ILog.Print($"[CompiledAtlasLoader] Error loading transition map: {ex.Message}");
            return false;
        }
    }

    private static int CreateTransitionTiles(
        TileSetAtlasSource atlasSource,
        Dictionary<string, TransitionEntry> transitions)
    {
        try
        {
            return CreateTransitionTilesCore(atlasSource, transitions);
        }
        catch (Exception ex)
        {
            ILog.Print($"[CompiledAtlasLoader] Error loading transition map: {ex.Message}");
            return 0;
        }
    }

    private static int CreateTransitionTilesCore(
        TileSetAtlasSource atlasSource,
        Dictionary<string, TransitionEntry> transitions)
    {
        var tilesCreated = 0;
        var tilesSkipped = 0;

        foreach (var entry in transitions.Values)
        {
            AddTransitionEntryTiles(atlasSource, entry, ref tilesCreated, ref tilesSkipped);
        }

        ILog.Print(
            $"[CompiledAtlasLoader] Transition tiles: {tilesCreated} created, " +
            $"{tilesSkipped} already existed");
        return tilesCreated;
    }

    private static void AddTransitionEntryTiles(
        TileSetAtlasSource atlasSource,
        TransitionEntry entry,
        ref int tilesCreated,
        ref int tilesSkipped)
    {
        var variants = entry.GetVariants();
        if (variants == null)
            return;

        foreach (var variantOptions in variants)
        {
            AddTransitionVariantTiles(
                atlasSource,
                variantOptions,
                ref tilesCreated,
                ref tilesSkipped);
        }
    }

    private static void AddTransitionVariantTiles(
        TileSetAtlasSource atlasSource,
        VariantCoord[]? variantOptions,
        ref int tilesCreated,
        ref int tilesSkipped)
    {
        if (variantOptions == null)
            return;

        foreach (var variant in variantOptions)
        {
            AddTransitionTile(atlasSource, variant, ref tilesCreated, ref tilesSkipped);
        }
    }

    private static void AddTransitionTile(
        TileSetAtlasSource atlasSource,
        VariantCoord variant,
        ref int tilesCreated,
        ref int tilesSkipped)
    {
        var atlasCoords = variant.GetAtlasCoordinates();

        if (atlasSource.HasTile(atlasCoords))
        {
            tilesSkipped++;
            return;
        }

        try
        {
            atlasSource.CreateTile(atlasCoords);
            tilesCreated++;
        }
        catch (Exception ex)
        {
            ILog.Print(
                $"[CompiledAtlasLoader] Failed to create transition tile at {atlasCoords}: " +
                ex.Message);
        }
    }

    private sealed class TransitionMapData
    {
        [JsonInclude]
        [JsonPropertyName("transitions")]
        private Dictionary<string, TransitionEntry>? _transitions;

        internal Dictionary<string, TransitionEntry>? GetTransitions() => _transitions;
    }

    private sealed class TransitionEntry
    {
        [JsonInclude]
        [JsonPropertyName("variants")]
        private VariantCoord[]?[]? _variants;

        internal VariantCoord[]?[]? GetVariants() => _variants;
    }

    private sealed class VariantCoord
    {
        [JsonInclude]
        [JsonPropertyName("x")]
        private int _x;

        [JsonInclude]
        [JsonPropertyName("y")]
        private int _y;

        internal Vector2I GetAtlasCoordinates() => new(_x, _y);
    }
}
