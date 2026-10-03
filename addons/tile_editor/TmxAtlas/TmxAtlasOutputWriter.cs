#if TOOLS
using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using CardCleaner.Scripts.Features.Worldgen.AutoTiling;
using Godot;
using static CardCleaner.Addons.TileEditor.TmxAtlasCompilerModels;

namespace CardCleaner.Addons.TileEditor;

internal sealed class TmxAtlasOutputWriter
{
    private const string CompiledAtlasDir = "res://Data/CompiledAtlas";
    private const string MappingFileName = "atlas_mapping.json";
    private const string TransitionMapFileName = "transition_map.json";

    private static readonly JsonSerializerOptions JsonWriteOptions = new()
    {
        WriteIndented = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    internal AtlasSaveResult SaveAtlasImage(Image atlasImage, string atlasPath)
    {
        var absolutePath = ProjectSettings.GlobalizePath(atlasPath);
        var saveError = atlasImage.SavePng(absolutePath);
        return saveError == Error.Ok
            ? new AtlasSaveResult { Success = true }
            : new AtlasSaveResult
            {
                Success = false,
                Message = $"Failed to save atlas: {saveError}"
            };
    }

    internal AtlasSaveResult SaveAtlasMapping(
        Dictionary<string, Dictionary<string, TileAtlasRect>> mapping,
        Vector2I atlasSize,
        int tileSize,
        string atlasPath)
    {
        try
        {
            var sourcesMapping = CreateSourcesMapping(mapping);
            var data = new AtlasMappingData
            {
                Atlas = new AtlasInfo
                {
                    Path = atlasPath,
                    Width = atlasSize.X,
                    Height = atlasSize.Y,
                    TileSize = tileSize
                },
                Sources = sourcesMapping
            };
            var json = JsonSerializer.Serialize(data, JsonWriteOptions);
            var absolutePath = ProjectSettings.GlobalizePath($"{CompiledAtlasDir}/{MappingFileName}");
            File.WriteAllText(absolutePath, json);
            GD.Print($"[TmxAtlasCompiler] Saved mapping to {MappingFileName}");
            return new AtlasSaveResult { Success = true };
        }
        catch (Exception ex)
        {
            return new AtlasSaveResult
            {
                Success = false,
                Message = $"Error saving mapping: {ex.Message}"
            };
        }
    }

    internal AtlasSaveResult SaveTransitionMap(CompiledTransitionMap transitionMap)
    {
        try
        {
            var json = JsonSerializer.Serialize(transitionMap, JsonWriteOptions);
            var absolutePath = ProjectSettings.GlobalizePath($"{CompiledAtlasDir}/{TransitionMapFileName}");
            File.WriteAllText(absolutePath, json);
            GD.Print(
                $"[TmxAtlasCompiler] Saved transition map with "
                + $"{transitionMap.Transitions.Count} entries");
            return new AtlasSaveResult { Success = true };
        }
        catch (Exception ex)
        {
            return new AtlasSaveResult
            {
                Success = false,
                Message = $"Error saving transition map: {ex.Message}"
            };
        }
    }

    private static Dictionary<string, Dictionary<string, TileAtlasRect>> CreateSourcesMapping(
        Dictionary<string, Dictionary<string, TileAtlasRect>> mapping)
    {
        var sourcesMapping = new Dictionary<string, Dictionary<string, TileAtlasRect>>();
        foreach (var sourceMapping in mapping.Values)
        {
            const string sourceId = "1";
            if (!sourcesMapping.TryGetValue(sourceId, out var targetMapping))
            {
                targetMapping = new Dictionary<string, TileAtlasRect>();
                sourcesMapping[sourceId] = targetMapping;
            }

            foreach (var (coordKey, rect) in sourceMapping)
                targetMapping[coordKey] = rect;
        }

        return sourcesMapping;
    }
}
#endif
