using System.Linq;
using System.Threading.Tasks;
using CardCleaner.Scripts.Features.Card.Components;
using CardCleaner.Scripts.Features.Card.Controllers;
using CardCleaner.Tests.TestUtilities.Helpers;
using Godot;

namespace CardCleaner.Tests.Scenes;

/// <summary>
///     A real card that lands on the floor ends up resting on top of it, visible, and then frozen. Two things sank cards
///     under the shop floor. Jolt lets resting bodies sink into each other by
///     physics/jolt_physics_3d/simulation/penetration_slop, 0.02 m by default, and a card is only 5 mm thick, so the
///     setting must stay well below half that. And the card's <c>CardSleepEnforcer</c> froze it on the first physics
///     frame it moved slowly, which on landing is the frame the floor stops it, before Jolt has pushed it back out; a
///     card spawned in the shop froze 9 mm deep.
/// </summary>
[TestSuite]
[RequireGodotRuntime]
public class CardFloorRestTest
{
    private const string CardScene = "res://Scenes/Components/CardShader.tscn";
    private const float FloorThickness = 0.2f;
    private const int SettleFrames = 180;

    // How far the card's top face must stay above the floor: more than half its 5 mm, so it never hides under it.
    private const float MinimumTopClearance = 0.003f;

    // Far longer than the test runs, so the enforcer's own rest count never finishes.
    private const float UnreachableRestSeconds = 1000f;

    [TestCase]
    public static async Task ADroppedCardRestsOnTopOfTheFloor()
    {
        using var clock = FastClock.Start();
        AddFloor();
        var card = AddCard(new Vector3(0f, 0.5f, 0f), Vector3.Zero);

        await SettleOrFreeze(card);

        AssertRestsOnTop(card);
    }

    [TestCase]
    public static async Task ATumblingCardRestsOnTopOfTheFloor()
    {
        using var clock = FastClock.Start();
        AddFloor();
        var card = AddCard(new Vector3(0f, 1.2f, 0f), new Vector3(35f, 20f, 160f));
        card.AngularVelocity = new Vector3(2f, 0f, 3f);

        await SettleOrFreeze(card);

        AssertRestsOnTop(card);
    }

    [TestCase]
    public static async Task ACardThatStopsInsideTheFloorIsPushedOutBeforeItFreezes()
    {
        using var clock = FastClock.Start();
        // The state a landing card is in on the frame the floor stops it: still, and partly inside the floor.
        AddFloor();
        var card = AddCard(new Vector3(0f, -0.001f, 0f), Vector3.Zero);

        await SettleOrFreeze(card);

        AssertRestsOnTop(card);
    }

    // Jolt puts a resting card to sleep after its own half-second sleep time, the same as the enforcer's rest time, and
    // a sleeping body gets no more IntegrateForces calls. When Jolt's count finished first, the enforcer's count never
    // did, and the card lay unfrozen. With the enforcer's own count out of reach, only Jolt's sleep can freeze it.
    [TestCase]
    public static async Task ACardJoltPutsToSleepIsFrozen()
    {
        using var clock = FastClock.Start();
        AddFloor();
        var card = GD.Load<PackedScene>(CardScene).Instantiate<CardController>();
        card.GetNode<CardSleepEnforcer>("SleepEnforcer").RestSeconds = UnreachableRestSeconds;
        card.Position = new Vector3(0f, 0.5f, 0f);
        AddNode(card);

        await SettleOrFreeze(card);

        AssertRestsOnTop(card);
    }

    private static void AddFloor()
    {
        var floor = new StaticBody3D { Position = new Vector3(0f, -FloorThickness / 2f, 0f) };
        floor.AddChild(new CollisionShape3D { Shape = new BoxShape3D { Size = new Vector3(4f, FloorThickness, 4f) } });
        AddNode(floor);
    }

    private static CardController AddCard(Vector3 position, Vector3 rotationDegrees)
    {
        var card = GD.Load<PackedScene>(CardScene).Instantiate<CardController>();
        card.Position = position;
        card.RotationDegrees = rotationDegrees;
        return AddNode(card);
    }

    // Waits until the card freezes, or SettleFrames physics frames at most. A frozen card no longer moves, so checking it
    // as soon as it freezes sees what the full wait would see, and a card that freezes inside the floor still fails.
    private static async Task SettleOrFreeze(CardController card)
    {
        var first = Engine.GetPhysicsFrames();
        AssertThat(await BoundedWait.Until(
            () => card.Freeze || Engine.GetPhysicsFrames() - first >= SettleFrames,
            $"the card to freeze, or {SettleFrames} physics frames")).IsNull();
    }

    // The card lies on the floor with its top face clear of it, and the sleep enforcer has frozen it there.
    private static void AssertRestsOnTop(CardController card)
    {
        var top = WorldShapes(card).End.Y;
        AssertBool(top >= MinimumTopClearance)
            .OverrideFailureMessage($"the card's top face rests at {top} m, sunk into the floor (top at 0)").IsTrue();
        AssertBool(card.Freeze).OverrideFailureMessage("the card never came to rest and froze").IsTrue();
    }

    private static Aabb WorldShapes(RigidBody3D body)
    {
        var shape = body.GetChildren().OfType<CollisionShape3D>().First();
        return shape.GlobalTransform * shape.Shape.GetDebugMesh().GetAabb();
    }
}
