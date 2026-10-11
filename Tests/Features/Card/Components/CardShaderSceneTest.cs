using System.Threading.Tasks;
using CardCleaner.Scripts.Features.Card.Components;
using CardCleaner.Tests.Features.Card.Services;
using GdUnit4;
using Godot;

namespace CardCleaner.Tests.Features.Card.Components;

[TestSuite]
[RequireGodotRuntime]
public class CardShaderSceneTest
{
    private const string CardScenePath = "res://Scenes/Components/CardShader.tscn";

    // The outline is the card mesh grown by this much on every axis, so a re-baked mesh must bring the outline along.
    private const float OutlineMargin = 0.01f;

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
        var bodySize = card.GetNode<MeshInstance3D>("OuterBox_Baked").Mesh.GetAabb().Size;
        var expected = bodySize + Vector3.One * OutlineMargin;
        Assertions.AssertBool(box.Size.IsEqualApprox(expected))
            .OverrideFailureMessage($"OutlineBox size {box.Size} should be the card mesh size plus margin, {expected}")
            .IsTrue();

        var material = box.Material as StandardMaterial3D;
        Assertions.AssertThat(material).IsNotNull();
        Assertions.AssertBool(material!.EmissionEnabled).IsTrue();
    }

    [TestCase]
    [TestCategory("Unit")]
    public static async Task SpawnedCard_HasNoCsgNode()
    {
        var card = ResourceLoader.Load<PackedScene>(CardScenePath).Instantiate<Node3D>();
        Assertions.AddNode(card);
        await CardEffectBakeWait.Frames(1);
        Assertions.AssertBool(ContainsCsgShape(card)).IsFalse();
    }

    [TestCase]
    [TestCategory("Unit")]
    public static void DesignerDimensionsMatchMeshAabb()
    {
        var card = ResourceLoader.Load<PackedScene>(CardScenePath).Instantiate<Node3D>();
        Assertions.AddNode(card);
        var designer = card.GetNode<CardDesigner>("Designer");
        var size = card.GetNode<MeshInstance3D>("OuterBox_Baked").Mesh.GetAabb().Size;
        var dimensions = new Vector3(designer.Width, designer.Thickness, designer.Height);
        Assertions.AssertBool(dimensions.IsEqualApprox(size)).IsTrue();
    }

    private static bool ContainsCsgShape(Node node)
    {
        foreach (var child in node.GetChildren())
        {
            if (child is CsgShape3D || ContainsCsgShape(child)) return true;
        }

        return false;
    }
}
