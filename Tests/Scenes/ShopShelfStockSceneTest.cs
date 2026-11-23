using System;
using System.Linq;
using System.Threading.Tasks;
using CardCleaner.Scripts.Core.Interfaces;
using CardCleaner.Scripts.Features.Card.Components;
using CardCleaner.Scripts.Features.Card.Controllers;
using CardCleaner.Scripts.Features.Hand.Components;
using CardCleaner.Scripts.Features.Packs.Components;
using CardCleaner.Scripts.Features.Packs.Models;
using CardCleaner.Scripts.Features.Player.Controllers;
using CardCleaner.Scripts.Features.Shop.Shelves;
using CardCleaner.Tests.Features.Packs;
using CardCleaner.Tests.TestUtilities;
using CardCleaner.Tests.TestUtilities.Helpers;
using Godot;
using NSubstitute;

namespace CardCleaner.Tests.Scenes;

/// <summary>
///     A click on free space on a shelf level stocks the hand's front item into the level's next free seat: a card on a
///     card shelf, a held booster on a product shelf. The interaction ray runs for real from the camera aimed at the
///     seat. A click on the stock takes the level's last item back into the hand, and a level refuses stock its shelf
///     kind does not take. Items move as records, so the stocked card is a new body with the same signature.
/// </summary>
[TestSuite]
[RequireGodotRuntime]
public class ShopShelfStockSceneTest
{
    private const string CardScene = "res://Scenes/Components/CardShader.tscn";
    private const string BoosterScene = "res://Scenes/Shop/Items/Booster.tscn";
    private const ulong Seed = 4242;
    private const float StandingDistance = 1.2f;

    // Where a player stops in front of a shelf in play: far enough back that the aim at the slot comes in low.
    private const float StepBackDistance = 1.8f;

    // InteractionSystem raycasts once per 0.05 s of physics time, so the aim has to outlast one interval.
    private const int AimFrames = 6;

    // Half the 1.8 m capsule in Scenes/Components/player.tscn: the body stands on the floor.
    private const float PlayerStandHeight = 0.9f;

    private Node3D _shop = null!;
    private PlayerController _player = null!;
    private InteractionSystem _interaction = null!;
    private IHand _hand = null!;
    private CardShelf _cardShelf = null!;
    private CardShelf _productShelf = null!;
    private CardController _card = null!;
    private Action<MouseActionArgs>? _leftClick;

    [BeforeTest]
    public async Task Setup()
    {
        _shop = GD.Load<PackedScene>(ShopSceneProbe.ScenePath).Instantiate<Node3D>();
        _player = _shop.GetNode<PlayerController>("Player");
        _interaction = _player.GetNode<InteractionSystem>("Head/Camera3D/InteractionSystem");
        _hand = _player.GetNode<IHand>("CardInteraction/Hand");
        _cardShelf = _shop.GetNode<CardShelf>("World/Furniture/CardShelf/Content");
        _productShelf = _shop.GetNode<CardShelf>("World/Furniture/ProductShelf/Content");

        // The test scene is not the current scene, so the scene-owned services are handed over directly, before the
        // shop enters the tree. The level targets find the hand through its group.
        ShopSceneServices.RegisterWithCardSpawner(_shop, Seed);

        var input = Substitute.For<IInputService>();
        TestServices.Container.RegisterSingleton(input);
        input.When(x => x.RegisterAction(
                Arg.Any<object>(), "interact", MouseButton.Left, Arg.Any<Action<MouseActionArgs>>()))
            .Do(ci => _leftClick = ci.Arg<Action<MouseActionArgs>>());

        // The card joins the Cards group in its _Ready, before the player's CardHighlighter readies (World precedes
        // Player in the shop), so the highlighter puts it on the card layer, where the aim finds it, and a stocked card
        // can be picked up again.
        _card = GD.Load<PackedScene>(CardScene).Instantiate<CardController>();
        _card.Position = new Vector3(-30f, 5f, -30f);
        _shop.GetNode<Node3D>("World").AddChild(_card);

        AddNode(_shop);
        await PhysicsFrames(AimFrames);
    }

