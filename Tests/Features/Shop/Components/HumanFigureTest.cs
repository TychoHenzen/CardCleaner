using System.Threading.Tasks;
using CardCleaner.Scripts.Features.Shop.People;
using CardCleaner.Tests.TestUtilities.Helpers;
using Godot;

namespace CardCleaner.Tests.Features.Shop.Components;

/// <summary>
///     A <see cref="HumanFigure" /> wearing a hand-built model: a 1 m box standing on its origin and an animation
///     player with the five clips <see cref="FigureLooks" /> names by default.
/// </summary>
[TestSuite]
[RequireGodotRuntime]
public class HumanFigureTest
{
    private const float Tolerance = 0.001f;
    private const float PickUpSeconds = 0.2f;
    private const string MissingModel = "res://Tests/Features/Shop/Components/no-such-person.glb";

    [TestCase]
    public static void AWornModelStandsAtTheFigureHeightOnItsOrigin()
    {
        var figure = AddNode(Figure());

        figure.Put(Model());

        var bounds = figure.Model!.Transform * MeshBox(figure.Model);
        AssertFloat(bounds.Size.Y).IsEqualApprox(figure.Height, Tolerance);
        AssertFloat(bounds.Position.Y).IsEqualApprox(0f, Tolerance);
    }

    [TestCase]
    public static void AWornModelIsTurnedByTheLooksYaw()
    {
        var figure = AddNode(Figure());
        figure.Looks!.ModelYawDegrees = 180f;

        figure.Put(Model());

        // The model's own -Z, its face for this hand-built model, now points down the figure's +Z.
        var face = figure.Model!.Transform.Basis * Vector3.Forward;
        AssertThat(face.Normalized().IsEqualApprox(Vector3.Back)).IsTrue();
    }

    [TestCase]
    public static void AWornModelHidesThePlaceholder()
    {
        var figure = AddNode(Figure());
        var placeholder = figure.GetNode<Node3D>("Placeholder");
        AssertBool(placeholder.Visible).IsTrue();

        figure.Put(Model());

        AssertBool(placeholder.Visible).IsFalse();
    }

    [TestCase]
    public static void AMissingModelKeepsThePlaceholderAndActsForNoTime()
    {
        var figure = AddNode(Figure());
        figure.Looks!.Models = [MissingModel];

        AssertBool(figure.Wear(0)).IsFalse();

        AssertThat(figure.Worn).IsEqual(-1);
        AssertBool(figure.GetNode<Node3D>("Placeholder").Visible).IsTrue();
        AssertFloat(figure.Act(FigureClip.PickUp)).IsEqual(0f);
    }

    [TestCase]
    public static void IdleAndWalkLoopAndActionsDoNot()
    {
        var figure = AddNode(Figure());

        figure.Put(Model());

        var player = figure.Animations!;
        AssertThat(player.GetAnimation("idle").LoopMode).IsEqual(Animation.LoopModeEnum.Linear);
        AssertThat(player.GetAnimation("walk").LoopMode).IsEqual(Animation.LoopModeEnum.Linear);
        AssertThat(player.GetAnimation("pick-up").LoopMode).IsEqual(Animation.LoopModeEnum.None);
        AssertThat(player.CurrentAnimation.ToString()).IsEqual("idle");
    }

    [TestCase]
    public static void AWalkingBodyTurnsTowardItsWalkAndLoopsWalk()
    {
        var body = AddNode(new Node3D());
        var figure = Figure();
        body.AddChild(figure);
        figure.Put(Model());

        // A full second turns up to 540 degrees, so the body ends facing +X.
        figure.Follow(body, new Vector3(1.6f, 0f, 0f), 1.0);

        AssertThat(Facing(body).IsEqualApprox(Vector3.Right)).IsTrue();
        AssertThat(figure.Playing).IsEqual(FigureClip.Walk);
        AssertThat(figure.Animations!.CurrentAnimation.ToString()).IsEqual("walk");
        AssertFloat(figure.Animations.SpeedScale).IsEqualApprox(1.6f / figure.Looks!.WalkClipSpeed, Tolerance);
    }

    [TestCase]
    public static void ATurnTakesTimeAtTheTurnRate()
    {
        var body = AddNode(new Node3D());
        var figure = Figure();
        body.AddChild(figure);

        // 90 degrees at 540 degrees a second takes 1/6 s; a tenth of a second turns 54 degrees of it.
        figure.Follow(body, new Vector3(-1.6f, 0f, 0f), 0.1);

        AssertFloat(Mathf.RadToDeg(body.GlobalRotation.Y)).IsEqualApprox(54f, 0.01f);
    }

