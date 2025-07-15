using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using CardCleaner.Scripts.Core.Interfaces;
using CardCleaner.Scripts.Features.Worldgen;
using Godot;
using Godot.Collections;

namespace CardCleaner.Scripts.Core.Services;

public static class JsonWorldSerializer
{
    public static readonly JsonSerializerOptions SerializerOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true,
        Converters =
        {
            new Vector2IJsonConverter(),
            new Vector3IJsonConverter(),
            new CompatibilityTagArrayJsonConverter(),
            new JsonStringEnumConverter()
        },
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    public static WorldData LoadFromJson(string jsonText)
    {
        try
        {
            // Parse the JSON structure
            using var doc = JsonDocument.Parse(jsonText);
            var root = doc.RootElement;

            if (!root.TryGetProperty("worldData", out var worldDataElement))
                throw new InvalidOperationException("JSON must have a 'worldData' root property");

            // First, load and register all compatibility tags
            var registry = CompatibilityTagRegistry.Instance;
            registry.Clear();

            if (worldDataElement.TryGetProperty("compatibilityTags", out var tagsElement))
            {
                var tags = JsonSerializer.Deserialize<List<CompatibilityTag>>(
                    tagsElement.GetRawText(), SerializerOptions);

                if (tags != null)
                {
                    foreach (var tag in tags)
                        // The JSON property "name" maps to "Tag" property
                        registry.RegisterTag(tag);

                    // Resolve all string references to object references
                    registry.ResolveReferences();
                }
            }

            // Now deserialize the full world data
            var worldData = JsonSerializer.Deserialize<WorldData>(
                worldDataElement.GetRawText(), SerializerOptions);

            if (worldData == null)
                throw new InvalidOperationException("Failed to deserialize world data");

            // Resolve tile socket references
            ResolveTileReferences(worldData, registry);

            // Load resources (TileSets, PackedScenes) from paths
            LoadResources(worldData);

            return worldData;
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException($"Failed to load world data from JSON: {ex.Message}", ex);
        }
    }


    private static void ResolveTileReferences(WorldData worldData, CompatibilityTagRegistry registry)
    {
        foreach (var tile in worldData.SemanticTiles)
        {
            // Resolve socket tag names to objects
            tile.SocketData.North = ResolveTagArray(tile.SocketData.NorthName, registry);
            tile.SocketData.East = ResolveTagArray(tile.SocketData.EastName, registry);
            tile.SocketData.South = ResolveTagArray(tile.SocketData.SouthName, registry);
            tile.SocketData.West = ResolveTagArray(tile.SocketData.WestName, registry);
            tile.SocketData.NorthEast = ResolveTagArray(tile.SocketData.NorthEastName, registry);
            tile.SocketData.SouthEast = ResolveTagArray(tile.SocketData.SouthEastName, registry);
            tile.SocketData.SouthWest = ResolveTagArray(tile.SocketData.SouthWestName, registry);
            tile.SocketData.NorthWest = ResolveTagArray(tile.SocketData.NorthWestName, registry);
            tile.SocketData.Up = ResolveTagArray(tile.SocketData.UpName, registry);
            tile.SocketData.Down = ResolveTagArray(tile.SocketData.DownName, registry);

            // Resolve layer constraint tags
            foreach (var constraint in tile.LayerConstraints)
                if (!string.IsNullOrEmpty(constraint.tagName))
                    constraint.tag = registry.GetTag(constraint.tagName);
        }

        // Resolve enemy spawn data terrain preferences
        foreach (var enemyData in worldData.EnemySpawnData)
            enemyData.PreferredTerrain = ResolveTagArray(enemyData.PreferredTerrainNames, registry);
    }

    private static Array<CompatibilityTag> ResolveTagArray(Array<string> tagNames, CompatibilityTagRegistry registry)
    {
        var tags = new Array<CompatibilityTag>();
        foreach (var name in tagNames)
        {
            var tag = registry.GetTag(name);
            if (tag != null)
                tags.Add(tag);
            else
                ILog.Warning($"Could not resolve tag '{name}'");
        }

        return tags;
    }

    public static string SaveToJson(WorldData worldData)
    {
        try
        {
            return JsonSerializer.Serialize(worldData, SerializerOptions);
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException($"Failed to serialize world data to JSON: {ex.Message}", ex);
        }
    }

    public static void SaveToFile(WorldData worldData, string filePath)
    {
        var json = SaveToJson(worldData);
        File.WriteAllText(filePath, json);
        ILog.Print($"World data saved to {filePath}");
    }

    public static WorldData LoadFromFile(string filePath)
    {
        var jsonText = File.ReadAllText(filePath);
        return LoadFromJson(jsonText);
    }

    private static void LoadResources(WorldData worldData)
    {
        foreach (var tile in worldData.SemanticTiles)
            if (!string.IsNullOrEmpty(tile.TileSetPath))
                tile.TileSet = GD.Load<TileSet>(tile.TileSetPath);

        foreach (var enemyData in worldData.EnemySpawnData)
            if (!string.IsNullOrEmpty(enemyData.EnemyScenePath))
                enemyData.EnemyScene = GD.Load<PackedScene>(enemyData.EnemyScenePath);
    }
}