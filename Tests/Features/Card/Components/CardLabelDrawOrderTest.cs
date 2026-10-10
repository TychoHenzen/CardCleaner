using Godot;

namespace CardCleaner.Tests.Features.Card.Components;

/// <summary>
/// The card face shader draws in the transparent pass
/// (depth_prepass_alpha), where objects of equal render priority are sorted
/// by distance. A label at the material's priority is then drawn under the
/// face on cards below the camera, so each label must sit at a higher
/// priority than the card material.
/// </summary>
[TestSuite]
[RequireGodotRuntime]
public class CardLabelDrawOrderTest
{
    private const string CardShaderScene =
        "res://Scenes/Components/CardShader.tscn";
    private const string CardMaterialPath =
        "res://Assets/Materials/CardMaterial.tres";

    [TestCase("Title")]
    [TestCase("Description")]
    [TestCategory("Unit")]
    public static void CardLabel_IsDrawnAboveTheCardFace(string labelName)
    {
        // Arrange - not added to the tree: the check reads the scene's
        // stored values, not runtime state
        var scene = GD.Load<PackedScene>(CardShaderScene);
        var card = AutoFree(scene.Instantiate())!;
        var faceMaterial = GD.Load<Material>(CardMaterialPath);
        var facePriority = faceMaterial.RenderPriority;
        var label = card.GetNode<Label3D>(labelName);

        // Assert - the text must sort after the face
        AssertInt(label.RenderPriority).IsGreater(facePriority);

        // Assert - any outline must sort after the face too, or be off
        var outlineHiddenOrAboveFace = label.OutlineSize == 0
            || label.OutlineRenderPriority > facePriority;
        AssertBool(outlineHiddenOrAboveFace)
            .OverrideFailureMessage(
                $"{labelName} outline (size {label.OutlineSize}, "
                + $"priority {label.OutlineRenderPriority}) is drawn "
                + $"under the card face (priority {facePriority})")
            .IsTrue();
    }
}
