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

    /// <summary>
    /// The tile set with the mapped tiles created and the transition tiles deferred until a cell first shows one.
    /// </summary>
    internal static CompiledAtlasTiles Build(
        AtlasMapping.AtlasMappingData mapping,
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

        var deferred = CollectTransitionMapTiles(atlasSource, jsonOptions);
        ILog.Print(
            $"[CompiledAtlasLoader] Deferred {deferred.Count} additional tiles " +
            "from transition_map until a cell shows them");

        tileSet.AddSource(atlasSource, CompiledAtlasTiles.SourceId);
        return new CompiledAtlasTiles(tileSet, atlasSource, deferred);
    }

    private static void CreateMappedTiles(
        TileSetAtlasSource atlasSource,
        Dictionary<string, Dictionary<string, AtlasMapping.TileAtlasRect>>? sources)
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
        AtlasMapping.TileAtlasRect rect,
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

    // The in-bounds transition-map coordinates that hold no tile yet. None is created here.
    private static HashSet<Vector2I> CollectTransitionMapTiles(
        TileSetAtlasSource atlasSource,
        JsonSerializerOptions jsonOptions)
    {
        var deferred = new HashSet<Vector2I>();
        var absolutePath = ProjectSettings.GlobalizePath(DefaultTransitionMapPath);
        if (!File.Exists(absolutePath))
        {
            ILog.Print($"[CompiledAtlasLoader] Transition map not found: {absolutePath}");
            return deferred;
        }

        if (!TryReadTransitionMap(absolutePath, jsonOptions, out var transitionMap))
            return deferred;

        var transitions = transitionMap?.Transitions;
        if (transitions == null)
        {
            ILog.Print("[CompiledAtlasLoader] Transition map has no transitions");
            return deferred;
        }

        CollectTransitionTiles(atlasSource, transitions, deferred);
        return deferred;
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

    private static void CollectTransitionTiles(
        TileSetAtlasSource atlasSource,
        Dictionary<string, TransitionEntry> transitions,
        HashSet<Vector2I> deferred)
    {
        var texture = atlasSource.Texture;
        if (texture == null)
            return;

        var regionSize = atlasSource.TextureRegionSize;
        var width = texture.GetWidth();
        var height = texture.GetHeight();
        var alreadyTiles = 0;
        foreach (var entry in transitions.Values)
        foreach (var variantOptions in entry.Variants ?? [])
        foreach (var variant in variantOptions ?? [])
        {
            var atlasCoords = variant.GetAtlasCoordinates();
            if (atlasCoords.X < 0 || atlasCoords.Y < 0
                || atlasCoords.X * regionSize.X >= width
                || atlasCoords.Y * regionSize.Y >= height)
                continue;

            if (atlasSource.HasTile(atlasCoords))
                alreadyTiles++;
            else
                deferred.Add(atlasCoords);
        }

        ILog.Print(
            $"[CompiledAtlasLoader] Transition tiles: {deferred.Count} deferred, {alreadyTiles} already mapped tiles");
    }

    private sealed class TransitionMapData
    {
        [JsonInclude]
        [JsonPropertyName("transitions")]
        internal Dictionary<string, TransitionEntry>? Transitions { get; set; }
    }

    private sealed class TransitionEntry
    {
        [JsonInclude]
        [JsonPropertyName("variants")]
        internal VariantCoord[]?[]? Variants { get; set; }
    }

    private sealed class VariantCoord
    {
        [JsonInclude]
        [JsonPropertyName("x")]
        internal int X { get; set; }

        [JsonInclude]
        [JsonPropertyName("y")]
        internal int Y { get; set; }

        internal Vector2I GetAtlasCoordinates() => new(X, Y);
    }
}
