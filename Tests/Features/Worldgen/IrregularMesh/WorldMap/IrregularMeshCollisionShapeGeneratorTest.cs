using System.Linq;
using CardCleaner.Scripts.Core.Services;
using CardCleaner.Scripts.Features.Worldgen.IrregularMesh.WorldMap;
using Godot;
using IrregularMeshNs = CardCleaner.Scripts.Features.Worldgen.IrregularMesh;

namespace CardCleaner.Tests.Features.Worldgen.IrregularMesh.WorldMap;

/// <summary>
/// Pins IrregularMeshCollisionShapeGenerator.GenerateForIrregularMesh: one shape per opaque quad, the opaque collision
/// layer with a zero mask, and the quad corners in world space.
/// </summary>
[TestSuite]
[RequireGodotRuntime]
public class IrregularMeshCollisionShapeGeneratorTest
{
    private const int CenterVertex = 4;
    private const float WorldScale = 2f;
    private static readonly Vector2 WorldOffset = new(10f, 20f);

    [TestCase]
    public static void TestNoShapesWhenNoQuadIsOpaque()
    {
        var mapData = new IrregularMeshNs.IrregularMeshMapData(CreateTwoByTwoMesh());
        var staticBody = new StaticBody2D();

        IrregularMeshCollisionShapeGenerator.GenerateForIrregularMesh(mapData, staticBody);

        AssertThat(staticBody.GetShapeOwners().Length).IsEqual(0);
        staticBody.Free();
    }

    [TestCase]
    public static void TestOneShapePerOpaqueQuad()
    {
        var mapData = new IrregularMeshNs.IrregularMeshMapData(CreateTwoByTwoMesh());
        mapData.PlaceStructure(0);
        mapData.PlaceStructure(3);
        var staticBody = new StaticBody2D();

        IrregularMeshCollisionShapeGenerator.GenerateForIrregularMesh(mapData, staticBody);

        AssertThat(staticBody.GetShapeOwners().Length).IsEqual(2);
        staticBody.Free();
    }

    [TestCase]
    public static void TestEveryQuadIsOpaqueWhenTheCenterVertexHoldsAWall()
    {
        var mesh = CreateTwoByTwoMesh();
        var placement = new IrregularMeshNs.StructurePlacement(mesh);
        AssertBool(placement.PlaceStructure(CenterVertex, IrregularMeshNs.StructureType.Wall)).IsTrue();
        var mapData = new IrregularMeshNs.IrregularMeshMapData(mesh);
        var staticBody = new StaticBody2D();

        IrregularMeshCollisionShapeGenerator.GenerateForIrregularMesh(mapData, staticBody);

        AssertThat(staticBody.GetShapeOwners().Length).IsEqual(4);
        staticBody.Free();
    }

    [TestCase]
    public static void TestSetsOpaqueLayerAndClearsMaskOnStaticBody()
    {
        var staticBody = new StaticBody2D();

        IrregularMeshCollisionShapeGenerator.GenerateForIrregularMesh(CreateMapWithOneOpaqueQuad(), staticBody);

        AssertThat(staticBody.CollisionLayer).IsEqual(RaycastVisibilityChecker.OpaqueTerrainCollisionLayer);
        AssertThat(staticBody.CollisionMask).IsEqual(0u);
        staticBody.Free();
    }

    [TestCase]
    public static void TestSetsOpaqueLayerAndClearsMaskOnArea2D()
    {
        var area = new Area2D();

        IrregularMeshCollisionShapeGenerator.GenerateForIrregularMesh(CreateMapWithOneOpaqueQuad(), area);

        AssertThat(area.CollisionLayer).IsEqual(RaycastVisibilityChecker.OpaqueTerrainCollisionLayer);
        AssertThat(area.CollisionMask).IsEqual(0u);
        area.Free();
    }

    [TestCase]
    public static void TestShapeIsTheQuadCornersInWorldSpace()
    {
        var staticBody = new StaticBody2D();

        IrregularMeshCollisionShapeGenerator.GenerateForIrregularMesh(CreateMapWithOneOpaqueQuad(), staticBody);

        // Quad 0 spans (0,0)-(16,16) in mesh space. Scaled by 2 and offset by (10,20) it spans (10,20)-(42,52).
        var ownerId = (uint)(int)staticBody.GetShapeOwners()[0];
        var shape = staticBody.ShapeOwnerGetShape(ownerId, 0) as ConvexPolygonShape2D;
        AssertThat(shape).IsNotNull();
        var corners = shape!.Points.OrderBy(p => p.X).ThenBy(p => p.Y).ToArray();
        var expected = new[]
        {
            new Vector2(10f, 20f),
            new Vector2(10f, 52f),
            new Vector2(42f, 20f),
            new Vector2(42f, 52f),
        };
        AssertBool(corners.SequenceEqual(expected)).IsTrue();
        staticBody.Free();
    }

    private static IrregularMeshNs.IrregularMeshMapData CreateMapWithOneOpaqueQuad()
    {
        var mapData = new IrregularMeshNs.IrregularMeshMapData(CreateTwoByTwoMesh())
        {
            WorldScale = WorldScale,
            WorldOffset = WorldOffset,
        };
        mapData.PlaceStructure(0);
        return mapData;
    }

    private static IrregularMeshNs.IrregularMesh CreateTwoByTwoMesh()
    {
        var mesh = new IrregularMeshNs.IrregularMesh();
        for (var y = 0; y < 3; y++)
        {
            for (var x = 0; x < 3; x++)
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
