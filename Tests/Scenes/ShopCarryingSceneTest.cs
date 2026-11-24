using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using CardCleaner.Scripts.Core.Interfaces;
using CardCleaner.Scripts.Features.Card.Controllers;
using CardCleaner.Scripts.Features.Hand.Components;
using CardCleaner.Scripts.Features.Packs.Components;
using CardCleaner.Scripts.Features.Packs.Models;
using CardCleaner.Scripts.Features.Player.Controllers;
using CardCleaner.Scripts.Features.Shop.Models;
using CardCleaner.Scripts.Features.Shop.Services;
using CardCleaner.Scripts.Features.Shop.Ui;
using CardCleaner.Tests.Features.Packs.Components;
using CardCleaner.Tests.TestUtilities.Helpers;
using Godot;
using NSubstitute;
using CardCleaner.Tests.TestUtilities;
using System;

namespace CardCleaner.Tests.Scenes;

/// <summary>
/// End to end in the shop scene: the player's interaction ray picks up an ordered box with a left click, a right
/// press opens and closes it in hand, Q puts it down in front of the camera, and a held box and a card in hand block
/// each other. A booster pack is the exception: it is picked up with cards in hand, wheeled to the front and opens
/// into them.
/// The input service is replaced so the scene's own registered callbacks are captured and invoked directly; the
/// interaction ray itself runs for real because the camera is aimed at the box.
/// </summary>
[TestSuite]
[RequireGodotRuntime]
public class ShopCarryingSceneTest
{
    private const string CardScene = "res://Scenes/Components/CardShader.tscn";
    private const ulong Seed = 4242;
    private const float StandingDistance = 2.0f;
    private const int SettleFrames = 180;
    // InteractionSystem raycasts once per 0.05 s of physics time (about three frames at 60 Hz), so the aim has to
    // outlast one full interval before the click, or the target is still the one from before the aim.
    private const int AimFrames = 4;
    // Half the 1.8 m capsule in Scenes/Components/player.tscn: the body stands on the floor at the box's height.
    private const float PlayerStandHeight = 0.9f;

    private Node3D _shop = null!;
    private Node3D _world = null!;
    private PlayerController _player = null!;
    private InteractionSystem _interaction = null!;
    private IHand _hand = null!;
    private CardController _card = null!;
    private MoneyService _money = null!;
    private OrderingService _ordering = null!;
    private Action<MouseActionArgs>? _leftClick;
    private Action<MouseActionArgs>? _rightPress;
    private Action? _qPress;
    private Action<MouseActionArgs>? _wheelUp;

    private FastClock? _clock;

    [BeforeTest]
    public async Task Setup()
    {
        _clock = FastClock.Start();
        _shop = GD.Load<PackedScene>(ShopSceneProbe.ScenePath).Instantiate<Node3D>();
        _world = _shop.GetNode<Node3D>("World");
        _player = _shop.GetNode<PlayerController>("Player");
        _interaction = _player.GetNode<InteractionSystem>("Head/Camera3D/InteractionSystem");
        _hand = _player.GetNode<IHand>("CardInteraction/Hand");
        _money = _shop.GetNode<MoneyService>("Services/MoneyService");
        _ordering = _shop.GetNode<OrderingService>("Services/OrderingService");

        // The test scene is not the current scene, so hand the scene-owned services over directly.
        ShopSceneServices.RegisterWithCardSpawner(_shop, Seed);

        CaptureInputCallbacks();

        // A card on the floor for the card-click cases. It joins the Cards group in its _Ready, which runs before the
        // Player's CardHighlighter readies (World precedes Player in the shop), so the highlighter connects to it.
        _card = GD.Load<PackedScene>(CardScene).Instantiate<CardController>();
        _card.Position = new Vector3(-30f, 5f, -30f);
        _world.AddChild(_card);

        AddNode(_shop);
        await PhysicsFrames(AimFrames);
    }

