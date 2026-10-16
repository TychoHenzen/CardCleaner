using Godot;
using IrregularMeshNs = CardCleaner.Scripts.Features.Worldgen.IrregularMesh;

namespace CardCleaner.Tests.Features.Worldgen.IrregularMesh;

/// <summary>
/// Pins the two passable-cell searches of IrregularMeshMapData: the seeded random pick over the ascending passable
/// cells and the nearest passable centroid (lowest index wins a tie), on a mesh with a non-trivial world scale and
/// offset, a mixed-corner cell, a structure cell, and a mesh with no passable cell.
/// </summary>
[TestSuite]
[RequireGodotRuntime]
public class IrregularMeshMapDataPassableCellTest
{
    private const float Scale = 2f;
    private static readonly Vector2 Offset = new(100f, -50f);

    private static Vector2 ToWorld(Vector2 meshPos) => meshPos * Scale + Offset;

    /// <summary>
    /// 4x3 vertices (16 apart) make 6 quads. Quad 0, 1, 3 and 5 are passable, quad 2 has one filled corner, quad 4
    /// is passable but holds a structure, and quad 5 has exactly two filled corners.
    /// </summary>
    private static IrregularMeshNs.IrregularMeshMapData CreateMapData(bool anyPassable)
    {
        var mesh = new IrregularMeshNs.IrregularMesh();
        for (var row = 0; row < 3; row++)
        {
            for (var col = 0; col < 4; col++)
                mesh.AddVertex(new Vector2(col * 16, row * 16));
        }

        if (anyPassable)
        {
            foreach (var id in new[] { 0, 1, 4, 5, 6 })
                mesh.Vertices[id].TerrainType = 1;
            mesh.Vertices[9].TerrainType = 2;
            mesh.Vertices[11].TerrainType = 3;
        }

        mesh.AddQuad(new[] { 0, 1, 5, 4 });
        mesh.AddQuad(new[] { 1, 2, 6, 5 });
        mesh.AddQuad(new[] { 2, 3, 7, 6 });
        mesh.AddQuad(new[] { 4, 5, 9, 8 });
        mesh.AddQuad(new[] { 5, 6, 10, 9 });
        mesh.AddQuad(new[] { 6, 7, 11, 10 });
        mesh.UpdateAllCachedProperties();
        mesh.BuildAdjacency();

        var mapData = new IrregularMeshNs.IrregularMeshMapData(mesh)
        {
            WorldScale = Scale,
            WorldOffset = Offset
        };
        mapData.PlaceStructure(4);
        return mapData;
    }

    private static void AssertCell(int? actual, int expected)
    {
        AssertBool(actual.HasValue).IsTrue();
        AssertInt(actual!.Value).IsEqual(expected);
    }

    [TestCase(1, 0)]
    [TestCase(2, 5)]
    [TestCase(3, 1)]
    [TestCategory("Unit")]
    public void FindRandomPassableCellPicksTheSeededPassableCell(int seed, int expected)
    {
        AssertCell(CreateMapData(true).FindRandomPassableCell(seed), expected);
    }

    [TestCase]
    [TestCategory("Unit")]
    public void FindRandomPassableCellNeverPicksAnImpassableCellOrTheStructure()
    {
        var mapData = CreateMapData(true);

        for (var seed = 0; seed < 50; seed++)
        {
            var cell = mapData.FindRandomPassableCell(seed);
            AssertBool(cell.HasValue).IsTrue();
            AssertBool(cell!.Value is 0 or 1 or 3 or 5).IsTrue();
        }
    }

    [TestCase]
    [TestCategory("Unit")]
    public void FindNearestPassableCellTiesGoToTheLowestIndex()
    {
        // Equidistant from the centroids of quads 0 and 1.
        AssertCell(CreateMapData(true).FindNearestPassableCell(ToWorld(new Vector2(16, 8))), 0);
    }

    [TestCase]
    [TestCategory("Unit")]
    public void FindNearestPassableCellSkipsTheStructureCell()
    {
        // On the centroid of quad 4, which holds a structure; quads 1, 3 and 5 tie behind it.
        AssertCell(CreateMapData(true).FindNearestPassableCell(ToWorld(new Vector2(24, 24))), 1);
    }

    [TestCase]
    [TestCategory("Unit")]
    public void FindNearestPassableCellPicksTheClosestCentroidFarAway()
    {
        AssertCell(CreateMapData(true).FindNearestPassableCell(ToWorld(new Vector2(60, 30))), 5);
    }

    [TestCase]
    [TestCategory("Unit")]
    public void BothSearchesReturnNullWhenNoCellIsPassable()
    {
        var mapData = CreateMapData(false);

        AssertBool(mapData.FindRandomPassableCell(7).HasValue).IsFalse();
        AssertBool(mapData.FindNearestPassableCell(ToWorld(new Vector2(8, 8))).HasValue).IsFalse();
    }
}
