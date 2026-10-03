using Godot;
using IrregularMeshNs = CardCleaner.Scripts.Features.Worldgen.IrregularMesh;

namespace CardCleaner.Tests.Features.Worldgen.IrregularMesh;

/// <summary>
/// Tests for DecorationRenderer.
/// Verifies decoration placement, terrain compatibility, and rendering.
/// </summary>
[TestSuite]
[RequireGodotRuntime]
public class DecorationRendererTest
{
    private IrregularMeshNs.IrregularMesh _testMesh = null!;
    private IrregularMeshNs.DecorationRenderer _renderer = null!;

    [BeforeTest]
    public void Setup()
    {
        _testMesh = CreateSimpleTestMesh();
        _renderer = new IrregularMeshNs.DecorationRenderer();
        _renderer._Ready();
        _renderer.Initialize(_testMesh);
    }

    [AfterTest]
    public void Teardown()
    {
        _renderer.QueueFree();
    }

    [TestCase]
    public void TestRendererInitializes()
    {
        AssertThat(_renderer).IsNotNull();
        AssertThat(_renderer.DecorationCount).IsEqual(0);
    }

    [TestCase]
    public void TestCanPlaceDecorationOnValidQuad()
    {
        var canPlace = _renderer.CanPlaceDecoration(0, IrregularMeshNs.DecorationType.Tree);
        AssertBool(canPlace).IsTrue();
    }

    [TestCase]
    public void TestCannotPlaceDecorationOnInvalidQuad()
    {
        var canPlace = _renderer.CanPlaceDecoration(-1, IrregularMeshNs.DecorationType.Tree);
        AssertBool(canPlace).IsFalse();

        canPlace = _renderer.CanPlaceDecoration(999, IrregularMeshNs.DecorationType.Tree);
        AssertBool(canPlace).IsFalse();
    }

    [TestCase]
    public void TestCannotPlaceDecorationOnExistingDecoration()
    {
        _renderer.PlaceDecoration(0, IrregularMeshNs.DecorationType.Tree);

        var canPlace = _renderer.CanPlaceDecoration(0, IrregularMeshNs.DecorationType.Rock);
        AssertBool(canPlace).IsFalse();
    }

    [TestCase]
    public void TestPlaceDecoration()
    {
        var result = _renderer.PlaceDecoration(0, IrregularMeshNs.DecorationType.Tree);

        AssertBool(result).IsTrue();
        AssertThat(_renderer.DecorationCount).IsEqual(1);
    }

    [TestCase]
    public void TestPlaceDecorationOnInvalidQuadReturnsFalse()
    {
        var result = _renderer.PlaceDecoration(-1, IrregularMeshNs.DecorationType.Tree);
        AssertBool(result).IsFalse();
    }

    [TestCase]
    public void TestGetDecoration()
    {
        _renderer.PlaceDecoration(0, IrregularMeshNs.DecorationType.Rock, 2);

        var decoration = _renderer.GetDecoration(0);

        AssertThat(decoration).IsNotNull();
        AssertThat(decoration!.Type).IsEqual(IrregularMeshNs.DecorationType.Rock);
        AssertThat(decoration.Variation).IsEqual(2);
    }

    [TestCase]
    public void TestGetDecorationOnEmptyQuadReturnsNull()
    {
        var decoration = _renderer.GetDecoration(0);
        AssertThat(decoration).IsNull();
    }

    [TestCase]
    public void TestRemoveDecoration()
    {
        _renderer.PlaceDecoration(0, IrregularMeshNs.DecorationType.Tree);
        AssertThat(_renderer.DecorationCount).IsEqual(1);

        var result = _renderer.RemoveDecoration(0);

        AssertBool(result).IsTrue();
        AssertThat(_renderer.DecorationCount).IsEqual(0);
    }

    [TestCase]
    public void TestRemoveDecorationFromEmptyQuadReturnsFalse()
    {
        var result = _renderer.RemoveDecoration(0);
        AssertBool(result).IsFalse();
    }

    [TestCase]
    public void TestDecorationPositionIsQuadCentroid()
    {
        _renderer.PlaceDecoration(0, IrregularMeshNs.DecorationType.Tree);

        var decoration = _renderer.GetDecoration(0);
        var expectedCentroid = _testMesh.Quads[0].Centroid;

        AssertThat(decoration!.Position).IsEqual(expectedCentroid);
    }

    [TestCase]
    public void TestPlaceDecorationRandomizedHasOffset()
    {
        _renderer.PlaceDecoration(0, IrregularMeshNs.DecorationType.Tree);
        _renderer.PlaceDecorationRandomized(1, IrregularMeshNs.DecorationType.Tree);

        var standardDeco = _renderer.GetDecoration(0);
        var randomDeco = _renderer.GetDecoration(1);

        // Standard has exact centroid
        AssertThat(standardDeco!.Position).IsEqual(_testMesh.Quads[0].Centroid);

        // Randomized may have slight offset (within ±2 pixels)
        var centroid = _testMesh.Quads[1].Centroid;
        var distanceFromCentroid = randomDeco!.Position.DistanceTo(centroid);
        AssertThat(distanceFromCentroid).IsLessEqual(4f); // Max offset is ~2.83 (sqrt(2*2))
    }