    // Captures the four callbacks the scene registers, so the test presses them the way the device would.
    private void CaptureInputCallbacks()
    {
        var input = Substitute.For<IInputService>();
        TestServices.Container.RegisterSingleton(input);
        input.When(x => x.RegisterAction(Arg.Any<object>(), "interact", MouseButton.Left, Arg.Any<Action<MouseActionArgs>>()))
            .Do(ci => _leftClick = ci.Arg<Action<MouseActionArgs>>());
        input.When(x => x.RegisterAction(Arg.Any<object>(), "card_drop_prepare", MouseButton.Right,
                Arg.Any<Action<MouseActionArgs>>()))
            .Do(ci => _rightPress = ci.Arg<Action<MouseActionArgs>>());
        input.When(x => x.RegisterAction(Arg.Any<object>(), "hand_put_down", Key.Q, Arg.Any<Action>()))
            .Do(ci => _qPress = ci.Arg<Action>());
        input.When(x => x.RegisterAction(Arg.Any<object>(), "hand_next", MouseButton.WheelUp,
                Arg.Any<Action<MouseActionArgs>>()))
            .Do(ci => _wheelUp = ci.Arg<Action<MouseActionArgs>>());
    }

    [AfterTest]
    public void Teardown()
    {
        _clock?.Dispose();
        TestServices.Reset();
    }

    [TestCase]
    public async Task LeftClick_PicksUpBox()
    {
        var box = OrderBox();
        await PhysicsFrames(SettleFrames);
        Aim(box);
        await PhysicsFrames(AimFrames);

        var hit = AimedHit();
        AssertBool(hit == box).OverrideFailureMessage($"aimed ray hit {hit?.Name ?? "nothing"}, expected {box.Name}")
            .IsTrue();
        _leftClick!.Invoke(new MouseActionArgs(true, false));

        AssertThat(_hand.Items).ContainsExactly(box);
        AssertBool(box.GetParent() == _interaction.Camera).IsTrue();
    }

    [TestCase]
    public async Task Q_PutsTheBoxDown_InFrontOfCamera()
    {
        var box = OrderBox();
        await PhysicsFrames(SettleFrames);
        Aim(box);
        await PhysicsFrames(AimFrames);
        _leftClick!.Invoke(new MouseActionArgs(true, false));
        var camera = _interaction.Camera!;
        var expected = camera.GlobalPosition + (-camera.GlobalBasis.Z * ((Scripts.Features.Hand.Components.Hand)_hand).PutDownDistance);

        _qPress!.Invoke();

        AssertInt(_hand.Count).IsEqual(0);
        AssertBool(box.GetParent() == _shop.GetNode("World/Cards")).IsTrue();
        AssertBool(box.GlobalPosition.DistanceTo(expected) < 0.01f)
            .OverrideFailureMessage($"box dropped at {box.GlobalPosition}, expected {expected}")
            .IsTrue();
    }

    [TestCase]
    public async Task RightPress_OpensTheHeldBoxInHandAndSpillsNothing()
    {
        var box = OrderBox();
        await PhysicsFrames(SettleFrames);
        Aim(box);
        await PhysicsFrames(AimFrames);
        _leftClick!.Invoke(new MouseActionArgs(true, false));

        _rightPress!.Invoke(new MouseActionArgs(true, false));
        _rightPress.Invoke(new MouseActionArgs(false, false));
        await PhysicsFrames(AimFrames);

        AssertThat(_hand.Front).IsSame(box);
        AssertBool(box.IsLidOpen).IsTrue();
        AssertBool(box.IsOpened).IsFalse();
        AssertThat(Containers(CardContainerKind.Pack).Length).IsEqual(0);
    }

    [TestCase]
    public async Task CardsHeld_BlocksBoxPickup()
    {
        var box = OrderBox();
        await PhysicsFrames(SettleFrames);
        Aim(box);
        await PhysicsFrames(AimFrames);
        AssertBool(_hand.TryTake(_card)).IsTrue();

        _leftClick!.Invoke(new MouseActionArgs(true, false));

        AssertThat(_hand.Items).ContainsExactly(_card);
        AssertBool(box.GetParent() == _interaction.Camera).IsFalse();
    }

    // The player's whole path for a booster pack: picked up with a card already in hand, which stays in front, wheeled
    // to the front, torn open with the right button, and its eight real cards join the hand after the card that was
    // there.
    [TestCase]
    public async Task ABoosterPickedUpWithACardInHandOpensIntoTheHand()
    {
        var booster = Order("booster");
        await PhysicsFrames(SettleFrames);
        Aim(booster);
        await PhysicsFrames(AimFrames);
        AssertBool(_hand.TryTake(_card)).IsTrue();

        _leftClick!.Invoke(new MouseActionArgs(true, false));
        AssertThat(_hand.Items).ContainsExactly(_card, booster);
        _wheelUp!.Invoke(new MouseActionArgs(true, false));
        _wheelUp.Invoke(new MouseActionArgs(false, false));
        AssertThat(_hand.Front).IsSame(booster);

        _rightPress!.Invoke(new MouseActionArgs(true, false));
        _rightPress.Invoke(new MouseActionArgs(false, false));
        AssertBool(_hand.IsOpening).IsTrue();
        await ContainerOpeningWait.Until(() => !_hand.IsOpening, "the booster opening in the hand");

        var hand = _hand.Cards;
        AssertThat(hand.Count).IsEqual(9);
        AssertBool(hand[0] == _card).IsTrue();
        AssertThat(hand.Skip(1).OfType<CardController>().Count()).IsEqual(8);
        AssertBool(GodotObject.IsInstanceValid(booster)).IsFalse();
        AssertThat(_hand.Items.OfType<CardContainer>().Count()).IsEqual(0);
    }

