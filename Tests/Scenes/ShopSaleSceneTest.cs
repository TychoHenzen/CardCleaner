using System.IO;
using System.Linq;
using System.Threading.Tasks;
using CardCleaner.Scripts.Core.Data;
using CardCleaner.Scripts.Core.DependencyInjection;
using CardCleaner.Scripts.Core.Interfaces;
using CardCleaner.Scripts.Features.Card.Controllers;
using CardCleaner.Scripts.Features.Card.Models;
using CardCleaner.Scripts.Features.Player.Controllers;
using CardCleaner.Scripts.Features.Shop.Components;
using CardCleaner.Scripts.Features.Shop.Models;
using CardCleaner.Scripts.Features.Shop.Services;
using Godot;

namespace CardCleaner.Tests.Scenes;

/// <summary>
/// End to end in the shop scene: an ordered shelf takes cards in its slots, the checkout counter is
/// reachable by the player's interaction ray, and interacting with it sells a card for the fixed
/// price and updates the balance the counter displays.
/// </summary>
[TestSuite]
[RequireGodotRuntime]
public class ShopSaleSceneTest
{
    private const float StandingDistanceToRegister = 2.9f;
    private const string CounterArtPath = "res://Assets/Synty/SimpleShopInterior/SI_Prop_CheckoutCounter_01.fbx";

    private Node3D _shop = null!;
    private PlayerController _player = null!;
    private SaleRegister _register = null!;
    private MoneyService _money = null!;
    private OrderingService _ordering = null!;

    [BeforeTest]
    public async Task Setup()
    {
        _shop = GD.Load<PackedScene>(ShopSceneProbe.ScenePath).Instantiate<Node3D>();
        _player = _shop.GetNode<PlayerController>("Player");
        _register = _shop.GetNode<SaleRegister>("World/Storefront/CheckoutCounter");
        _money = _shop.GetNode<MoneyService>("Services/MoneyService");
        _ordering = _shop.GetNode<OrderingService>("Services/OrderingService");

        // The test scene is not the current scene, so hand the scene-owned services over directly.
        ServiceLocator.ResetForTesting();
        ServiceLocator.Container.RegisterSingleton<IGameSettings>(_shop.GetNode<GameSettings>("Services/GameSettings"));
        ServiceLocator.Container.RegisterSingleton<IMoneyService>(_money);
        ServiceLocator.Container.RegisterSingleton<IOrderingService>(_ordering);
        ServiceLocator.Container.RegisterSingleton<ICardSpawner>(_shop.GetNode<Node3D>("World/Cards") as ICardSpawner
            ?? throw new System.InvalidOperationException("World/Cards must be the card spawner"));

        AddNode(_shop);
        await ISceneRunner.SyncPhysicsFrame;
        await ISceneRunner.SyncPhysicsFrame;
    }

    [AfterTest]
    public static void Teardown()
    {
        ServiceLocator.ResetForTesting();
    }

    [TestCase]
    [TestCategory("Unit")]
    public void RegisterIsTheCheckoutCounterWithItsOwnStatusLabelAndTheHandWired()
    {
        AssertThat(_register.StatusLabel).IsNotNull();
        AssertThat(_register.PlayerHolder).IsNotNull();
        AssertThat(_register.StatusLabel!.Text).Contains($"Balance: {_money.Balance}");
    }

    [TestCase]
    [TestCategory("Unit")]
    public void PlayerInteractionRayCanReachTheRegisterFromInFrontOfTheCounter()
    {
        var interaction = _player.GetNode<InteractionSystem>("Head/Camera3D/InteractionSystem");
        var from = _register.GlobalPosition + new Vector3(0f, 1.0f, StandingDistanceToRegister);
        var to = _register.GlobalPosition + new Vector3(0f, 1.0f, 0f);

        var hit = _shop.GetWorld3D().DirectSpaceState.IntersectRay(new PhysicsRayQueryParameters3D
        {
            From = from, To = to, CollideWithBodies = true, CollisionMask = interaction.InteractableCollisionMask
        });

        AssertThat(hit.Count).IsGreater(0);
        AssertThat(hit["collider"].Obj).IsEqual(_register);
        AssertBool(from.DistanceTo(_register.GlobalPosition) <= _register.InteractionRange).IsTrue();
    }

    [TestCase]
    [TestCategory("Unit")]
    public void CounterUsesAPackMeshListedInTheManifestWithAPlaceholderFallback()
    {
        var art = _register.GetNode<ShopArtSlot>("CounterArt");
        var manifest = File.ReadAllText(ProjectSettings.GlobalizePath("res://tools/synty-assets.json"));

        AssertThat(art.ArtPath).IsEqual(CounterArtPath);
        AssertThat(manifest).Contains(Path.GetFileName(CounterArtPath));
        AssertThat(art.Placeholder).IsNotNull();
        AssertBool(art.ArtLoaded || art.Placeholder!.Visible).IsTrue();
    }

    [TestCase]
    [TestCategory("Unit")]
    public async Task OrderedShelfCanBeStockedAndItsCardSoldAtTheRegister()
    {
        var shelf = OrderShelf();
        var card = await Stock(shelf, new CardSignature());
        var balance = _money.Balance;

        _register.Interact();

        AssertThat(_money.Balance).IsEqual(balance + CardPricing.FixedCardPrice);
        AssertThat(shelf.StockedCount).IsEqual(0);
        AssertBool(card.IsQueuedForDeletion()).IsTrue();
        AssertThat(_register.StatusLabel!.Text).Contains($"Balance: {_money.Balance}");
    }

    [TestCase]
    [TestCategory("Unit")]
    public void SellingWithNothingStockedLeavesTheBalanceAlone()
    {
        OrderShelf();
        var balance = _money.Balance;

        _register.Interact();

        AssertThat(_money.Balance).IsEqual(balance);
    }

    [TestCase]
    [TestCategory("Unit")]
    public async Task SecondInteractionDoesNotPayForTheSameCardAgain()
    {
        var shelf = OrderShelf();
        await Stock(shelf, new CardSignature());

        _register.Interact();
        var afterFirst = _money.Balance;
        _register.Interact();

        AssertThat(_money.Balance).IsEqual(afterFirst);
    }

    private CardShelf OrderShelf()
    {
        var item = _shop.GetNode<OrderTerminal>("World/Markers/PcLocation/PcTerminal").Catalog!.Items
            .First(i => i.Id == "shelf");
        _money.Add(item.Price);
        var result = _ordering.Order(item);
        AssertBool(result.Succeeded).IsTrue();
        return (CardShelf)result.Spawned!;
    }

    private async Task<CardController> Stock(CardShelf shelf, CardSignature signature)
    {
        var card = new CardController { Name = "Card1", Signature = signature };
        _shop.GetNode("World/Cards").AddChild(card);
        shelf.Slots.First().Area.EmitSignal(Area3D.SignalName.BodyEntered, card);
        await ISceneRunner.SyncPhysicsFrame;
        await ISceneRunner.SyncPhysicsFrame;
        AssertThat(shelf.StockedCount).IsEqual(1);
        return card;
    }
}
