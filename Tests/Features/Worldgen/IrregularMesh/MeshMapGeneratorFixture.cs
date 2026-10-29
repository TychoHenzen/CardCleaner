using CardCleaner.Scripts.Core.Services.CompiledAtlas;
using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using CardCleaner.Scripts.Core.Interfaces;
using CardCleaner.Scripts.Core.Services;
using CardCleaner.Scripts.Features.Deckbuilder.Tiles;
using CardCleaner.Scripts.Features.Card.Models;
using CardCleaner.Scripts.Features.Worldgen.Biomes;
using CardCleaner.Scripts.Features.Worldgen.Wfc;
using IrregularMeshNs = CardCleaner.Scripts.Features.Worldgen.IrregularMesh;

namespace CardCleaner.Tests.Features.Worldgen.IrregularMesh;

/// <summary>
/// Shared inputs for the irregular map tests: the generator the composition root registers, built over fresh
/// registries, a generator that never finishes, the default biomes, and the terrain cards and terrain text the mesh
/// suites compare.
/// </summary>
public static class MeshMapGeneratorFixture
{
    public static IrregularMeshNs.MeshMapGenerator Create(int rings = IrregularMeshNs.MeshMapGenerator.DefaultRings) =>
        new(
            TileCatalogData.From(TileCatalogLoad.Load(new GodotAssetPaths(), new CompiledAtlasSource())),
            DefaultBiomes(),
            TestUtilities.CompiledTransitionPairs.Load(),
            new WfcMapGenerator.Factory(),
            rings);

    /// <summary>A generator whose maps never arrive, for tests that need a generation still running.</summary>
    public static IrregularMeshNs.IMeshMapGenerator Pending() => new PendingMeshGenerator();

    public static BiomeSet DefaultBiomes()
    {
        var biomes = new BiomeRegistry();
        biomes.RegisterDefaultBiomes(new GodotAssetPaths());
        return biomes.ToBiomeSet();
    }

    public static CardSignature[] TerrainCards() => new[]
    {
        new CardSignature(new[] { 1f, 1f, 0f, -1f, 0f, 0.5f, 0f, 0f }),
        new CardSignature(new[] { -1f, -1f, 1f, 1f, 0f, -0.5f, 0f, 0f })
    };

    public static string TerrainOf(IrregularMeshNs.IIrregularMesh mesh) =>
        string.Join(",", mesh.Vertices.Select(v => $"{v.TerrainType}:{v.TileId}:{v.ForegroundTileId}:{v.Position}"));

    private sealed class PendingMeshGenerator : IrregularMeshNs.IMeshMapGenerator
    {
        public Task<IGeneratedMap> GenerateAsync(
            MapGenerationConfig config,
            IProgress<float>? progress = null,
            CancellationToken cancellationToken = default) =>
            new TaskCompletionSource<IGeneratedMap>().Task;
    }
}
