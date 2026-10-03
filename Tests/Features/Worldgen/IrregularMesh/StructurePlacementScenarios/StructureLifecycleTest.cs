using System.Collections.Generic;
using Godot;
using IrregularMeshNs = CardCleaner.Scripts.Features.Worldgen.IrregularMesh;

namespace CardCleaner.Tests.Features.Worldgen.IrregularMesh.StructurePlacementScenarios;

/// <summary>
///     StructureLifecycleTest scenarios split out of StructurePlacementTest.
/// </summary>
[TestSuite]
[RequireGodotRuntime]
public class StructureLifecycleTest : StructurePlacementTestBase
{
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
}