    [AfterTest]
    public static void Teardown()
    {
        TestServices.Reset();
    }

    [TestCase]
    public void EveryLevelOfEveryShelfHasOneClickTarget()
    {
        foreach (var shelf in new[] { _cardShelf, _productShelf })
        {
            var covered = shelf.LevelTargets.Select(target => target.Level).ToArray();
            var levels = shelf.Levels.Count();
            AssertThat(covered.Length).IsEqual(levels);
            AssertThat(covered.Distinct().Count()).IsEqual(levels);
        }
    }

    [TestCase]
    public async Task AClickOnAFreeCardSeatStocksTheFrontCardInHand()
    {
        var signature = _card.Signature;
        AssertBool(_hand.TryTake(_card)).IsTrue();
        var (target, seat) = TargetOf(_cardShelf, "Slot1");

        await AimAt(seat);
        AssertBool(ReferenceEquals(_interaction.Target, target))
            .OverrideFailureMessage($"aimed at {Describe(_interaction.Target)}, expected {target.Name}")
            .IsTrue();
        AssertThat(_interaction.Prompt).IsEqual("Put the card here");
        _leftClick!.Invoke(new MouseActionArgs(true, false));

        AssertInt(target.Level!.Count).IsEqual(1);
        AssertThat(((CardController)seat.Cards.Single()).Signature).IsEqual(signature);
        AssertInt(_hand.Count).IsEqual(0);
        AssertThat(target.CollisionLayer).IsEqual(4u);
    }

    // A level fills its seats in order: aimed at its last seat, the card still goes into the first free one.
    [TestCase]
    public async Task ACardPutAnywhereOnALevelGoesIntoItsFirstFreeSeat()
    {
        AssertBool(_hand.TryTake(_card)).IsTrue();
        var (target, last) = TargetOf(_cardShelf, "Slot4");

        await AimAt(last);
        _leftClick!.Invoke(new MouseActionArgs(true, false));

        AssertBool(target.Level!.Seats[0].HasCards).IsTrue();
        AssertBool(last.HasCards).IsFalse();
    }

    [TestCase]
    public async Task AimingAtTheSeatFromAStepBackFindsTheLevelTarget()
    {
        AssertBool(_hand.TryTake(_card)).IsTrue();
        var (target, seat) = TargetOf(_cardShelf, "Slot1");
        var standing = seat.GlobalPosition + ShelfFront(seat) * StepBackDistance;
        _player.GlobalPosition = new Vector3(standing.X, PlayerStandHeight, standing.Z);
        _player.Rotation = Vector3.Zero;
        _interaction.Camera!.LookAt(seat.GlobalPosition);
        await PhysicsFrames(AimFrames);

        AssertBool(ReferenceEquals(_interaction.Target, target))
            .OverrideFailureMessage($"aimed at {Describe(_interaction.Target)}, expected {target.Name}")
            .IsTrue();
    }

    // Taking a card off its seat builds it again under the camera while the old body still lies on the seat, and the
    // level must not take either back once the frames after the click have run: before the hand took cards out of
    // play, a card ended up both in the hand and locked in a seat.
    [TestCase("Slot1")]
    [TestCase("Slot5")]
    public async Task AStockedCardIsTakenBackIntoTheHandByClickingIt(string seatName)
    {
        var signature = _card.Signature;
        AssertBool(_hand.TryTake(_card)).IsTrue();
        var (target, seat) = TargetOf(_cardShelf, seatName);
        await AimAt(seat);
        _leftClick!.Invoke(new MouseActionArgs(true, false));
        AssertBool(seat.HasCards).IsTrue();

        await AimAt(seat);
        AssertThat(_interaction.Prompt).IsEqual("Pick up card");
        _leftClick!.Invoke(new MouseActionArgs(true, false));
        await PhysicsFrames(AimFrames);

        AssertInt(_hand.Count).IsEqual(1);
        var held = (CardController)_hand.Front!;
        AssertThat(held.Signature).IsEqual(signature);
        AssertBool(held.GetParent() == _interaction.Camera).IsTrue();
        AssertBool(held.Freeze).IsTrue();
        AssertThat(held.CollisionLayer).IsEqual(0u);
        AssertInt(target.Level!.Count).IsEqual(0);
    }

