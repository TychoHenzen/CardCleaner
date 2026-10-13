using CardCleaner.Scripts.Core.Services;
using CardCleaner.Scripts.Features.Worldgen.Biomes;
using IrregularMeshNs = CardCleaner.Scripts.Features.Worldgen.IrregularMesh;

namespace CardCleaner.Tests.Features.Worldgen.IrregularMesh;

/// <summary>
/// The irregular map generator the composition root registers, built over fresh registries, for tests.
/// </summary>
public static class MeshMapGeneratorFixture
{
    public static IrregularMeshNs.MeshMapGenerator Create() => new(new TileRegistry(), DefaultBiomes());

    public static BiomeRegistry DefaultBiomes()
    {
        var biomes = new BiomeRegistry();
        biomes.RegisterDefaultBiomes();
        return biomes;
    }
}
