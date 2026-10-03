using System.Collections.Generic;
using GdUnit4;
using Godot;
using static GdUnit4.Assertions;
using IrregularMeshNs = CardCleaner.Scripts.Features.Worldgen.IrregularMesh;

namespace CardCleaner.Tests.Features.Worldgen.IrregularMesh;

/// <summary>
/// Tests for StructurePlacement service.
/// Verifies structure placement, passability updates, and visual spawning.
/// </summary>
[TestSuite]
[RequireGodotRuntime]
public class StructurePlacementTest
{
    private IrregularMeshNs.IrregularMesh _testMesh = null!;
    private IrregularMeshNs.StructurePlacement _placement = null!;

    [BeforeTest]
    public void Setup()
    {
        _testMesh = CreateSimpleTestMesh();
        _placement = new IrregularMeshNs.StructurePlacement(_testMesh);
    }

    [TestCase]
    public void TestCanPlaceStructureOnValidVertex()
    {
        // Interior vertex (id=5) should be valid for placement
        var canPlace = _placement.CanPlaceStructure(5, IrregularMeshNs.StructureType.Wall);
        AssertBool(canPlace).IsTrue();
    }

    [TestCase]
    public void TestCannotPlaceStructureOnInvalidVertex()
    {
        // Invalid vertex ID
        var canPlace = _placement.CanPlaceStructure(-1, IrregularMeshNs.StructureType.Wall);
        AssertBool(canPlace).IsFalse();

        canPlace = _placement.CanPlaceStructure(999, IrregularMeshNs.StructureType.Wall);
        AssertBool(canPlace).IsFalse();
    }

    [TestCase]
    public void TestCannotPlaceStructureOnBoundaryVertex()
    {
        // Mark a vertex as boundary
        _testMesh.Vertices[0].IsBoundary = true;

        var canPlace = _placement.CanPlaceStructure(0, IrregularMeshNs.StructureType.Wall);
        AssertBool(canPlace).IsFalse();
    }

    [TestCase]
    public void TestCannotPlaceStructureOnExistingStructure()
    {
        // Place first structure
        _placement.PlaceStructure(5, IrregularMeshNs.StructureType.Wall);

        // Cannot place another at same vertex
        var canPlace = _placement.CanPlaceStructure(5, IrregularMeshNs.StructureType.Door);
        AssertBool(canPlace).IsFalse();
    }

    [TestCase]
    public void TestPlaceStructureUpdatesVertexHasStructure()
    {
        AssertBool(_testMesh.Vertices[5].HasStructure).IsFalse();

        _placement.PlaceStructure(5, IrregularMeshNs.StructureType.Wall);

        AssertBool(_testMesh.Vertices[5].HasStructure).IsTrue();
    }

    [TestCase]
    public void TestPlaceStructureReturnsTrue()
    {
        var result = _placement.PlaceStructure(5, IrregularMeshNs.StructureType.Wall);
        AssertBool(result).IsTrue();
    }

    [TestCase]
    public void TestPlaceStructureOnInvalidVertexReturnsFalse()
    {
        var result = _placement.PlaceStructure(-1, IrregularMeshNs.StructureType.Wall);
        AssertBool(result).IsFalse();
    }

    [TestCase]
    public void TestRemoveStructure()
    {
        _placement.PlaceStructure(5, IrregularMeshNs.StructureType.Wall);
        AssertBool(_testMesh.Vertices[5].HasStructure).IsTrue();

        var result = _placement.RemoveStructure(5);

        AssertBool(result).IsTrue();
        AssertBool(_testMesh.Vertices[5].HasStructure).IsFalse();
    }

    [TestCase]
    public void TestRemoveStructureFromEmptyVertexReturnsFalse()
    {
        var result = _placement.RemoveStructure(5);
        AssertBool(result).IsFalse();
    }

    [TestCase]
    public void TestGetStructureAt()
    {
        _placement.PlaceStructure(5, IrregularMeshNs.StructureType.Door);

        var structureType = _placement.GetStructureAt(5);

        AssertThat(structureType).IsNotNull();
        AssertThat(structureType).IsEqual(IrregularMeshNs.StructureType.Door);
    }

    [TestCase]
    public void TestGetStructureAtEmptyVertexReturnsNull()
    {
        var structureType = _placement.GetStructureAt(5);
        AssertThat(structureType).IsNull();
    }

    [TestCase]
    public void TestStructureCount()
    {
        AssertThat(_placement.StructureCount).IsEqual(0);

        _placement.PlaceStructure(5, IrregularMeshNs.StructureType.Wall);
        AssertThat(_placement.StructureCount).IsEqual(1);

        _placement.PlaceStructure(6, IrregularMeshNs.StructureType.Door);
        AssertThat(_placement.StructureCount).IsEqual(2);
    }

