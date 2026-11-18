using System.Linq;
using CardCleaner.Scripts.Features.Shop.Components;
using CardCleaner.Scripts.Features.Shop.Models;
using Godot;

namespace CardCleaner.Tests.Scenes;

/// <summary>
///     The people of the shop: every look in <c>Data/Shop/PeopleLooks.tres</c> is a rigged model with every clip the
///     looks name, it stands as tall as a person, and the customer, the restocker and the cashier all wear one.
/// </summary>
[TestSuite]
[RequireGodotRuntime]
public class PeopleLooksSceneTest
{
    private const string LooksPath = "res://Data/Shop/PeopleLooks.tres";
    private const string CustomerScene = "res://Scenes/Shop/Customer.tscn";
    private const string RestockerScene = "res://Scenes/Shop/Restocker.tscn";
    private const string ShopScene = "res://Scenes/Gameplay/ShopScene.tscn";
    private const string CashierFigurePath = "World/Storefront/CheckoutCounter/CashierFigure";
    private const float HeightTolerance = 0.05f;

    [TestCase]
    public static void ThereAreTwelvePeopleToDrawFrom()
    {
        AssertThat(Looks().Models.Length).IsEqual(12);
        AssertThat(Looks().Models.Distinct().Count()).IsEqual(12);
    }

    [TestCase]
    public static void EveryLookIsARiggedPersonWithEveryClip()
    {
        var looks = Looks();
        var clips = System.Enum.GetValues<FigureClip>().Select(looks.ClipName).ToList();
        foreach (var path in looks.Models)
        {
            AssertBool(ResourceLoader.Exists(path)).OverrideFailureMessage($"{path} is missing").IsTrue();
            var model = GD.Load<PackedScene>(path).Instantiate<Node3D>();
            var player = model.FindChildren("*", nameof(AnimationPlayer), true, false).OfType<AnimationPlayer>()
                .FirstOrDefault();
            AssertThat(player).OverrideFailureMessage($"{path} has no animation player").IsNotNull();
            var missing = clips.Where(clip => !player!.HasAnimation(clip)).ToList();
            AssertThat(string.Join(", ", missing)).OverrideFailureMessage($"{path} lacks clips").IsEqual("");
            model.Free();
        }
    }

    [TestCase]
    public static void EveryLookStandsAPersonTallOnTheFloor()
    {
        var looks = Looks();
        for (var i = 0; i < looks.Models.Length; i++)
        {
            var figure = new HumanFigure { Looks = looks };
            AssertBool(figure.Wear(i)).IsTrue();

            var bounds = Bounds(figure.Model!, figure.Model!.Transform)!.Value;
            AssertFloat(bounds.Size.Y).IsEqualApprox(figure.Height, HeightTolerance);
            AssertFloat(bounds.Position.Y).IsEqualApprox(0f, HeightTolerance);
            figure.Free();
        }
    }

    [TestCase]
    public static void ACustomerIsDrawnFromThePeopleLooks()
    {
        var customer = GD.Load<PackedScene>(CustomerScene).Instantiate<Node3D>();
        var figure = customer.GetNode<HumanFigure>("Figure");

        AssertThat(figure.Looks?.ResourcePath).IsEqual(LooksPath);
        AssertThat(figure.Look).IsEqual(-1);
        AssertBool(figure.GetNode<Node3D>("Placeholder").Visible).IsTrue();
        customer.Free();
    }

    [TestCase]
    public static void TheRestockerWearsAFixedLook()
    {
        var restocker = GD.Load<PackedScene>(RestockerScene).Instantiate<Node3D>();
        var figure = restocker.GetNode<HumanFigure>("Figure");

        AssertThat(figure.Looks?.ResourcePath).IsEqual(LooksPath);
        AssertBool(figure.Wear(figure.Look)).IsTrue();
        restocker.Free();
    }

    [TestCase]
    public static void TheCashierWearsAFixedLookAndFacesTheQueue()
    {
        var shop = GD.Load<PackedScene>(ShopScene).Instantiate<Node3D>();
        var figure = shop.GetNode<HumanFigure>(CashierFigurePath);
        var queue = shop.GetNode<Node3D>("World/Storefront/CheckoutCounter/QueueSlot0");

        AssertThat(figure.Looks?.ResourcePath).IsEqual(LooksPath);
        AssertBool(figure.Wear(figure.Look)).IsTrue();
        var facing = figure.Transform.Basis * Vector3.Forward;
        var toQueue = (queue.Position - figure.Position).Normalized();
        AssertFloat(facing.Dot(toQueue)).IsGreater(0.99f);
        shop.Free();
    }

    private static FigureLooks Looks() => GD.Load<FigureLooks>(LooksPath);

    // The box around every mesh under node, in the frame toFrame maps node into.
    private static Aabb? Bounds(Node node, Transform3D toFrame)
    {
        Aabb? bounds = node is MeshInstance3D { Mesh: not null } mesh ? toFrame * mesh.GetAabb() : null;
        foreach (var child in node.GetChildren())
        {
            var childFrame = child is Node3D spatial ? toFrame * spatial.Transform : toFrame;
            if (Bounds(child, childFrame) is { } box)
                bounds = bounds is { } sofar ? sofar.Merge(box) : box;
        }

        return bounds;
    }
}
