using CardCleaner.Scripts.Core.Services;
using CardCleaner.Scripts.Features.Worldgen.AutoTiling;
using Godot;

namespace CardCleaner.Tests.Features.Worldgen.AutoTiling.CompiledResolver;

/// <summary>
///     Shared fixture for the CompiledTransitionResolverTest scenario suites.
/// </summary>
public abstract class CompiledTransitionResolverTestBase
{
    protected CompiledTransitionResolver _resolver = null!;

    protected TileRegistry _registry = null!;

    protected CompiledTransitionMap _transitionMap = null!;

    [BeforeTest]
    public void Setup()
    {
        _resolver = new CompiledTransitionResolver();
        // Load from Tiled source to match transition_map.json source
        _registry = new TileRegistry();
        _registry.Clear();
        _registry.LoadFromData("res://Data/Tiled/tileset.tmx");

        // Load transition map for direct inspection
        var json = System.IO.File.ReadAllText(
            ProjectSettings.GlobalizePath("res://Data/CompiledAtlas/transition_map.json"));
        _transitionMap = System.Text.Json.JsonSerializer.Deserialize<CompiledTransitionMap>(json,
            new System.Text.Json.JsonSerializerOptions { PropertyNameCaseInsensitive = true })!;
    }
}
