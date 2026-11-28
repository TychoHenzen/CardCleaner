using CardCleaner.Scripts.Features.Deckbuilder.Tiles.Loading.CompiledAtlas;
using System.IO;
using System.Text.Json;
using CardCleaner.Scripts.Core.Services;
using CardCleaner.Scripts.Features.Deckbuilder.Tiles.Loading;
using CardCleaner.Scripts.Features.Worldgen.AutoTiling;
using Godot;
using CardCleaner.Tests.TestUtilities;

namespace CardCleaner.Tests.Features.Worldgen.AutoTiling.BlackTile;

/// <summary>
///     Shared fixture for the BlackTileReproductionTest scenario suites.
/// </summary>
[RequireGodotRuntime]
public abstract class BlackTileReproductionTestBase
{
    protected const string TransitionMapPath = "res://Data/CompiledAtlas/transition_map.json";

    protected const int CompiledAtlasSourceId = 0;

    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };

    protected TileRegistry _registry = null!;

    protected CompiledTransitionResolver _resolver = null!;

    protected AtlasMapping.AtlasMappingData? _atlasMapping;

    protected CompiledTransitionMap? _transitionMap;

    [BeforeTest]
    public void Setup()
    {
        _registry = TileCatalogLoad.Load(new GodotAssetPaths(), new CompiledAtlasSource());
        _resolver = GameTransitionResolver.Shared;
        _atlasMapping = CompiledAtlasLoader.LoadMapping();

        var transitionPath = ProjectSettings.GlobalizePath(TransitionMapPath);
        if (File.Exists(transitionPath))
        {
            var json = File.ReadAllText(transitionPath);
            _transitionMap = JsonSerializer.Deserialize<CompiledTransitionMap>(json, JsonOptions);
        }
    }
}
