using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using CardCleaner.Scripts.Core.Data;
using CardCleaner.Scripts.Core.DependencyInjection;
using CardCleaner.Scripts.Core.Interfaces;
using CardCleaner.Scripts.Features.Card.Components;
using CardCleaner.Scripts.Features.Card.Controllers;
using CardCleaner.Scripts.Features.Card.Services;
using CardCleaner.Scripts.Features.Packs.Components;
using CardCleaner.Scripts.Features.Packs.Models;
using CardCleaner.Scripts.Features.Packs.Services;
using CardCleaner.Scripts.Features.Player.Controllers;
using CardCleaner.Scripts.Features.Shop.Components;
using CardCleaner.Scripts.Features.Shop.Models;
using CardCleaner.Scripts.Features.Shop.Services;
using CardCleaner.Scripts.Core.Randomness;
using Godot;
using NSubstitute;
using CardCleaner.Tests.TestUtilities;
using System;

namespace CardCleaner.Tests.Scenes;

/// <summary>
/// End to end in the shop scene: the player's interaction ray picks up an ordered box with a left click, a right
/// press drops it in front of the camera, Q opens it in hand, and a held box and a card in hand block each other.
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
    private const int OpenFrames = 12;
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
    private CardHolder _cardHolder = null!;
    private ContainerHolder _holder = null!;
    private CardController _card = null!;
    private MoneyService _money = null!;
    private OrderingService _ordering = null!;
    private Action<bool>? _leftClick;
    private Action<bool>? _rightPress;
    private Action? _qPress;

    [BeforeTest]
    public async Task Setup()
    {
        _shop = GD.Load<PackedScene>(ShopSceneProbe.ScenePath).Instantiate<Node3D>();
        _world = _shop.GetNode<Node3D>("World");
        _player = _shop.GetNode<PlayerController>("Player");
        _interaction = _player.GetNode<InteractionSystem>("Head/Camera3D/InteractionSystem");
        _cardHolder = _player.GetNode<CardHolder>("CardInteraction/CardHolder");
        _holder = _player.GetNode<ContainerHolder>("CardInteraction/ContainerHolder");
        _money = _shop.GetNode<MoneyService>("Services/MoneyService");
        _ordering = _shop.GetNode<OrderingService>("Services/OrderingService");

        // The test scene is not the current scene, so hand the scene-owned services over directly.
        TestServices.Reset();
        TestServices.Container.RegisterSingleton<IGameSettings>(_shop.GetNode<GameSettings>("Services/GameSettings"));
        TestServices.Container.RegisterSingleton<IMoneyService>(_money);
        TestServices.Container.RegisterSingleton<IOrderingService>(_ordering);
        TestServices.Container.RegisterSingleton(RandomSources.Create(Seed));
        TestServices.Container.RegisterSingleton(Substitute.For<ICardGenerator>());
        TestServices.Container.RegisterSingleton<ICardSpawningService>(
            _shop.GetNode<CardSpawningService>("Services/SpawningService"));
        TestServices.Container.RegisterSingleton<ICardSpawner>((ICardSpawner)_shop.GetNode("World/Cards"));

        // Captures the three callbacks the scene registers, so the test presses them the way the device would.
        var input = Substitute.For<IInputService>();
        TestServices.Container.RegisterSingleton(input);
        input.When(x => x.RegisterAction(Arg.Any<object>(), "interact", MouseButton.Left, Arg.Any<Action<bool>>()))
            .Do(ci => _leftClick = ci.Arg<Action<bool>>());
        input.When(x => x.RegisterAction(Arg.Any<object>(), "card_drop_prepare", MouseButton.Right,
                Arg.Any<Action<bool>>()))
            .Do(ci => _rightPress = ci.Arg<Action<bool>>());
        input.When(x => x.RegisterAction(Arg.Any<object>(), "container_open", Key.Q, Arg.Any<Action>()))
            .Do(ci => _qPress = ci.Arg<Action>());

        // A card on the floor for the card-click cases. It joins the Cards group in its _Ready, which runs before the
        // Player's CardHighlighter readies (World precedes Player in the shop), so the highlighter connects to it.
        _card = GD.Load<PackedScene>(CardScene).Instantiate<CardController>();
        _card.Position = new Vector3(-30f, 5f, -30f);
        _world.AddChild(_card);

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
    public async Task LeftClick_PicksUpBox()
    {
        var box = OrderBox();
        await PhysicsFrames(SettleFrames);
        Aim(box);
        await PhysicsFrames(AimFrames);

        var hit = AimedHit();
        AssertBool(hit == box).OverrideFailureMessage($"aimed ray hit {hit?.Name ?? "nothing"}, expected {box.Name}")
            .IsTrue();
        _leftClick!.Invoke(true);

        AssertBool(_holder.IsHolding).IsTrue();
        AssertThat(_holder.Held).IsSame(box);
        AssertBool(box.GetParent() == _interaction.Camera).IsTrue();
    }

    [TestCase]
    [TestCategory("Unit")]
    public async Task RightPress_DropsBox_InFrontOfCamera()
    {
        var box = OrderBox();
        await PhysicsFrames(SettleFrames);
        Aim(box);
        await PhysicsFrames(AimFrames);
        _leftClick!.Invoke(true);
        var camera = _interaction.Camera!;
        var expected = camera.GlobalPosition + (-camera.GlobalBasis.Z * _holder.DropDistance);

        _rightPress!.Invoke(true);

        AssertBool(_holder.IsHolding).IsFalse();
        AssertBool(box.GetParent() == _shop.GetNode("World/Cards")).IsTrue();
        AssertBool(box.GlobalPosition.DistanceTo(expected) < 0.01f)
            .OverrideFailureMessage($"box dropped at {box.GlobalPosition}, expected {expected}")
            .IsTrue();
    }

    [TestCase]
    [TestCategory("Unit")]
    public async Task Q_OpensHeldBox_Spawns8Packs()
    {
        var box = OrderBox();
        await PhysicsFrames(SettleFrames);
        Aim(box);
        await PhysicsFrames(AimFrames);
        _leftClick!.Invoke(true);

        _qPress!.Invoke();
        await Frames(OpenFrames);

        AssertBool(_holder.IsHolding).IsFalse();
        AssertBool(box.IsOpened).IsTrue();
        var packs = Containers(CardContainerKind.Pack);
        AssertThat(packs.Length).IsEqual(CardContainerLayout.ItemsPerContainer);
    }

    [TestCase]
    [TestCategory("Unit")]
    public async Task CardsHeld_BlocksContainerPickup()
    {
        var box = OrderBox();
        await PhysicsFrames(SettleFrames);
        Aim(box);
        await PhysicsFrames(AimFrames);
        _cardHolder.AddCard(_card);

        _leftClick!.Invoke(true);

        AssertBool(_holder.IsHolding).IsFalse();
        AssertBool(box.GetParent() == _interaction.Camera).IsFalse();
        AssertBool(_cardHolder.HasCards).IsTrue();
    }

    [TestCase]
    [TestCategory("Unit")]
    public async Task ContainerHeld_BlocksCardPickup()
    {
        var box = OrderBox();
        await PhysicsFrames(SettleFrames);
        Aim(box);
        await PhysicsFrames(AimFrames);
        _leftClick!.Invoke(true);
        AssertBool(_holder.IsHolding).IsTrue();

        // A card click while the box is held must not reach the hand.
        _card.Interact();
        AssertBool(_cardHolder.HasCards).IsFalse();

        // Once the box is dropped, the same card click is accepted, which proves the click is wired.
        _rightPress!.Invoke(true);
        _card.Interact();
        AssertBool(_cardHolder.HasCards).IsTrue();
    }

    [TestCase]
    [TestCategory("Unit")]
    public async Task PickUpDropPickUp_Works()
    {
        var box = OrderBox();
        await PhysicsFrames(SettleFrames);
        Aim(box);
        await PhysicsFrames(AimFrames);
        _leftClick!.Invoke(true);
        _rightPress!.Invoke(true);
        AssertBool(_holder.IsHolding).IsFalse();

        await PhysicsFrames(SettleFrames);
        Aim(box);
        await PhysicsFrames(AimFrames);
        _leftClick!.Invoke(true);

        AssertBool(_holder.IsHolding).IsTrue();
        AssertThat(_holder.Held).IsSame(box);
        AssertBool(box.GetParent() == _interaction.Camera).IsTrue();
    }

    // Puts the player StandingDistance in front of the box on the same ground and turns the camera onto it, so the
    // interaction ray from the camera reaches the box the way a player's aim does.
    private void Aim(CardContainer box)
    {
        _player.GlobalPosition = box.GlobalPosition + new Vector3(0f, PlayerStandHeight, StandingDistance);
        _interaction.Camera!.LookAt(box.GlobalPosition + new Vector3(0f, 0.2f, 0f));
    }

    private CardContainer OrderBox()
    {
        var item = Catalog().Items.Single(i => i.Id == "cardboard_box");
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
        _shop.GetNode<OrderTerminal>("World/Markers/PcLocation/PcTerminal").Catalog!;

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

    private static async Task Frames(int count)
    {
        for (var i = 0; i < count; i++)
            await ISceneRunner.SyncProcessFrame;
    }
}