    [TestCase]
    public void TestPlaceDecorationRandomizedHasRotation()
    {
        _renderer.PlaceDecorationRandomized(0, IrregularMeshNs.DecorationType.Tree);

        var decoration = _renderer.GetDecoration(0);

        // Rotation should be between 0 and 2π
        AssertThat(decoration!.Rotation).IsGreaterEqual(0f);
        AssertThat(decoration.Rotation).IsLessEqual(Mathf.Pi * 2);
    }

    [TestCase]
    public void TestClear()
    {
        _renderer.PlaceDecoration(0, IrregularMeshNs.DecorationType.Tree);
        _renderer.PlaceDecoration(1, IrregularMeshNs.DecorationType.Rock);
        AssertThat(_renderer.DecorationCount).IsEqual(2);

        _renderer.Clear();

        AssertThat(_renderer.DecorationCount).IsEqual(0);
    }

    [TestCase]
    public void TestGetDecoratedQuads()
    {
        _renderer.PlaceDecoration(0, IrregularMeshNs.DecorationType.Tree);
        _renderer.PlaceDecoration(2, IrregularMeshNs.DecorationType.Rock);

        var decorated = _renderer.GetDecoratedQuads();

        AssertThat(decorated).Contains(0);
        AssertThat(decorated).Contains(2);
        AssertThat(decorated).HasSize(2);
    }

    [TestCase]
    public void TestTreeRequiresSolidGround()
    {
        // Set all vertices of quad 0 to water (type 0)
        foreach (var vertexId in _testMesh.Quads[0].VertexIds)
        {
            _testMesh.Vertices[vertexId].TerrainType = 0;
        }

        var canPlace = _renderer.CanPlaceDecoration(0, IrregularMeshNs.DecorationType.Tree);
        AssertBool(canPlace).IsFalse();
    }

    [TestCase]
    public void TestWaterLilyRequiresWater()
    {
        // Default terrain is solid ground (type 1)
        var canPlaceOnLand = _renderer.CanPlaceDecoration(0, IrregularMeshNs.DecorationType.WaterLily);
        AssertBool(canPlaceOnLand).IsFalse();

        // Set one vertex to water
        _testMesh.Vertices[_testMesh.Quads[0].VertexIds[0]].TerrainType = 0;

        var canPlaceOnWater = _renderer.CanPlaceDecoration(0, IrregularMeshNs.DecorationType.WaterLily);
        AssertBool(canPlaceOnWater).IsTrue();
    }

    [TestCase]
    public void TestDecorationZIndex()
    {
        _renderer.DecorationZIndex = 15;
        AssertThat(_renderer.DecorationZIndex).IsEqual(15);
    }

    [TestCase]
    public void TestRandomSeed()
    {
        _renderer.RandomSeed = 12345;
        AssertThat(_renderer.RandomSeed).IsEqual(12345);
    }

    [TestCase]
    public void TestRenderAsSpritesCreatesChildren()
    {
        _renderer.PlaceDecoration(0, IrregularMeshNs.DecorationType.Tree);
        _renderer.PlaceDecoration(1, IrregularMeshNs.DecorationType.Rock);

        // Create a simple texture provider that returns a placeholder
        _renderer.RenderAsSprites((type, variation) =>
        {
            var image = Image.CreateEmpty(16, 16, false, Image.Format.Rgba8);
            return ImageTexture.CreateFromImage(image);
        });

        // The sprite container should have 2 children
        var spriteContainer = _renderer.GetNode<Node2D>("Decorations");
        AssertThat(spriteContainer.GetChildCount()).IsEqual(2);
    }

    /// <summary>
    /// Create a simple 2x2 quad test mesh with 9 vertices.
    /// </summary>
    private static IrregularMeshNs.IrregularMesh CreateSimpleTestMesh()
    {
        var mesh = new IrregularMeshNs.IrregularMesh();

        // Create 3x3 grid of vertices
        var positions = new[]
        {
            new Vector2(0, 0), new Vector2(16, 0), new Vector2(32, 0),
            new Vector2(0, 16), new Vector2(16, 16), new Vector2(32, 16),
            new Vector2(0, 32), new Vector2(16, 32), new Vector2(32, 32)
        };

        foreach (var pos in positions)
        {
            var id = mesh.AddVertex(pos);
            mesh.Vertices[id].TerrainType = 1; // Solid ground
        }

        // Add 4 quads
        mesh.AddQuad(new[] { 0, 1, 4, 3 });
        mesh.AddQuad(new[] { 1, 2, 5, 4 });
        mesh.AddQuad(new[] { 3, 4, 7, 6 });
        mesh.AddQuad(new[] { 4, 5, 8, 7 });

        mesh.UpdateAllCachedProperties();
        mesh.BuildAdjacency();

        return mesh;
    }
}