    [TestCase]
    public async Task AClickOnAFreeProductSeatStocksTheHeldBooster()
    {
        var booster = NewBooster();
        await PhysicsFrames(1);
        AssertBool(_hand.TryTake(booster)).IsTrue();
        var (target, seat) = TargetOf(_productShelf, "Slot1");

        await AimAt(seat);
        AssertBool(ReferenceEquals(_interaction.Target, target))
            .OverrideFailureMessage($"aimed at {Describe(_interaction.Target)}, expected {target.Name}")
            .IsTrue();
        AssertThat(_interaction.Prompt).IsEqual("Put the booster pack here");
        _leftClick!.Invoke(new MouseActionArgs(true, false));

        AssertInt(_hand.Count).IsEqual(0);
        var stocked = seat.Cards.Single();
        AssertThat(((CardContainer)stocked).Kind).IsEqual(CardContainerKind.Booster);
        AssertBool(stocked.Freeze).IsTrue();
    }

    [TestCase]
    public async Task ACardShelfLevelRefusesAHeldBooster()
    {
        var booster = NewBooster();
        await PhysicsFrames(1);
        AssertBool(_hand.TryTake(booster)).IsTrue();
        var (target, seat) = TargetOf(_cardShelf, "Slot1");

        await AimAt(seat);
        AssertObject(RouterClicks.PromptAt(_interaction.Router!, target, seat.SeatPosition(0))).IsNull();
        AssertBool(ReferenceEquals(_interaction.Target, target)).IsFalse();
        _leftClick!.Invoke(new MouseActionArgs(true, false));

        AssertBool(ReferenceEquals(_hand.Front, booster)).IsTrue();
        AssertInt(target.Level!.Count).IsEqual(0);
    }

    // The click target of the level that holds the named seat, and that seat.
    private static (ShelfLevelTarget Target, DeckSlot Seat) TargetOf(CardShelf shelf, string seatName)
    {
        var seat = shelf.Slots.Single(slot => slot.Name == seatName);
        return (shelf.LevelTargets.Single(target => target.Level == shelf.LevelOf(seat)), seat);
    }

    // Stands the player StandingDistance in front of the body (shelves face their local -Z) and turns the camera
    // onto it, so the interaction ray reaches it the way a player's aim does.
    private async Task AimAt(Node3D body)
    {
        var standing = body.GlobalPosition + ShelfFront(body) * StandingDistance;
        _player.GlobalPosition = new Vector3(standing.X, PlayerStandHeight, standing.Z);
        _player.Rotation = Vector3.Zero;
        _interaction.Camera!.LookAt(body.GlobalPosition);
        await PhysicsFrames(AimFrames);
    }

    // A slot is turned to hold its card (a card stand's -Z points up and back), so the side a player stands on is
    // the front of the shelf the body belongs to: the shelf's -Z, flattened.
    private static Vector3 ShelfFront(Node body)
    {
        var node = body;
        while (node is not CardShelf && node.GetParent() is { } parent)
            node = parent;
        var front = -((Node3D)node).GlobalBasis.Z;
        return new Vector3(front.X, 0f, front.Z).Normalized();
    }

    private CardContainer NewBooster()
    {
        var booster = GD.Load<PackedScene>(BoosterScene).Instantiate<CardContainer>();
        booster.ContentSeed = Seed;
        booster.Spawning = new RecordingCardSpawner();
        booster.Position = new Vector3(-30f, 5f, -32f);
        _shop.GetNode<Node3D>("World").AddChild(booster);
        return booster;
    }

    private static string Describe(IInteractable? target) =>
        target is Node node ? node.Name.ToString() : "nothing";

    private static async Task PhysicsFrames(int count)
    {
        for (var i = 0; i < count; i++)
            await ISceneRunner.SyncPhysicsFrame;
    }
}
