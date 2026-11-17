using CardCleaner.Scripts.Features.Signage;
using Godot;

namespace CardCleaner.Tests.Features.Signage;

/// <summary>
///     A sign board prints its text flat on its board, never turned to the camera, on the front and, for a two-sided
///     board, on the back facing the other way. Its rods rise from the top edge and its post drops from the bottom edge.
///     Everything it builds is internal, so a scene that holds a sign stores only the sign itself.
/// </summary>
[TestSuite]
[RequireGodotRuntime]
public class SignBoardTest
{
    [TestCase]
    [TestCategory("Unit")]
    public static void TheFrontPrintsTheTextFlatOnTheBoard()
    {
        var sign = AddNode(new SignBoard { Text = "Stock deliveries", Size = new Vector2(1.6f, 0.4f) });

        var front = sign.FrontFace!;
        AssertThat(front.Text).IsEqual("Stock deliveries");
        AssertThat(front.Billboard).IsEqual(BaseMaterial3D.BillboardModeEnum.Disabled);
        AssertBool(front.DoubleSided).IsFalse();
        AssertBool(front.Shaded).IsTrue();
        AssertBool(front.Position.Z > sign.Thickness / 2f).IsTrue();
        AssertBool(front.Width * front.PixelSize <= sign.Size.X).IsTrue();
        AssertObject(sign.BackFace).IsNull();
    }

    [TestCase]
    [TestCategory("Unit")]
    public static void ATwoSidedBoardPrintsTheBackFacingTheOtherWay()
    {
        var sign = AddNode(new SignBoard { Text = "Cabinet room", TwoSided = true });

        var back = sign.BackFace!;
        AssertThat(back.Text).IsEqual("Cabinet room");
        AssertBool(back.Position.Z < -sign.Thickness / 2f).IsTrue();
        var frontFacing = sign.FrontFace!.GlobalBasis.Z;
        var backFacing = back.GlobalBasis.Z;
        AssertBool(frontFacing.Dot(backFacing) < -0.99f).IsTrue();
    }

    [TestCase]
    [TestCategory("Unit")]
    public static void NewTextRepaintsBothFaces()
    {
        var sign = AddNode(new SignBoard { Text = "Old", TwoSided = true });

        sign.Text = "New";

        AssertThat(sign.FrontFace!.Text).IsEqual("New");
        AssertThat(sign.BackFace!.Text).IsEqual("New");
    }

    [TestCase]
    [TestCategory("Unit")]
    public static void ANewTextColourRepaintsBothFaces()
    {
        var sign = AddNode(new SignBoard { Text = "OPEN", TwoSided = true });
        var green = new Color(0.35f, 0.95f, 0.45f);

        sign.TextColor = green;

        AssertThat(sign.FrontFace!.Modulate).IsEqual(green);
        AssertThat(sign.BackFace!.Modulate).IsEqual(green);
    }

    [TestCase]
    [TestCategory("Unit")]
    public static void AScreenGlowsInsteadOfTakingTheRoomsLight()
    {
        var sign = AddNode(new SignBoard { Screen = true });

        AssertBool(sign.FrontFace!.Shaded).IsFalse();
    }

    [TestCase]
    [TestCategory("Unit")]
    public static void HangingRodsRiseFromTheTopEdgeToTheirLength()
    {
        var sign = AddNode(new SignBoard
        {
            Size = new Vector2(1.2f, 0.4f), Mount = SignMount.Hanging, MountLength = 0.6f
        });

        AssertThat(sign.MountParts.Count).IsEqual(2);
        foreach (var rod in sign.MountParts)
        {
            var bounds = rod.GetAabb();
            var bottom = rod.Position.Y + bounds.Position.Y;
            var top = bottom + bounds.Size.Y;
            AssertFloat(bottom).IsEqualApprox(0.2f, 0.001f);
            AssertFloat(top).IsEqualApprox(0.8f, 0.001f);
        }
    }

    [TestCase]
    [TestCategory("Unit")]
    public static void APostDropsFromTheBottomEdgeToItsLength()
    {
        var sign = AddNode(new SignBoard
        {
            Size = new Vector2(0.4f, 0.2f), Mount = SignMount.Post, MountLength = 0.3f
        });

        var post = sign.MountParts[0];
        var bounds = post.GetAabb();
        var top = post.Position.Y + bounds.Position.Y + bounds.Size.Y;
        var bottom = post.Position.Y + bounds.Position.Y;
        AssertThat(sign.MountParts.Count).IsEqual(1);
        AssertFloat(top).IsEqualApprox(-0.1f, 0.001f);
        AssertFloat(bottom).IsEqualApprox(-0.4f, 0.001f);
    }

    [TestCase]
    [TestCategory("Unit")]
    public static void AWallSignBuildsNoMount()
    {
        var sign = AddNode(new SignBoard { Mount = SignMount.Wall, MountLength = 1f });

        AssertThat(sign.MountParts.Count).IsEqual(0);
    }

    [TestCase]
    [TestCategory("Unit")]
    public static void AScenePacksTheSignWithoutItsBuiltParts()
    {
        var root = AddNode(new Node3D { Name = "Root" });
        var sign = new SignBoard
        {
            Name = "Sign", Text = "For sale", TwoSided = true, Mount = SignMount.Hanging, MountLength = 0.5f
        };
        root.AddChild(sign);
        sign.Owner = root;

        var packed = new PackedScene();
        AssertThat(packed.Pack(root)).IsEqual(Error.Ok);

        AssertThat(packed.GetState().GetNodeCount()).IsEqual(2);
        AssertThat(sign.GetChildCount()).IsEqual(0);
        AssertBool(sign.GetChildCount(true) > 0).IsTrue();
    }

    [TestCase]
    [TestCategory("Unit")]
    public static void AShortWordOnAWideBoardFits()
    {
        var sign = AddNode(new SignBoard { Text = "OPEN", Size = new Vector2(1.2f, 0.4f) });

        AssertBool(sign.TextFits).IsTrue();
    }

    [TestCase]
    [TestCategory("Unit")]
    public static void AWordWiderThanTheBoardDoesNotFitAndIsNotCutInTwo()
    {
        var sign = AddNode(new SignBoard { Text = "CLOSED", Size = new Vector2(0.3f, 0.4f) });

        AssertBool(sign.TextFits).IsFalse();
        AssertThat(sign.FrontFace!.AutowrapMode).IsEqual(TextServer.AutowrapMode.Word);
    }

    [TestCase]
    [TestCategory("Unit")]
    public static void MoreLinesThanTheBoardIsTallDoNotFit()
    {
        var sign = AddNode(new SignBoard { Text = "A\nB\nC\nD", Size = new Vector2(1.2f, 0.4f) });

        AssertBool(sign.TextFits).IsFalse();
    }
}
