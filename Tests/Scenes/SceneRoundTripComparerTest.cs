using Godot;

namespace CardCleaner.Tests.Scenes;

[TestSuite]
[RequireGodotRuntime]
public class SceneRoundTripComparerTest
{
    [TestCase]
    [TestCategory("Unit")]
    public static void ComparingTreesThatDifferOnlyInsideAnInlineSubResourceReportsThatPath()
    {
        var original = BuildPlaceholderTree(Colors.Red);
        var saved = BuildPlaceholderTree(Colors.Blue);
        AssertThat(Lost(original, saved)).IsEqual("Placeholder.mesh.material.albedo_color");
    }

    [TestCase]
    [TestCategory("Unit")]
    public static void ComparingTreesWithEqualButSeparateInlineSubResourcesReportsNothing()
    {
        var original = BuildPlaceholderTree(Colors.Red);
        var saved = BuildPlaceholderTree(Colors.Red);
        AssertThat(Lost(original, saved)).IsEmpty();
    }

    [TestCase]
    [TestCategory("Unit")]
    public static void InlineResourcesInAnArrayAreComparedByContent()
    {
        var original = new Node3D();
        var saved = new Node3D();
        original.SetMeta("items", new Godot.Collections.Array
        {
            new StandardMaterial3D { AlbedoColor = Colors.Red },
            new StandardMaterial3D { AlbedoColor = Colors.Red },
        });
        saved.SetMeta("items", new Godot.Collections.Array
        {
            new StandardMaterial3D { AlbedoColor = Colors.Red },
            new StandardMaterial3D { AlbedoColor = Colors.Blue },
        });
        AssertThat(Lost(original, saved)).IsEqual("metadata/items[1].albedo_color");
    }

    [TestCase]
    [TestCategory("Unit")]
    public static void InlineResourcesInADictionaryAreComparedByContent()
    {
        var original = new Node3D();
        var saved = new Node3D();
        original.SetMeta("lookup", new Godot.Collections.Dictionary
        {
            { "material", new StandardMaterial3D { AlbedoColor = Colors.Red } },
        });
        saved.SetMeta("lookup", new Godot.Collections.Dictionary
        {
            { "material", new StandardMaterial3D { AlbedoColor = Colors.Blue } },
        });
        AssertThat(Lost(original, saved)).IsEqual("metadata/lookup[\"material\"].albedo_color");
    }

    [TestCase]
    [TestCategory("Unit")]
    public static void ASubResourceSharedBeforeButSplitAfterIsReported()
    {
        var shared = new BoxMesh();
        var original = BuildTwoMeshTree(shared, shared);
        var saved = BuildTwoMeshTree(new BoxMesh(), new BoxMesh());
        AssertThat(Lost(original, saved)).IsEqual("B.mesh (sharing)");
    }

    [TestCase]
    [TestCategory("Unit")]
    public static void ASubResourceSplitBeforeButSharedAfterIsReported()
    {
        var shared = new BoxMesh();
        var original = BuildTwoMeshTree(new BoxMesh(), new BoxMesh());
        var saved = BuildTwoMeshTree(shared, shared);
        AssertThat(Lost(original, saved)).IsEqual("B.mesh (sharing)");
    }

    [TestCase]
    [TestCategory("Unit")]
    public static void ASubResourceSharedOnBothSidesReportsNothing()
    {
        var originalShared = new BoxMesh();
        var savedShared = new BoxMesh();
        var original = BuildTwoMeshTree(originalShared, originalShared);
        var saved = BuildTwoMeshTree(savedShared, savedShared);
        AssertThat(Lost(original, saved)).IsEmpty();
    }

    // Two mesh children, A and B, under one root. Passing the same mesh twice shares that one resource between them.
    private static Node3D BuildTwoMeshTree(Mesh first, Mesh second)
    {
        var root = new Node3D { Name = "Fixture" };
        root.AddChild(new MeshInstance3D { Name = "A", Mesh = first });
        root.AddChild(new MeshInstance3D { Name = "B", Mesh = second });
        return root;
    }

    // Shared with SceneEditorRoundTripSceneTest: one placeholder mesh whose box material carries the albedo colour.
    internal static Node3D BuildPlaceholderTree(Color albedo)
    {
        var root = new Node3D { Name = "Fixture" };
        var placeholder = new MeshInstance3D
        {
            Name = "Placeholder",
            Mesh = new BoxMesh { Material = new StandardMaterial3D { AlbedoColor = albedo } },
        };
        root.AddChild(placeholder);
        placeholder.Owner = root;
        return root;
    }

    // Returns the lost values of the two trees joined for one assertion, and frees both trees.
    private static string Lost(Node original, Node saved)
    {
        try
        {
            return string.Join(", ", SceneRoundTripComparer.LostValues(original, saved));
        }
        finally
        {
            original.Free();
            saved.Free();
        }
    }
}
