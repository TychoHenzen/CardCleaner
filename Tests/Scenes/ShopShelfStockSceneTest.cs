using System;
using System.Linq;
using System.Threading.Tasks;
using CardCleaner.Scripts.Core.Interfaces;
using CardCleaner.Scripts.Features.Card.Components;
using CardCleaner.Scripts.Features.Card.Controllers;
using CardCleaner.Scripts.Features.Packs.Components;
using CardCleaner.Scripts.Features.Player.Controllers;
using CardCleaner.Scripts.Features.Shop.Components;
using CardCleaner.Tests.Features.Packs;
using CardCleaner.Tests.TestUtilities;
using Godot;
using NSubstitute;

namespace CardCleaner.Tests.Scenes;

/// <summary>
///     A click on a free shelf slot stocks what the hand holds: the top card on a card shelf, a held booster on a
///     product shelf. The interaction ray runs for real from the camera aimed at the slot. A stocked slot leaves its
///     stock to be clicked and picked up again, and a slot refuses stock its shelf kind does not take.
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
    private CardHolder _cardHolder = null!;
    private ContainerHolder _holder = null!;
    private CardShelf _cardShelf = null!;
    private CardShelf _productShelf = null!;
    private CardController _card = null!;
    private Action<bool>? _leftClick;

    [BeforeTest]
    public async Task Setup()
    {
        _shop = GD.Load<PackedScene>(ShopSceneProbe.ScenePath).Instantiate<Node3D>();
        _player = _shop.GetNode<PlayerController>("Player");
        _interaction = _player.GetNode<InteractionSystem>("Head/Camera3D/InteractionSystem");
        _cardHolder = _player.GetNode<CardHolder>("CardInteraction/CardHolder");
        _holder = _player.GetNode<ContainerHolder>("CardInteraction/ContainerHolder");
        _cardShelf = _shop.GetNode<CardShelf>("World/Furniture/CardShelf/Content");
        _productShelf = _shop.GetNode<CardShelf>("World/Furniture/ProductShelf/Content");

        // The test scene is not the current scene, so the scene-owned services, the stock hand among them, are
        // handed over directly; registered before the shop enters the tree, the slot targets resolve the hand at once.
        ShopSceneServices.RegisterWithCardSpawner(_shop, Seed);
        TestServices.Container.RegisterSingleton<IStockHand>(_player.GetNode<CardHighlighter>("CardInteraction"));

        var input = Substitute.For<IInputService>();
        TestServices.Container.RegisterSingleton(input);
        input.When(x => x.RegisterAction(Arg.Any<object>(), "interact", MouseButton.Left, Arg.Any<Action<bool>>()))
            .Do(ci => _leftClick = ci.Arg<Action<bool>>());

        // The card joins the Cards group in its _Ready, before the player's CardHighlighter readies (World precedes
        // Player in the shop), so the highlighter connects to its click and a stocked card can be picked up again.
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
    [TestCategory("Unit")]
    public void EverySlotOfEveryShelfHasOneClickTarget()
    {
        foreach (var shelf in new[] { _cardShelf, _productShelf })
        {
            var covered = shelf.SlotTargets.Select(target => target.Slot).ToArray();
            AssertThat(covered.Length).IsEqual(shelf.SlotCount);
            AssertThat(covered.Distinct().Count()).IsEqual(shelf.SlotCount);
        }
    }

    [TestCase]
    [TestCategory("Unit")]
    public async Task AClickOnAFreeCardSlotStocksTheTopCardInHand()
    {
        var card = _card;
        _cardHolder.AddCard(card);
        var target = TargetOf(_cardShelf, "Slot2");

        await AimAt(target);
        AssertBool(ReferenceEquals(_interaction.Target, target))
            .OverrideFailureMessage($"aimed at {Describe(_interaction.Target)}, expected {target.Name}")
            .IsTrue();
        AssertThat(_interaction.Target!.Prompt).IsEqual("Put the card here");
        _leftClick!.Invoke(true);

        AssertBool(target.Slot!.Cards.Contains(card)).IsTrue();
        AssertBool(card.GetParent() == target.Slot).IsTrue();
        AssertBool(_cardHolder.HasCards).IsFalse();
        AssertThat(target.CollisionLayer).IsEqual(0u);
    }

    [TestCase]
    [TestCategory("Unit")]
    public async Task AimingAtTheSpotTheCardGoesFromAStepBackFindsTheSlotTarget()
    {
        _cardHolder.AddCard(_card);
        var target = TargetOf(_cardShelf, "Slot1");
        var slot = target.Slot!;
        var standing = slot.GlobalPosition + ShelfFront(slot) * StepBackDistance;
        _player.GlobalPosition = new Vector3(standing.X, PlayerStandHeight, standing.Z);
        _player.Rotation = Vector3.Zero;
        _interaction.Camera!.LookAt(slot.GlobalPosition);
        await PhysicsFrames(AimFrames);

        AssertBool(ReferenceEquals(_interaction.Target, target))
            .OverrideFailureMessage($"aimed at {Describe(_interaction.Target)}, expected {target.Name}")
            .IsTrue();
    }

    [TestCase]
    [TestCategory("Unit")]
    public async Task AStockedCardIsPickedUpAgainByClickingIt()
    {
        var card = _card;
        _cardHolder.AddCard(card);
        var target = TargetOf(_cardShelf, "Slot2");
        await AimAt(target);
        _leftClick!.Invoke(true);
        AssertBool(target.Slot!.Cards.Contains(card)).IsTrue();

        await AimAt(card);
        AssertBool(ReferenceEquals(_interaction.Target, card))
            .OverrideFailureMessage($"aimed at {Describe(_interaction.Target)}, expected the stocked card")
            .IsTrue();
        _leftClick!.Invoke(true);

        AssertBool(_cardHolder.HeldCards.Contains(card)).IsTrue();
        AssertBool(target.Slot.HasCards).IsFalse();
        AssertThat(target.CollisionLayer).IsEqual(4u);
    }

    [TestCase]
    [TestCategory("Unit")]
    public async Task AClickOnAFreeProductSlotStocksTheHeldBooster()
    {
        var booster = NewBooster();
        await PhysicsFrames(1);
        AssertBool(_holder.TryPickUp(booster)).IsTrue();
        var target = TargetOf(_productShelf, "Slot1");

        await AimAt(target);
        AssertBool(ReferenceEquals(_interaction.Target, target))
            .OverrideFailureMessage($"aimed at {Describe(_interaction.Target)}, expected {target.Name}")
            .IsTrue();
        AssertThat(_interaction.Target!.Prompt).IsEqual("Put the booster pack here");
        _leftClick!.Invoke(true);

        AssertBool(_holder.IsHolding).IsFalse();
        AssertBool(target.Slot!.Cards.Contains(booster)).IsTrue();
        AssertBool(booster.GetParent() == target.Slot).IsTrue();
        AssertBool(booster.Freeze).IsTrue();
    }

    [TestCase]
    [TestCategory("Unit")]
    public async Task ACardShelfSlotRefusesAHeldBooster()
    {
        var booster = NewBooster();
        await PhysicsFrames(1);
        AssertBool(_holder.TryPickUp(booster)).IsTrue();
        var target = TargetOf(_cardShelf, "Slot1");

        await AimAt(target);
        AssertBool(target.CanInteract).IsFalse();
        AssertBool(ReferenceEquals(_interaction.Target, target)).IsFalse();
        _leftClick!.Invoke(true);

        AssertBool(ReferenceEquals(_holder.Held, booster)).IsTrue();
        AssertBool(target.Slot!.HasCards).IsFalse();
    }

    private static ShelfSlotTarget TargetOf(CardShelf shelf, string slotName) =>
        shelf.SlotTargets.Single(target => target.Slot!.Name == slotName);

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
