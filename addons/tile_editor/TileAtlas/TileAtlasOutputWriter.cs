#if TOOLS
using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using CardCleaner.Scripts.Features.Worldgen.AutoTiling;
using Godot;
using static CardCleaner.Addons.TileEditor.TileAtlasCompilerModels;

namespace CardCleaner.Addons.TileEditor;

internal sealed class TileAtlasOutputWriter
{
    private static readonly JsonSerializerOptions JsonWriteOptions = new()
    {
        WriteIndented = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    internal TileAtlasSaveResult SaveAtlasImage(Image atlasImage, string atlasPath)
    {
        var absolutePath = ProjectSettings.GlobalizePath(atlasPath);
        var error = atlasImage.SavePng(absolutePath);
        if (error != Error.Ok)
        {
            return new TileAtlasSaveResult
            {
                Message = $"Failed to save atlas: {error}"
            };
        }

        GD.Print(
            $"[TileAtlasCompiler] Saved atlas {atlasImage.GetWidth()}x"
            + $"{atlasImage.GetHeight()} to {atlasPath}");
        return new TileAtlasSaveResult { Success = true };
    }

    internal TileAtlasSaveResult SaveTransitionMap(
        CompiledTransitionMap transitionMap,
        string path)
    {
        try
        {
            var json = JsonSerializer.Serialize(transitionMap, JsonWriteOptions);
            var absolutePath = ProjectSettings.GlobalizePath(path);
            File.WriteAllText(absolutePath, json);
            GD.Print(
                $"[TileAtlasCompiler] Saved transition map with "
                + $"{transitionMap.Transitions.Count} entries to {path}");
            return new TileAtlasSaveResult { Success = true };
        }
        catch (Exception ex)
        {
            return new TileAtlasSaveResult
            {
                Message = $"Error saving transition map: {ex.Message}"
            };
        }
    }

    internal TileAtlasSaveResult SaveAtlasMapping(
        Dictionary<string, AtlasTileMapping> mapping,
        Vector2I atlasSize,
        Vector2I tileSize,
        string atlasPath,
        string mappingPath)
    {
        try
        {
            var data = new TileAtlasMappingData
            {
                Version = "1.0",
                Atlas = new TileAtlasInfo
                {
                    Path = atlasPath,
                    Width = atlasSize.X,
                    Height = atlasSize.Y,
                    TileSize = tileSize.X
                },
                Sources = mapping
            };
            var json = JsonSerializer.Serialize(data, JsonWriteOptions);
            var absolutePath = ProjectSettings.GlobalizePath(mappingPath);
            File.WriteAllText(absolutePath, json);
            GD.Print(
                $"[TileAtlasCompiler] Saved mapping with {mapping.Count} "
                + $"sources to {mappingPath}");
            return new TileAtlasSaveResult { Success = true };
        }
        catch (Exception ex)
        {
            return new TileAtlasSaveResult
            {
                Message = $"Error saving mapping: {ex.Message}"
            };
        }
    }
}
#endif
