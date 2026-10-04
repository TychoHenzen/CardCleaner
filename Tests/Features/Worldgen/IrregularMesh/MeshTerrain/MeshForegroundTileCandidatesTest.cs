using System.Linq;
using CardCleaner.Scripts.Core.Enumeration;
using CardCleaner.Scripts.Core.Services;
using CardCleaner.Scripts.Features.Deckbuilder.Tiles;
using CardCleaner.Scripts.Features.Worldgen.Biomes;
using CardCleaner.Scripts.Features.Worldgen.IrregularMesh.MeshTerrain;
using Godot;

namespace CardCleaner.Tests.Features.Worldgen.IrregularMesh.MeshTerrain;

/// <summary>
/// Direct mesh WFC may only place terrain-layer tiles; structures and decorations are placed elsewhere.
/// </summary>
[TestSuite]
[RequireGodotRuntime]
public class MeshForegroundTileCandidatesTest
{
    private BiomeRegistry _biomes = null!;

    [BeforeTest]
    public void Setup()
    {
        _biomes = new BiomeRegistry();
        _biomes.RegisterDefaultBiomes();
    }

    private static TileRegistry MixedLayerRegistry()
    {
        var registry = new TileRegistry();
        registry.Clear();
        registry.RegisterTile(Tile("grass", TileLayer.Terrain));
        registry.RegisterTile(Tile("dirt", TileLayer.Terrain));
        registry.RegisterTile(Tile("house", TileLayer.Structure));
        registry.RegisterTile(Tile("flowers", TileLayer.Decoration));
        registry.RegisterTile(Tile("sparkles", TileLayer.Effects));
        return registry;
    }

    private static TileDefinition Tile(string id, TileLayer layer) =>
        new(id, id, TilePassability.Passable, Vector2I.Zero, new TileDefinitionOptions { Layer = layer });

    [TestCase]
    public void OnlyTerrainLayerTilesAreCandidates()
    {
        var candidates = MeshForegroundTileCandidates.Collect(MixedLayerRegistry(), _biomes);

        AssertThat(candidates.OrderBy(id => id).ToList()).IsEqual(new[] { "dirt", "grass" }.ToList());
    }

    [TestCase]
    public void TheShippedRegistryYieldsTerrainTilesOnly()
    {
        var registry = new TileRegistry();

        var candidates = MeshForegroundTileCandidates.Collect(registry, _biomes);

        var offenders = candidates.Where(id => registry.GetTile(id)!.Layer != TileLayer.Terrain).ToList();
        AssertInt(candidates.Count).IsGreater(0);
        AssertInt(offenders.Count).IsEqual(0);
    }
}