    [TestCase]
    public static void AStandingBodyTurnsTowardThePointItFaces()
    {
        var body = AddNode(new Node3D());
        var figure = Figure();
        body.AddChild(figure);
        figure.Put(Model());

        figure.FaceToward(new Vector3(0f, 0f, 3f));
        figure.Follow(body, Vector3.Zero, 1.0);

        AssertThat(Facing(body).IsEqualApprox(Vector3.Back)).IsTrue();
        AssertThat(figure.Playing).IsEqual(FigureClip.Idle);
    }

    [TestCase]
    public static void WalkingForgetsThePointToFace()
    {
        var body = AddNode(new Node3D());
        var figure = Figure();
        body.AddChild(figure);

        figure.FaceToward(new Vector3(0f, 0f, 3f));
        figure.Follow(body, new Vector3(1.6f, 0f, 0f), 1.0);
        figure.Follow(body, Vector3.Zero, 1.0);

        AssertThat(Facing(body).IsEqualApprox(Vector3.Right)).IsTrue();
    }

    [TestCase]
    public static async Task AnActionPlaysOnceAndTheLoopComesBack()
    {
        var figure = AddNode(Figure());
        figure.Put(Model());

        var seconds = figure.Act(FigureClip.PickUp);

        AssertFloat(seconds).IsEqualApprox(PickUpSeconds, Tolerance);
        AssertThat(figure.Playing).IsEqual(FigureClip.PickUp);
        AssertThat(figure.Animations!.CurrentAnimation.ToString()).IsEqual("pick-up");
        var timeout = await BoundedWait.Until(() => figure.Playing == FigureClip.Idle, "the pick-up to end");
        AssertThat(timeout).OverrideFailureMessage(timeout ?? "the pick-up to end").IsNull();
        AssertThat(figure.Animations.CurrentAnimation.ToString()).IsEqual("idle");
    }

    [TestCase]
    public static void TheWalkLoopWaitsForAnActionToEnd()
    {
        var body = AddNode(new Node3D());
        var figure = Figure();
        body.AddChild(figure);
        figure.Put(Model());

        figure.Act(FigureClip.Refuse);
        figure.Follow(body, new Vector3(1.6f, 0f, 0f), 0.016);

        AssertThat(figure.Playing).IsEqual(FigureClip.Refuse);
        AssertThat(figure.Animations!.CurrentAnimation.ToString()).IsEqual("emote-no");
    }

    [TestCase]
    public static void ALoopClipIsNoAction()
    {
        var figure = AddNode(Figure());
        figure.Put(Model());

        AssertFloat(figure.Act(FigureClip.Walk)).IsEqual(0f);
        AssertThat(figure.Playing).IsEqual(FigureClip.Idle);
    }

    // The yaw-0 figure, its capsule placeholder and default looks with no model files.
    private static HumanFigure Figure()
    {
        var figure = new HumanFigure { Looks = new FigureLooks() };
        figure.AddChild(new MeshInstance3D { Name = "Placeholder", Mesh = new CapsuleMesh() });
        return figure;
    }

    // A person 1 m tall, feet on its origin, with every clip the default looks name.
    private static Node3D Model()
    {
        var model = new Node3D();
        model.AddChild(new MeshInstance3D
        {
            Mesh = new BoxMesh { Size = new Vector3(0.4f, 1f, 0.3f) },
            Position = new Vector3(0f, 0.5f, 0f),
        });
        var library = new AnimationLibrary();
        library.AddAnimation("idle", new Animation { Length = 1f });
        library.AddAnimation("walk", new Animation { Length = 1f });
        library.AddAnimation("pick-up", new Animation { Length = PickUpSeconds });
        library.AddAnimation("interact-right", new Animation { Length = PickUpSeconds });
        library.AddAnimation("emote-no", new Animation { Length = PickUpSeconds });
        var player = new AnimationPlayer { Name = "AnimationPlayer" };
        player.AddAnimationLibrary("", library);
        model.AddChild(player);
        return model;
    }

    private static Aabb MeshBox(Node3D model)
    {
        var mesh = model.GetChild<MeshInstance3D>(0);
        return mesh.Transform * mesh.GetAabb();
    }

    // The flat direction a body's -Z looks along.
    private static Vector3 Facing(Node3D body)
    {
        var forward = body.GlobalBasis * Vector3.Forward;
        return new Vector3(forward.X, 0f, forward.Z).Normalized();
    }
}
