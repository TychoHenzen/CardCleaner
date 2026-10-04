using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Godot;
using IrregularMeshNs = CardCleaner.Scripts.Features.Worldgen.IrregularMesh;

namespace CardCleaner.Tests.Features.Worldgen.IrregularMesh;

/// <summary>
/// GenerateMapWithWfc must assign terrain from the rules it is given instead of the fallback terrain.
/// </summary>
[TestSuite]
[RequireGodotRuntime]
public class IrregularWorldMapScreenWfcRulesTest
{
    private const int Seed = 777;

    private IrregularMeshNs.IrregularWorldMapScreen _screen = null!;

    [BeforeTest]
    public async Task Setup()
    {
        var viewport = new SubViewport { Name = "Viewport" };
        _screen = new IrregularMeshNs.IrregularWorldMapScreen { MeshRings = 3, Viewport = viewport };
        _screen.AddChild(viewport);
        AddNode(_screen);
        await ISceneRunner.SyncProcessFrame;
    }

    private static HashSet<int> TerrainTypes(IrregularMeshNs.IrregularMesh mesh) =>
        mesh.Vertices.Select(v => v.TerrainType).ToHashSet();

    [TestCase]
    public void TerrainTypesComeFromTheSuppliedTileMapping()
    {
        var adjacency = new Dictionary<string, HashSet<string>> { { "water", new HashSet<string> { "water" } } };
        var tileToTerrain = new Dictionary<string, int> { { "water", 0 } };

        _screen.GenerateMapWithWfc(Seed, adjacency, tileToTerrain);

        AssertThat(TerrainTypes(_screen.Mesh!)).IsEqual(new HashSet<int> { 0 });
    }

    [TestCase]
    public void TilesWithoutCrossAdjacencyNeverShareAMesh()
    {
        var adjacency = new Dictionary<string, HashSet<string>>
        {
            { "grass", new HashSet<string> { "grass" } },
            { "water", new HashSet<string> { "water" } }
        };
        var tileToTerrain = new Dictionary<string, int> { { "grass", 5 }, { "water", 0 } };

        _screen.GenerateMapWithWfc(Seed, adjacency, tileToTerrain);

        var types = TerrainTypes(_screen.Mesh!);
        AssertInt(types.Count).IsEqual(1);
        AssertBool(types.Contains(5) || types.Contains(0)).IsTrue();
    }

    [TestCase]
    public void MixedRulesOnlyYieldMappedTerrainTypes()
    {
        var adjacency = new Dictionary<string, HashSet<string>>
        {
            { "grass", new HashSet<string> { "grass", "water" } },
            { "water", new HashSet<string> { "grass", "water" } }
        };
        var tileToTerrain = new Dictionary<string, int> { { "grass", 5 }, { "water", 0 } };

        _screen.GenerateMapWithWfc(Seed, adjacency, tileToTerrain);

        AssertThat(TerrainTypes(_screen.Mesh!).IsSubsetOf(new HashSet<int> { 0, 5 })).IsTrue();
    }
}
