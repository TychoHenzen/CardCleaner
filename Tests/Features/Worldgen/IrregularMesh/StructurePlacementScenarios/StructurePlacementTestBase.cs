using Godot;
using IrregularMeshNs = CardCleaner.Scripts.Features.Worldgen.IrregularMesh;

namespace CardCleaner.Tests.Features.Worldgen.IrregularMesh.StructurePlacementScenarios;

/// <summary>
///     Shared fixture for the StructurePlacementTest scenario suites.
/// </summary>
public abstract class StructurePlacementTestBase
{
    protected IrregularMeshNs.IrregularMesh _testMesh = null!;

    protected IrregularMeshNs.StructurePlacement _placement = null!;

    [BeforeTest]
    public void Setup()
    {
        _testMesh = CreateSimpleTestMesh();
        _placement = new IrregularMeshNs.StructurePlacement(_testMesh);
    }

    /// <summary>
    /// Create a 3x3 quad test mesh with 16 vertices (4x4 grid).
    /// Interior vertices: 5, 6, 9, 10 (not on boundary).
    /// </summary>
    protected static IrregularMeshNs.IrregularMesh CreateSimpleTestMesh()
    {
        var mesh = new IrregularMeshNs.IrregularMesh();

        // Create 4x4 grid of vertices
        // 12 -- 13 -- 14 -- 15
        // |  Q6 |  Q7 |  Q8 |
        // 8  -- 9  -- 10 -- 11
        // |  Q3 |  Q4 |  Q5 |
        // 4  -- 5  -- 6  -- 7
        // |  Q0 |  Q1 |  Q2 |
        // 0  -- 1  -- 2  -- 3
        var positions = new[]
        {
            new Vector2(0, 0), new Vector2(16, 0), new Vector2(32, 0), new Vector2(48, 0),
            new Vector2(0, 16), new Vector2(16, 16), new Vector2(32, 16), new Vector2(48, 16),
            new Vector2(0, 32), new Vector2(16, 32), new Vector2(32, 32), new Vector2(48, 32),
            new Vector2(0, 48), new Vector2(16, 48), new Vector2(32, 48), new Vector2(48, 48)
        };

        foreach (var pos in positions)
        {
            var id = mesh.AddVertex(pos);
            mesh.Vertices[id].TerrainType = 1; // Solid ground
        }

        // Add 9 quads (3x3 grid)
        mesh.AddQuad(new[] { 0, 1, 5, 4 }); // Q0
        mesh.AddQuad(new[] { 1, 2, 6, 5 }); // Q1
        mesh.AddQuad(new[] { 2, 3, 7, 6 }); // Q2
        mesh.AddQuad(new[] { 4, 5, 9, 8 }); // Q3
        mesh.AddQuad(new[] { 5, 6, 10, 9 }); // Q4
        mesh.AddQuad(new[] { 6, 7, 11, 10 }); // Q5
        mesh.AddQuad(new[] { 8, 9, 13, 12 }); // Q6
        mesh.AddQuad(new[] { 9, 10, 14, 13 }); // Q7
        mesh.AddQuad(new[] { 10, 11, 15, 14 }); // Q8

        // Finalize mesh
        mesh.UpdateAllCachedProperties();
        mesh.BuildAdjacency();

        return mesh;
    }
}
