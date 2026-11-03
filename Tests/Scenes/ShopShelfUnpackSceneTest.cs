using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using CardCleaner.Scripts.Core.Interfaces;
using CardCleaner.Scripts.Features.Packs.Components;
using CardCleaner.Scripts.Features.Menu.Services;
using CardCleaner.Scripts.Features.Packs.Models;
using CardCleaner.Scripts.Features.Player.Controllers;
using CardCleaner.Scripts.Features.Shop.Components;
using CardCleaner.Scripts.Features.Shop.Models;
using CardCleaner.Scripts.Features.Shop.Services;
using CardCleaner.Tests.TestUtilities;
using Godot;
using NSubstitute;

namespace CardCleaner.Tests.Scenes;

/// <summary>
/// End to end in the shop scene: a carried box or pack, aimed at a product shelf or storage rack, unpacks into the
/// shelf's free slots when Q is pressed.
/// The input service is replaced so the scene's Q callback is captured and invoked directly; the unpack target is
/// found by the hand's real ray because the camera is aimed at the shelf.
/// </summary>
[TestSuite]
[RequireGodotRuntime]
public class ShopShelfUnpackSceneTest
{
    private const ulong Seed = 4242;
    private const float StandingDistance = 2.0f;
    // InteractionSystem raycasts once per 0.05 s of physics time (about three frames at 60 Hz), so the aim has to
    // outlast one full interval before the press, or the target is still the one from before the aim.
    private const int AimFrames = 4;
    private const int UnpackFrames = 20;
    private const int SettleFrames = 180;
    // Half the 1.8 m capsule in Scenes/Components/player.tscn: the body stands on the floor at the box's height.
    private const float PlayerStandHeight = 0.9f;
    private const string ProductShelfPath = "World/Storefront/ProductShelf";
    private const string StorageRackPath = "World/RightStorage/StorageRack";

    private Node3D _shop = null!;
    private PlayerController _player = null!;
    private InteractionSystem _interaction = null!;
    private ContainerHolder _holder = null!;
    private MoneyService _money = null!;
    private OrderingService _ordering = null!;
    private Action? _qPress;
    private string _optionsPath = null!;

    [BeforeTest]
    public async Task Setup()
    {
        // The shop's pause and options menus resolve OptionsStore when they ready, so the test registers one on its own
        // temp file (the same pattern as OptionsMenuSceneTest) instead of reading the player's settings.
        _optionsPath = $"user://test_shop_shelf_unpack_{Guid.NewGuid():N}.cfg";
        TestServices.Container.RegisterSingleton<OptionsStore>(new OptionsStore(_optionsPath));

        _shop = GD.Load<PackedScene>(ShopSceneProbe.ScenePath).Instantiate<Node3D>();
        _player = _shop.GetNode<PlayerController>("Player");
        _interaction = _player.GetNode<InteractionSystem>("Head/Camera3D/InteractionSystem");
        _holder = _player.GetNode<ContainerHolder>("CardInteraction/ContainerHolder");
        _money = _shop.GetNode<MoneyService>("Services/MoneyService");
        _ordering = _shop.GetNode<OrderingService>("Services/OrderingService");

        // The test scene is not the current scene, so hand the scene-owned services over directly.
        ShopSceneServices.RegisterWithCardSpawner(_shop, Seed);

        // Captures the container_open callback the scene registers, so the test presses Q the way the device would.
        var input = Substitute.For<IInputService>();
        TestServices.Container.RegisterSingleton(input);
        input.When(x => x.RegisterAction(Arg.Any<object>(), "container_open", Key.Q, Arg.Any<Action>()))
            .Do(ci => _qPress = ci.Arg<Action>());

        AddNode(_shop);
        await PhysicsFrames(AimFrames);
    }

    [AfterTest]
    public void Teardown()
    {
        TestServices.Reset();
        if (FileAccess.FileExists(_optionsPath))
            DirAccess.RemoveAbsolute(ProjectSettings.GlobalizePath(_optionsPath));
    }

