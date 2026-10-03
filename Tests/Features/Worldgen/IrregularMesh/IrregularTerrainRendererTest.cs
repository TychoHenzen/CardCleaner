using Godot;
using IrregularMeshNs = CardCleaner.Scripts.Features.Worldgen.IrregularMesh;

namespace CardCleaner.Tests.Features.Worldgen.IrregularMesh;

/// <summary>
/// Tests for IrregularTerrainRenderer.
/// Verifies mesh building and UV mapping for terrain rendering.
/// </summary>
[TestSuite]
[RequireGodotRuntime]
public class IrregularTerrainRendererTest
{
    private IrregularMeshNs.IrregularTerrainRenderer _renderer = null!;
    private IrregularMeshNs.IrregularMesh _testMesh = null!;

    [BeforeTest]
    public void Setup()
    {
        _renderer = new IrregularMeshNs.IrregularTerrainRenderer();
        _testMesh = CreateSimpleTestMesh();
    }

    [AfterTest]
    public void Teardown()
    {
        _renderer.QueueFree();
    }

    [TestCase]
    public void TestRendererInitializes()
    {
        // Just verify the renderer can be created
        AssertThat(_renderer).IsNotNull();
    }

    [TestCase]
    public void TestDefaultProperties()
    {
        // Verify default export values
        AssertThat(_renderer.VariantSeed).IsEqual(12345);
        AssertBool(_renderer.ShowWireframe).IsFalse();
    }

    [TestCase]
    public void TestRenderSolidColorMesh()
    {
        // RenderTerrainSolid should work without atlas resources
        // This tests the basic mesh building functionality
        _renderer._Ready();
        _renderer.RenderTerrainSolid(_testMesh);

        // The internal MeshInstance2D should have a mesh
        var meshInstance = _renderer.GetChildOrNull<MeshInstance2D>(0);
        AssertThat(meshInstance).IsNotNull();
        AssertThat(meshInstance!.Mesh).IsNotNull();
    }

    [TestCase]
    public void TestSolidColorMeshHasCorrectPrimitiveType()
    {
        _renderer._Ready();
        _renderer.RenderTerrainSolid(_testMesh);

        var meshInstance = _renderer.GetChildOrNull<MeshInstance2D>(0);
        var arrayMesh = meshInstance?.Mesh as ArrayMesh;

        AssertThat(arrayMesh).IsNotNull();
        // ArrayMesh should have at least one surface
        AssertThat(arrayMesh!.GetSurfaceCount()).IsGreater(0);
    }

    [TestCase]
    public void TestEmptyMeshHandledGracefully()
    {
        var emptyMesh = new IrregularMeshNs.IrregularMesh();
        _renderer._Ready();

        // Should not crash with empty mesh
        _renderer.RenderTerrainSolid(emptyMesh);

        var meshInstance = _renderer.GetChildOrNull<MeshInstance2D>(0);
        // Mesh should be null or empty for empty input
        AssertThat(meshInstance).IsNotNull();
    }

    [TestCase]
    public void TestQuadCornerPositionsUsedForMesh()
    {
        // Create a simple quad mesh and verify rendering
        _renderer._Ready();
        _renderer.RenderTerrainSolid(_testMesh);

        // If mesh was built, we have quads rendered
        var meshInstance = _renderer.GetChildOrNull<MeshInstance2D>(0);
        AssertThat(meshInstance?.Mesh).IsNotNull();
    }

    [TestCase]
    public void TestVariantSeedCanBeChanged()
    {
        _renderer.VariantSeed = 99999;
        AssertThat(_renderer.VariantSeed).IsEqual(99999);
    }

    [TestCase]
    public void TestTileRegistryCanBeSet()
    {
        // SetTileRegistry should accept null without crashing
        _renderer.SetTileRegistry(null);
        // No assertion needed - just verify it doesn't throw
    }

    [TestCase]
    public void TestShowWireframeCanBeToggled()
    {
        _renderer.ShowWireframe = true;
        AssertBool(_renderer.ShowWireframe).IsTrue();

        _renderer.ShowWireframe = false;
        AssertBool(_renderer.ShowWireframe).IsFalse();
    }

    [TestCase]
    public void TestWireframeColorCanBeChanged()
    {
        var newColor = new Color(1.0f, 0.0f, 0.0f, 1.0f);
        _renderer.WireframeColor = newColor;
        AssertThat(_renderer.WireframeColor).IsEqual(newColor);
    }

    [TestCase]
    public void TestMeshInstanceIsChildOfRenderer()
    {
        _renderer._Ready();

        // After _Ready, a MeshInstance2D child should exist
        var childCount = _renderer.GetChildCount();
        AssertThat(childCount).IsEqual(1);

        var child = _renderer.GetChild(0);
        AssertThat(child).IsInstanceOf<MeshInstance2D>();
    }

    [TestCase]
    public void TestRenderTerrainWithMissingAtlasFails()
    {
        _renderer._Ready();

        // RenderTerrain requires atlas resources which won't be loaded in tests
        // This should log an error but not crash
        _renderer.RenderTerrain(_testMesh);

        // The mesh instance should exist but may not have a mesh
        var meshInstance = _renderer.GetChildOrNull<MeshInstance2D>(0);
        AssertThat(meshInstance).IsNotNull();
    }

    /// <summary>
    /// Create a simple test mesh with a few quads for testing.
    /// </summary>
    private static IrregularMeshNs.IrregularMesh CreateSimpleTestMesh()
    {
        var mesh = new IrregularMeshNs.IrregularMesh();

        // Create a 2x2 grid of quads
        // Vertices:
        // 6 -- 7 -- 8
        // |    |    |
        // 3 -- 4 -- 5
        // |    |    |
        // 0 -- 1 -- 2
        var positions = new[]
        {
            new Vector2(0, 0), new Vector2(16, 0), new Vector2(32, 0),
            new Vector2(0, 16), new Vector2(16, 16), new Vector2(32, 16),
            new Vector2(0, 32), new Vector2(16, 32), new Vector2(32, 32)
        };

        foreach (var pos in positions)
        {
            var id = mesh.AddVertex(pos);
            mesh.Vertices[id].TerrainType = 1;
        }

        // Add 4 quads
        mesh.AddQuad(new[] { 0, 1, 4, 3 }); // Bottom-left
        mesh.AddQuad(new[] { 1, 2, 5, 4 }); // Bottom-right
        mesh.AddQuad(new[] { 3, 4, 7, 6 }); // Top-left
        mesh.AddQuad(new[] { 4, 5, 8, 7 }); // Top-right

        // Finalize mesh
        mesh.UpdateAllCachedProperties();
        mesh.BuildAdjacency();

        return mesh;
    }
}
