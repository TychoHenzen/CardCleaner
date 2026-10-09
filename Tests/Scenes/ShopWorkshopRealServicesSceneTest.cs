using System.Threading.Tasks;
using CardCleaner.Scripts.Core.DependencyInjection;
using CardCleaner.Scripts.Core.Interfaces;
using CardCleaner.Scripts.Core.Services;
using CardCleaner.Scripts.Features.Card.Models;
using CardCleaner.Scripts.Features.Card.Components;
using CardCleaner.Scripts.Features.Deckbuilder.Components;
using CardCleaner.Scripts.Features.Deckbuilder.Services;
using CardCleaner.Scripts.Features.Worldgen.IrregularMesh;
using CardCleaner.Tests.Features.Shop;
using Godot;
using IServiceProvider = CardCleaner.Scripts.Core.Interfaces.IServiceProvider;

namespace CardCleaner.Tests.Scenes;

/// <summary>
/// The workshop cabinet against the registration the shop scene really produces: its own service providers
/// register into the container, the real spawning service seats the credit card, and the button runs a session.
/// </summary>
[TestSuite]
[RequireGodotRuntime]
public class ShopWorkshopRealServicesSceneTest
{
    private const int SmallMeshRings = 3;

    private SceneTree _tree = null!;
    private Node? _previousScene;
    private Node3D _shop = null!;

    [BeforeTest]
    public async Task Setup()
    {
        _tree = (SceneTree)Engine.GetMainLoop();
        _previousScene = _tree.CurrentScene;

        ServiceLocator.ResetForTesting();
        ServiceLocator.Container.RegisterSingleton(new RandomNumberGenerator());
        ServiceLocator.Container.RegisterSingleton<ITileRegistry>(new TileRegistry());

        _shop = GD.Load<PackedScene>(ShopSceneProbe.ScenePath).Instantiate<Node3D>();
        _tree.Root.AddChild(_shop);
        _tree.CurrentScene = _shop;

        foreach (var node in _tree.GetNodesInGroup("service_providers"))
        {
            if (node is IServiceProvider provider)
                provider.RegisterServices(ServiceLocator.Container);
        }

        ServiceLocator.ExecutePendingCallbacks();
        _shop.GetNode<IrregularWorldMapScreen>("World/Workshop/Cabinet/IrregularTileMapScreen").MeshRings = SmallMeshRings;
        await WorkshopSceneRig.Settle();
    }

    [AfterTest]
    public void Teardown()
    {
        ServiceLocator.ResetForTesting();
        _tree.CurrentScene = _previousScene;
        _tree.Root.RemoveChild(_shop);
        _shop.Free();
    }

    [TestCase]
    [TestCategory("Unit")]
    public void RegistrationResolvesTheWorkshopSessionServiceAndTheShopSpawner()
    {
        AssertBool(ReferenceEquals(ServiceLocator.Get<IGameSessionService>(),
            _shop.GetNode("World/Workshop/GameSessionService"))).IsTrue();
        AssertBool(ReferenceEquals(ServiceLocator.Get<ICardSpawningService>(),
            _shop.GetNode("Services/SpawningService"))).IsTrue();
    }

    [TestCase]
    [TestCategory("Unit")]
    public void RealSpawnerSeatsTheCreditCardInTheDeckHolderOnly()
    {
        var deck = _shop.GetNode<DeckSlot>("World/Workshop/Cabinet/Assembly/DeckSlot");
        var cards = _shop.GetNode<DeckSlot>("World/Workshop/Cabinet/Assembly/CardSlot");

        AssertThat(deck.Cards.Count).IsEqual(1);
        AssertBool(cards.HasCards).IsFalse();
    }

    [TestCase]
    [TestCategory("Unit")]
    public async Task SecondCardLightsTheButtonAndPressingItStartsTheSession()
    {
        var workshop = _shop.GetNode<Node3D>("World/Workshop");
        var button = workshop.GetNode<InteractableButton>("Cabinet/Assembly/Button/StaticBody3D");
        var cards = workshop.GetNode<DeckSlot>("Cabinet/Assembly/CardSlot");
        var screen = workshop.GetNode<IrregularWorldMapScreen>("Cabinet/IrregularTileMapScreen");
        AssertBool(button.Enabled).IsFalse();

        var card = ShopTestCards.Create(new CardSignature { Febris = 0.4f });
        workshop.AddChild(card);
        card.GlobalPosition = cards.GlobalPosition + cards.PositionOffset;
        await WorkshopSceneRig.Settle();
        AssertBool(button.Enabled).IsTrue();

        button.Interact();
        await WorkshopSceneRig.Settle();

        AssertBool(screen.IsInitialized).IsTrue();
        AssertBool(workshop.GetNode<DeckSlot>("Cabinet/Assembly/DeckSlot").HasCards).IsFalse();
        AssertBool(cards.HasCards).IsFalse();
    }
}