    [TestCase]
    [TestCategory("Unit")]
    public async Task CarriedBoxFacingProductShelfStocksThreePacks()
    {
        var shelf = await StockProductShelf();

        AssertThat(shelf.StockedCount).IsEqual(shelf.SlotCount);
        AssertBool(shelf.StockedCards.All(c => c is CardContainer { Kind: CardContainerKind.Pack })).IsTrue();
        AssertThat(Containers(CardContainerKind.Pack).Length).IsEqual(CardContainerLayout.ItemsPerContainer);
    }

    [TestCase]
    [TestCategory("Unit")]
    public async Task CarriedBoxFacingAwayFromShelfOpensLoose()
    {
        var shelf = _shop.GetNode<CardShelf>(ProductShelfPath);
        var box = OrderBox();
        await PhysicsFrames(SettleFrames);
        AssertBool(_holder.TryPickUp(box)).IsTrue();
        var standAt = shelf.GlobalPosition + new Vector3(0f, PlayerStandHeight, -StandingDistance);
        Stand(standAt, standAt + new Vector3(0f, 0f, -10f));
        await PhysicsFrames(AimFrames);

        _qPress!.Invoke();
        await PhysicsFrames(UnpackFrames);

        AssertThat(shelf.StockedCount).IsEqual(0);
        AssertThat(Containers(CardContainerKind.Pack).Length).IsEqual(CardContainerLayout.ItemsPerContainer);
    }

    [TestCase]
    [TestCategory("Unit")]
    public async Task CarriedPackFacingStorageRackStocksFourBoosters()
    {
        var rack = _shop.GetNode<CardShelf>(StorageRackPath);
        var box = OrderBox();
        await PhysicsFrames(SettleFrames);
        AssertBool(_holder.TryPickUp(box)).IsTrue();
        // Q with no unpack target drops the box and opens it loose, which leaves loose packs to carry.
        _qPress!.Invoke();
        await PhysicsFrames(UnpackFrames);
        await PhysicsFrames(SettleFrames);
        var pack = Containers(CardContainerKind.Pack)[0];
        AssertBool(_holder.TryPickUp(pack)).IsTrue();

        Stand(rack.GlobalPosition + new Vector3(0f, PlayerStandHeight, StandingDistance),
            rack.GlobalPosition + new Vector3(0f, 1.0f, 0f));
        await PhysicsFrames(AimFrames);
        _qPress.Invoke();
        await PhysicsFrames(UnpackFrames);

        AssertThat(rack.StockedCount).IsEqual(rack.SlotCount);
        AssertBool(rack.StockedCards.All(c => c is CardContainer { Kind: CardContainerKind.Booster })).IsTrue();
    }

    // Orders a box, carries it to the product shelf, and presses Q so it unpacks into the shelf's free slots.
    private async Task<CardShelf> StockProductShelf()
    {
        var shelf = _shop.GetNode<CardShelf>(ProductShelfPath);
        var box = OrderBox();
        await PhysicsFrames(SettleFrames);
        AssertBool(_holder.TryPickUp(box)).IsTrue();
        Stand(shelf.GlobalPosition + new Vector3(0f, PlayerStandHeight, -StandingDistance),
            shelf.GlobalPosition + new Vector3(0f, 0.5f, 0f));
        await PhysicsFrames(AimFrames);

        _qPress!.Invoke();
        await PhysicsFrames(UnpackFrames);
        return shelf;
    }

    // Puts the player at standAt and turns the camera toward lookAt, so the hand's ray reaches what the player faces.
    private void Stand(Vector3 standAt, Vector3 lookAt)
    {
        _player.GlobalPosition = standAt;
        _interaction.Camera!.LookAt(lookAt);
    }

    private CardContainer OrderBox()
    {
        var item = Catalog().Items.Single(i => i.Id == "cardboard_box");
        _money.Add(item.Price);
        var result = _ordering.Order(item);
        AssertBool(result.Succeeded).IsTrue();
        return (CardContainer)result.Spawned!;
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
}
