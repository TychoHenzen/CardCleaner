using System.IO;
using System.Text.Json;
using CardCleaner.Scripts.Core.Services;
using CardCleaner.Scripts.Features.Worldgen.AutoTiling;
using Godot;

namespace CardCleaner.Tests.Features.Worldgen.AutoTiling.TransitionMap;

/// <summary>
///     Shared fixture for the TransitionMapValidationTest scenario suites.
/// </summary>
public abstract class TransitionMapValidationTestBase
{
    protected const string TransitionMapPath = "res://Data/CompiledAtlas/transition_map.json";

    protected JsonDocument? _transitionDoc;

    protected CompiledTransitionMap? _transitionMap;

    protected TileRegistry _registry = null!;

    [BeforeTest]
    public void Setup()
    {
        var absolutePath = ProjectSettings.GlobalizePath(TransitionMapPath);
        if (File.Exists(absolutePath))
        {
            var json = File.ReadAllText(absolutePath);
            _transitionDoc = JsonDocument.Parse(json);
            _transitionMap = JsonSerializer.Deserialize<CompiledTransitionMap>(json, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });
        }
        // Load from Tiled source to match transition_map.json source
        _registry = new TileRegistry();
        _registry.Clear();
        _registry.LoadFromData("res://Data/Tiled/tileset.tmx");
    }

    [AfterTest]
    public void Teardown()
    {
        _transitionDoc?.Dispose();
    }
}