    [TestCase]
    public async Task ContainerHeld_BlocksCardPickup()
    {
        var box = OrderBox();
        await PhysicsFrames(SettleFrames);
        Aim(box);
        await PhysicsFrames(AimFrames);
        _leftClick!.Invoke(new MouseActionArgs(true, false));
        AssertThat(_hand.Items).ContainsExactly(box);

        // A card click while the box is held must not reach the hand.
        RouterClicks.Click(_interaction.Router!, _card);
        AssertBool(_hand.Cards.Count == 0).IsTrue();

        // Once the box is put down, the same card click is accepted, which proves the click is wired.
        _qPress!.Invoke();
        RouterClicks.Click(_interaction.Router!, _card);
        AssertThat(_hand.Cards).ContainsExactly(_card);
    }

    [TestCase]
    public async Task PickUpDropPickUp_Works()
    {
        var box = OrderBox();
        await PhysicsFrames(SettleFrames);
        Aim(box);
        await PhysicsFrames(AimFrames);
        _leftClick!.Invoke(new MouseActionArgs(true, false));
        _qPress!.Invoke();
        AssertInt(_hand.Count).IsEqual(0);

        await PhysicsFrames(SettleFrames);
        Aim(box);
        await PhysicsFrames(AimFrames);
        _leftClick!.Invoke(new MouseActionArgs(true, false));

        AssertThat(_hand.Items).ContainsExactly(box);
        AssertBool(box.GetParent() == _interaction.Camera).IsTrue();
    }

    // Puts the player StandingDistance in front of the box on the same ground and turns the camera onto it, so the
    // interaction ray from the camera reaches the box the way a player's aim does.
    private void Aim(CardContainer box)
    {
        // Into the middle of the container: a box stands 0.6 m tall, a booster pack lies 0.045 m thick.
        var height = box.Kind == CardContainerKind.Booster ? 0.02f : 0.2f;
        _player.GlobalPosition = box.GlobalPosition + new Vector3(0f, PlayerStandHeight, StandingDistance);
        _interaction.Camera!.LookAt(box.GlobalPosition + new Vector3(0f, height, 0f));
    }

    private CardContainer OrderBox() => Order("cardboard_box");

    private CardContainer Order(string id)
    {
        var item = Catalog().Items.Single(i => i.Id == id);
        _money.Add(item.Price);
        var result = _ordering.Order(item);
        AssertBool(result.Succeeded).IsTrue();
        return (CardContainer)result.Spawned!;
    }

    // The collider the interaction ray hits from the camera along its forward axis, as InteractionSystem casts it.
    private Node? AimedHit()
    {
        var camera = _interaction.Camera!;
        var origin = camera.GlobalPosition;
        var result = _shop.GetWorld3D().DirectSpaceState.IntersectRay(new PhysicsRayQueryParameters3D
        {
            From = origin,
            To = origin - camera.GlobalBasis.Z * 100f,
            CollideWithBodies = true,
            CollisionMask = _interaction.InteractableCollisionMask
        });
        return result.Count > 0 ? result["collider"].Obj as Node : null;
    }

    private OrderCatalog Catalog() =>
        _shop.GetNode<StockApp>("PcDesktopViewport/StockApp").Catalog!;

    private CardContainer[] Containers(CardContainerKind kind) =>
        AllContainers(_shop).Where(c => c.Kind == kind && !c.IsOpened).ToArray();

    private static IEnumerable<CardContainer> AllContainers(Node root)
    {
        foreach (var child in root.GetChildren())
        {
            if (child is CardContainer container)
                yield return container;
            foreach (var nested in AllContainers(child))
                yield return nested;
        }
    }

    private static async Task PhysicsFrames(int count)
    {
        for (var i = 0; i < count; i++)
            await ISceneRunner.SyncPhysicsFrame;
    }
}
