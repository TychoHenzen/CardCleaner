using System.IO;
using System.Text.Json;
using CardCleaner.Scripts.Core.Services;
using CardCleaner.Scripts.Features.Worldgen.AutoTiling;
using Godot;

namespace CardCleaner.Tests.Features.Worldgen.AutoTiling.BlackTile;

/// <summary>
///     Shared fixture for the BlackTileReproductionTest scenario suites.
/// </summary>
public abstract class BlackTileReproductionTestBase
{
    protected const string TransitionMapPath = "res://Data/CompiledAtlas/transition_map.json";

    protected const int CompiledAtlasSourceId = 0;

    protected TileRegistry _registry = null!;

    protected CompiledTransitionResolver _resolver = null!;

    protected CompiledAtlasLoader.AtlasMappingData? _atlasMapping;

    protected CompiledTransitionMap? _transitionMap;

    [BeforeTest]
    public void Setup()
    {
        _registry = new TileRegistry();
        _resolver = new CompiledTransitionResolver();
        _atlasMapping = CompiledAtlasLoader.LoadMapping();

        var transitionPath = ProjectSettings.GlobalizePath(TransitionMapPath);
        if (File.Exists(transitionPath))
        {
            var json = File.ReadAllText(transitionPath);
            _transitionMap = JsonSerializer.Deserialize<CompiledTransitionMap>(json,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
        }
    }
}
