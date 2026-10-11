using System.Threading.Tasks;
using CardCleaner.Tests.Features.Card.Services;
using GdUnit4;
using Godot;

namespace CardCleaner.Tests.Features.Card.Components;

[TestSuite]
[RequireGodotRuntime]
public class CardMeshBakeTest
{
    private const string MeshPath = "res://Data/Card/CardMesh.tres";
    private const string CardScenePath = "res://Scenes/Components/CardShader.tscn";
    private const string RegenerateSwitch = "CARDCLEANER_REGENERATE_CARD_MESH";
    private const float Width = 0.32f;
    private const float Height = 0.45f;
    private const float Thickness = 0.005f;

    // Observed with RegenerateCommittedMesh, then pinned. They are checked before any element comparison, so an
    // empty or changed mesh fails here rather than comparing equal to another empty mesh.
    private const int ExpectedSurfaceCount = 1;
    private const int ExpectedVertexCount = 228;
    private const int ExpectedIndexCount = 0;

    [TestCase]
    [TestCategory("Unit")]
    public static async Task CommittedMeshMatchesRecipe()
    {
        var committed = ResourceLoader.Load<ArrayMesh>(MeshPath);
        Assertions.AssertThat(committed).IsNotNull();
        var root = CardMeshRecipe.BuildCsg(Width, Height, Thickness);
        Assertions.AddNode(root);
        var fresh = await BakeRecipe(root);
        AssertSameMesh(fresh, committed!);
    }

    [TestCase]
    [TestCategory("Unit")]
    public static void SpawnedCardUsesCommittedMesh()
    {
        var card = ResourceLoader.Load<PackedScene>(CardScenePath).Instantiate<Node3D>();
        Assertions.AddNode(card);
        var body = card.GetNode<MeshInstance3D>("OuterBox_Baked");
        Assertions.AssertString(body.Mesh.ResourcePath).IsEqual(MeshPath);
    }

    [TestCase]
    public static async Task RegenerateCommittedMesh()
    {
        // Only a run started with CARDCLEANER_REGENERATE_CARD_MESH=1 writes the committed file.
        if (OS.GetEnvironment(RegenerateSwitch) != "1") return;

        var root = CardMeshRecipe.BuildCsg(Width, Height, Thickness);
        Assertions.AddNode(root);
        var mesh = await BakeRecipe(root);
        Assertions.AssertInt(mesh.GetSurfaceCount()).IsGreater(0);
        var arrays = mesh.SurfaceGetArrays(0);
        var vertexCount = arrays[(int)Mesh.ArrayType.Vertex].AsVector3Array().Length;
        var indexCount = arrays[(int)Mesh.ArrayType.Index].AsInt32Array().Length;
        Assertions.AssertInt(vertexCount).IsGreater(0);
        GD.Print($"card mesh: surfaces={mesh.GetSurfaceCount()} vertices={vertexCount} " +
                 $"indices={indexCount} aabb={mesh.GetAabb()}");

        DirAccess.MakeDirRecursiveAbsolute(ProjectSettings.GlobalizePath("res://Data/Card"));
        Assertions.AssertInt((int)ResourceSaver.Save(mesh, MeshPath)).IsEqual((int)Error.Ok);
    }

    // Godot updates CSG geometry in a deferred call, so the recipe's bake is null until that update has run.
    private static async Task<ArrayMesh> BakeRecipe(CsgBox3D root)
    {
        ArrayMesh? mesh = null;
        await CardEffectBakeWait.Until(() => (mesh = CardMeshRecipe.TryBake(root, Width, Height)) != null,
            "the recipe's CSG bake");
        Assertions.AssertThat(mesh).IsNotNull();
        return mesh!;
    }

    private static void AssertSameMesh(ArrayMesh actual, ArrayMesh expected)
    {
        Assertions.AssertInt(actual.GetSurfaceCount()).IsEqual(ExpectedSurfaceCount);
        Assertions.AssertThat(actual.SurfaceGetPrimitiveType(0)).IsEqual(expected.SurfaceGetPrimitiveType(0));
        Assertions.AssertThat(actual.GetAabb()).IsEqual(expected.GetAabb());

        // The byte-level proof: the surface dictionaries hold the packed vertex, normal, UV and index bytes.
        Assertions.AssertString(GD.VarToStr(actual.Get("_surfaces")))
            .IsEqual(GD.VarToStr(expected.Get("_surfaces")));

        var actualArrays = actual.SurfaceGetArrays(0);
        var expectedArrays = expected.SurfaceGetArrays(0);
        var vertices = actualArrays[(int)Mesh.ArrayType.Vertex].AsVector3Array();
        Assertions.AssertInt(vertices.Length).IsEqual(ExpectedVertexCount);
        AssertElementsEqual(vertices, expectedArrays[(int)Mesh.ArrayType.Vertex].AsVector3Array());
        AssertElementsEqual(
            actualArrays[(int)Mesh.ArrayType.Normal].AsVector3Array(),
            expectedArrays[(int)Mesh.ArrayType.Normal].AsVector3Array());
        AssertElementsEqual(
            actualArrays[(int)Mesh.ArrayType.TexUV].AsVector2Array(),
            expectedArrays[(int)Mesh.ArrayType.TexUV].AsVector2Array());
        var indices = actualArrays[(int)Mesh.ArrayType.Index].AsInt32Array();
        Assertions.AssertInt(indices.Length).IsEqual(ExpectedIndexCount);
        AssertElementsEqual(indices, expectedArrays[(int)Mesh.ArrayType.Index].AsInt32Array());
    }

    private static void AssertElementsEqual<T>(T[] actual, T[] expected)
    {
        Assertions.AssertInt(actual.Length).IsEqual(expected.Length);
        for (var i = 0; i < actual.Length; i++) Assertions.AssertThat(actual[i]).IsEqual(expected[i]);
    }
}
