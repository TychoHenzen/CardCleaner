using CardCleaner.Scripts.Core.Services;
using CardCleaner.Scripts.Features.Worldgen.IrregularMesh;
using CardCleaner.Scripts.Features.Worldgen.IrregularMesh.WorldMap;
using CardCleaner.Tests.Features.Worldgen.IrregularMesh;
using CardCleaner.Tests.TestUtilities;

namespace CardCleaner.Tests.Features.Worldgen.IrregularMesh.MeshTerrain;

[TestSuite]
[RequireGodotRuntime]
public class TerrainGeneratorFactoryTest
{
    [TestCase]
    [TestCategory("Unit")]
    public static void CreateReturnsTheContract()
    {
        IMeshTerrainGenerator generator = TerrainGeneratorFactory.Create(
            new TileRegistry(new GodotAssetPaths()),
            MeshMapGeneratorFixture.DefaultBiomes(),
            CompiledTransitionPairs.Load());

        AssertThat(generator).IsNotNull();
    }
}
