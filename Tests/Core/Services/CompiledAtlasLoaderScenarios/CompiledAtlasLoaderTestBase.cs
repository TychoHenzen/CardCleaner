using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using CardCleaner.Scripts.Core.Services;
using Godot;

namespace CardCleaner.Tests.Core.Services.CompiledAtlasLoaderScenarios;

/// <summary>
///     Shared atlas fixtures, constants and temporary file helpers for the CompiledAtlasLoader scenario suites.
/// </summary>
public abstract class CompiledAtlasLoaderTestBase
{
    protected const string AtlasMappingPath = "res://Data/CompiledAtlas/atlas_mapping.json";
    protected const string TransitionMapPath = "res://Data/CompiledAtlas/transition_map.json";
    protected const string AtlasPngPath = "res://Data/CompiledAtlas/terrain_atlas.png";

    protected CompiledAtlasLoader.AtlasMappingData? _mapping;
    protected JsonDocument? _transitionDoc;
    protected Image? _atlasImage;
    protected readonly List<string> _temporaryPaths = new();

    [BeforeTest]
    public void Setup()
    {
        CompiledAtlasLoader.ClearCache();
        _mapping = CompiledAtlasLoader.LoadMapping();

        var transitionPath = ProjectSettings.GlobalizePath(TransitionMapPath);
        if (File.Exists(transitionPath))
        {
            var json = File.ReadAllText(transitionPath);
            _transitionDoc = JsonDocument.Parse(json);
        }

        var atlasPath = ProjectSettings.GlobalizePath(AtlasPngPath);
        if (File.Exists(atlasPath))
        {
            _atlasImage = Image.LoadFromFile(atlasPath);
        }
    }

    [AfterTest]
    public void Teardown()
    {
        _transitionDoc?.Dispose();
        CompiledAtlasLoader.ClearCache();

        foreach (var path in _temporaryPaths)
        {
            var absolutePath = ProjectSettings.GlobalizePath(path);
            if (File.Exists(absolutePath))
                File.Delete(absolutePath);
        }

        _temporaryPaths.Clear();
    }

    protected string CreateTemporaryPath(string extension)
    {
        var path = $"user://compiled-atlas-test-{Guid.NewGuid():N}{extension}";
        _temporaryPaths.Add(path);
        return path;
    }

    protected static void WriteMapping(string mappingPath, string atlasPath)
    {
        var mapping = new CompiledAtlasLoader.AtlasMappingData
        {
            Version = "test",
            Atlas = new CompiledAtlasLoader.AtlasInfo
            {
                Path = atlasPath,
                Width = 16,
                Height = 16,
                TileSize = 16
            },
            Sources = new Dictionary<string, Dictionary<string, CompiledAtlasLoader.TileAtlasRect>>()
        };

        File.WriteAllText(
            ProjectSettings.GlobalizePath(mappingPath),
            JsonSerializer.Serialize(mapping));
    }
}