    [TestCase]
    public void TestClearAllStructures()
    {
        _placement.PlaceStructure(5, IrregularMeshNs.StructureType.Wall);
        _placement.PlaceStructure(6, IrregularMeshNs.StructureType.Door);

        _placement.ClearAllStructures();

        AssertThat(_placement.StructureCount).IsEqual(0);
        AssertBool(_testMesh.Vertices[5].HasStructure).IsFalse();
        AssertBool(_testMesh.Vertices[6].HasStructure).IsFalse();
    }

    [TestCase]
    public void TestStructureBlocksQuadPassability()
    {
        // Interior vertex (5) is part of 4 quads: Q0, Q1, Q3, Q4
        // Only those 4 quads should become impassable after placing structure
        var affectedQuadIds = new HashSet<int> { 0, 1, 3, 4 };

        // All quads should initially be passable
        foreach (var quad in _testMesh.Quads)
        {
            AssertBool(quad.IsPassable()).IsTrue();
        }

        // Place structure at interior vertex
        _placement.PlaceStructure(5, IrregularMeshNs.StructureType.Wall);

        // Affected quads should now be impassable, others remain passable
        for (int i = 0; i < _testMesh.Quads.Count; i++)
        {
            var expectedPassable = !affectedQuadIds.Contains(i);
            AssertBool(_testMesh.Quads[i].IsPassable()).IsEqual(expectedPassable);
        }
    }

    [TestCase]
    public void TestGetAffectedQuads()
    {
        // Interior vertex 5 affects 4 quads: Q0, Q1, Q3, Q4
        var affected = _placement.GetAffectedQuads(5);
        AssertThat(affected).HasSize(4);
    }

    [TestCase]
    public void TestFindNearestValidPlacement()
    {
        // Search near interior vertex 5 at (16, 16)
        var nearestId = _placement.FindNearestValidPlacement(
            new Vector2(16, 16),
            IrregularMeshNs.StructureType.Wall,
            50f
        );

        AssertThat(nearestId).IsNotNull();
        AssertThat(nearestId).IsEqual(5); // Interior vertex at (16, 16)
    }

    [TestCase]
    public void TestFindNearestValidPlacementReturnsNullIfNoneFound()
    {
        // Mark all vertices as having structures
        foreach (var vertex in _testMesh.Vertices)
        {
            vertex.HasStructure = true;
        }

        var nearestId = _placement.FindNearestValidPlacement(
            new Vector2(16, 16),
            IrregularMeshNs.StructureType.Wall,
            50f
        );

        AssertThat(nearestId).IsNull();
    }

    [TestCase]
    public void TestStructurePlacedEventRaised()
    {
        int eventVertexId = -1;
        IrregularMeshNs.StructureType eventType = IrregularMeshNs.StructureType.Wall;

        _placement.StructurePlaced += (vertexId, type) =>
        {
            eventVertexId = vertexId;
            eventType = type;
        };

        _placement.PlaceStructure(5, IrregularMeshNs.StructureType.Door);

        AssertThat(eventVertexId).IsEqual(5);
        AssertThat(eventType).IsEqual(IrregularMeshNs.StructureType.Door);
    }

    [TestCase]
    public void TestStructureRemovedEventRaised()
    {
        int eventVertexId = -1;

        _placement.StructureRemoved += (vertexId) =>
        {
            eventVertexId = vertexId;
        };

        _placement.PlaceStructure(5, IrregularMeshNs.StructureType.Wall);
        _placement.RemoveStructure(5);

        AssertThat(eventVertexId).IsEqual(5);
    }

    [TestCase]
    public void TestBridgeCanBePlacedOnWater()
    {
        // Set interior vertex 5 to water terrain (type 0)
        _testMesh.Vertices[5].TerrainType = 0;

        // Walls cannot be placed on water
        var canPlaceWall = _placement.CanPlaceStructure(5, IrregularMeshNs.StructureType.Wall);
        AssertBool(canPlaceWall).IsFalse();

        // But bridges can
        var canPlaceBridge = _placement.CanPlaceStructure(5, IrregularMeshNs.StructureType.Bridge);
        AssertBool(canPlaceBridge).IsTrue();
    }

    /// <summary>
    /// Create a 3x3 quad test mesh with 16 vertices (4x4 grid).
    /// Interior vertices: 5, 6, 9, 10 (not on boundary).
    /// </summary>
    private static IrregularMeshNs.IrregularMesh CreateSimpleTestMesh()
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
