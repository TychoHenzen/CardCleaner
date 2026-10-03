using Godot;
using IrregularMeshNs = CardCleaner.Scripts.Features.Worldgen.IrregularMesh;

namespace CardCleaner.Tests.Features.Worldgen.IrregularMesh;

/// <summary>
/// Vertex structures placed on the mesh make their adjacent cells impassable in the map data.
/// </summary>
[TestSuite]
[RequireGodotRuntime]
public class IrregularMeshMapDataStructureTest
{
    private const int CenterVertex = 4;

    private IrregularMeshNs.IrregularMesh _mesh = null!;
    private IrregularMeshNs.IrregularMeshMapData _mapData = null!;

    [BeforeTest]
    public void Setup()
    {
        _mesh = CreateTwoByTwoMesh();
        _mapData = new IrregularMeshNs.IrregularMeshMapData(_mesh);
    }

    [TestCase]
    public void CellsAreImpassableWhenOneOfTheirVerticesHasAStructure()
    {
        _mesh.Vertices[CenterVertex].HasStructure = true;

        for (int cellId = 0; cellId < _mapData.CellCount; cellId++)
        {
            AssertBool(_mapData.IsPassable(cellId)).IsFalse();
        }
    }

    [TestCase]
    public void CellsStayPassableWithoutStructures()
    {
        for (int cellId = 0; cellId < _mapData.CellCount; cellId++)
        {
            AssertBool(_mapData.IsPassable(cellId)).IsTrue();
        }
    }

    private static IrregularMeshNs.IrregularMesh CreateTwoByTwoMesh()
    {
        var mesh = new IrregularMeshNs.IrregularMesh();
        for (int y = 0; y < 3; y++)
        {
            for (int x = 0; x < 3; x++)
            {
                var id = mesh.AddVertex(new Vector2(x * 16, y * 16));
                mesh.Vertices[id].TerrainType = 1;
            }
        }

        mesh.AddQuad(new[] { 0, 1, 4, 3 });
        mesh.AddQuad(new[] { 1, 2, 5, 4 });
        mesh.AddQuad(new[] { 3, 4, 7, 6 });
        mesh.AddQuad(new[] { 4, 5, 8, 7 });
        mesh.UpdateAllCachedProperties();
        mesh.BuildAdjacency();
        return mesh;
    }
}
