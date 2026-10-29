using CardCleaner.Scripts.Core.Services.CompiledAtlas;
using CardCleaner.Scripts.Core.Services;
using CardCleaner.Scripts.Features.Deckbuilder.Tiles;
using CardCleaner.Scripts.Features.Worldgen.IrregularMesh;
using CardCleaner.Scripts.Features.Worldgen.IrregularMesh.WorldMap;
using CardCleaner.Scripts.Features.Worldgen.Wfc;
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
            TileCatalogData.From(TileCatalogLoad.Load(new GodotAssetPaths(), new CompiledAtlasSource())),
            MeshMapGeneratorFixture.DefaultBiomes(),
            CompiledTransitionPairs.Load(),
            new WfcMapGenerator.TerrainSolverFactory());

        AssertThat(generator).IsNotNull();
    }
}
