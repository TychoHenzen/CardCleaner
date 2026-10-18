using CardCleaner.Scripts.Core.Randomness;
using CardCleaner.Scripts.Features.Worldgen.IrregularMesh;
using IrregularMeshNs = CardCleaner.Scripts.Features.Worldgen.IrregularMesh;

namespace CardCleaner.Tests.Features.Worldgen.IrregularMesh;

[TestSuite]
[RequireGodotRuntime]
public class IrregularMapsTest
{
    private const int MeshRings = 20;

    [TestCase]
    [TestCategory("Unit")]
    public static void PopulatedAppliesTheSamePlacementAsMapPopulation()
    {
        var mesh = CreateMesh();

        var (map, placement, _) = IrregularMaps.Populated(
            mesh, IrregularMeshNs.MeshMapGenerator.WorldScale, RandomSources.Create(7UL));
        var (_, againPlacement, _) = IrregularMaps.Populated(
            mesh, IrregularMeshNs.MeshMapGenerator.WorldScale, RandomSources.Create(7UL));

        AssertThat(map.PlayerStartCell).IsEqual(placement.PlayerStartCell);
        AssertThat(string.Join(",", map.EnemySpawnCells)).IsEqual(string.Join(",", placement.EnemyCells));
        AssertThat(againPlacement.PlayerStartCell).IsEqual(placement.PlayerStartCell);
    }

    [TestCase]
    [TestCategory("Unit")]
    public static void RemoveEnemyRemovesOnce()
    {
        var mesh = CreateMesh();
        var (map, placement, removeEnemy) = IrregularMaps.Populated(
            mesh, IrregularMeshNs.MeshMapGenerator.WorldScale, RandomSources.Create(7UL));

        AssertBool(removeEnemy(placement.EnemyCells[0])).IsTrue();
        AssertBool(removeEnemy(placement.EnemyCells[0])).IsFalse();
        AssertThat(map.EnemySpawnCells.Count).IsEqual(placement.EnemyCells.Count - 1);
    }

    private static IrregularMeshNs.IrregularMesh CreateMesh()
    {
        var mesh = IrregularMeshNs.MeshGenerator.Generate(new IrregularMeshNs.MeshGenerator.GenerationConfig
        {
            Rings = MeshRings,
            Seed = 11,
            RelaxationIterations = 0
        });
        foreach (var vertex in mesh.Vertices)
            vertex.TerrainType = 1;
        mesh.UpdateAllCachedProperties();
        return mesh;
    }
}
