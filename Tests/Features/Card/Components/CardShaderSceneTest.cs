using GdUnit4;
using Godot;

namespace CardCleaner.Tests.Features.Card.Components;

[TestSuite]
[RequireGodotRuntime]
public class CardShaderSceneTest
{
    private const string CardScenePath = "res://Scenes/Components/CardShader.tscn";

    [TestCase]
    [TestCategory("Unit")]
    public static void OutlineBox_IsHiddenFlippedBoxMesh()
    {
        var card = ResourceLoader.Load<PackedScene>(CardScenePath).Instantiate<Node3D>();
        Assertions.AddNode(card);

        var outline = card.GetNode<MeshInstance3D>("OutlineBox");
        Assertions.AssertBool(outline.Visible).IsFalse();

        var box = outline.Mesh as BoxMesh;
        Assertions.AssertThat(box).IsNotNull();
        Assertions.AssertBool(box!.FlipFaces).IsTrue();
        Assertions.AssertBool(box.Size.IsEqualApprox(new Vector3(0.33f, 0.015f, 0.46f))).IsTrue();

        var material = box.Material as StandardMaterial3D;
        Assertions.AssertThat(material).IsNotNull();
        Assertions.AssertBool(material!.EmissionEnabled).IsTrue();
    }
}
