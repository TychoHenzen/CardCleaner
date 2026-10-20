using System.Threading.Tasks;
using CardCleaner.Scripts.Core.DependencyInjection;
using CardCleaner.Scripts.Core.Interfaces;
using CardCleaner.Scripts.Core.Services;
using CardCleaner.Scripts.Features.Card.Models;
using CardCleaner.Scripts.Features.Card.Components;
using CardCleaner.Scripts.Features.Card.Services;
using CardCleaner.Scripts.Features.Deckbuilder.Components;
using CardCleaner.Scripts.Features.Deckbuilder.Controllers;
using CardCleaner.Scripts.Features.Deckbuilder.Services;
using CardCleaner.Scripts.Features.Deckbuilder.Tiles;
using CardCleaner.Scripts.Features.Worldgen.IrregularMesh;
using CardCleaner.Tests.Features.Shop;
using CardCleaner.Tests.Features.Worldgen.IrregularMesh;
using CardCleaner.Tests.TestUtilities;
using CardCleaner.Scripts.Core.Enumeration;
using Godot;
using IServiceProvider = CardCleaner.Scripts.Core.Interfaces.IServiceProvider;

namespace CardCleaner.Tests.Scenes;

/// <summary>
/// The workshop cabinet against the registration the shop scene really produces: its own service providers
/// register into the container, the real spawning service seats the credit card, the kind toggle picks the irregular
/// map, and the button runs a session on it.
/// </summary>
[TestSuite]
[RequireGodotRuntime]
public class ShopWorkshopRealServicesSceneTest
{
    // Frames to wait for the explorer's first step once the map is up; a step takes a fraction of a second.
    private const int FirstStepFrameBudget = 300;

    private SceneTree _tree = null!;
    private Node? _previousScene;
    private Node3D _shop = null!;

    [BeforeTest]
    public async Task Setup()
    {
        _tree = (SceneTree)Engine.GetMainLoop();
        _previousScene = _tree.CurrentScene;

        TestServices.Reset();
        // The composition root's own scan registers the card generator, first, as Compose does (#273).
        TestServices.RegisterGameServiceClasses(TestServices.Container);
        TestServices.Container.RegisterSingleton(new RandomNumberGenerator());
        TestServices.Container.RegisterSingleton<ITileRegistry>(new TileRegistry(new GodotAssetPaths(), new CompiledAtlasSource()));
        TestServices.Container.RegisterSingleton<IMeshMapGenerator>(
            MeshMapGeneratorFixture.Create(WorkshopSceneRig.SmallMeshRings));

        _shop = GD.Load<PackedScene>(ShopSceneProbe.ScenePath).Instantiate<Node3D>();
        _tree.Root.AddChild(_shop);
        _tree.CurrentScene = _shop;

        foreach (var node in _tree.GetNodesInGroup("service_providers"))
        {
            if (node is IServiceProvider provider)
                provider.RegisterServices(TestServices.Container);
        }

        TestServices.ExecutePendingCallbacks();
        await WorkshopSceneRig.Settle();
    }

    [AfterTest]
    public void Teardown()
    {
        TestServices.Reset();
        _tree.CurrentScene = _previousScene;
        _tree.Root.RemoveChild(_shop);
        _shop.Free();
    }

    [TestCase]
    [TestCategory("Unit")]
    public void RegistrationResolvesTheWorkshopSessionServiceAndTheShopSpawner()
    {
        AssertBool(ReferenceEquals(TestServices.Resolver.Get<IGameSessionService>(),
            _shop.GetNode("World/Workshop/GameSessionService"))).IsTrue();
        AssertBool(ReferenceEquals(TestServices.Resolver.Get<ICardSpawningService>(),
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
    public async Task TogglePicksTheIrregularMapAndPressingTheButtonExploresIt()
    {
        var workshop = _shop.GetNode<Node3D>("World/Workshop");
        var button = workshop.GetNode<InteractableButton>("Cabinet/Assembly/Button/StaticBody3D");
        var toggle = workshop.GetNode<InteractableButton>("Cabinet/MapKindButton/StaticBody3D");
        var controller = workshop.GetNode<DeckBuilderController>("DeckbuilderController");
        var cards = workshop.GetNode<DeckSlot>("Cabinet/Assembly/CardSlot");
        var screen = workshop.GetNode<IrregularWorldMapScreen>("Cabinet/IrregularTileMapScreen");
        AssertBool(button.Enabled).IsFalse();
        AssertThat(controller.MapType).IsEqual(MapGenerationType.RegularGrid);

        toggle.Interact();
        AssertThat(controller.MapType).IsEqual(MapGenerationType.IrregularMesh);
        var steps = 0;
        screen.PlayerMoved += (_, _) => steps++;

        var card = ShopTestCards.Create(new CardSignature().With(Element.Febris, 0.4f));
        workshop.AddChild(card);
        card.GlobalPosition = cards.GlobalPosition + cards.PositionOffset;
        await WorkshopSceneRig.Settle();
        AssertBool(button.Enabled).IsTrue();

        button.Interact();
        AssertBool(toggle.Enabled).IsFalse();
        for (var frame = 0; frame < FirstStepFrameBudget && steps == 0; frame++)
            await ISceneRunner.SyncProcessFrame;

        AssertBool(screen.Visible).IsTrue();
        AssertBool(screen.IsInitialized).IsTrue();
        AssertThat(steps).OverrideFailureMessage("The explorer never stepped on the irregular map").IsGreater(0);
        AssertBool(toggle.Enabled).IsFalse();
        AssertBool(workshop.GetNode<DeckSlot>("Cabinet/Assembly/DeckSlot").HasCards).IsFalse();
        AssertBool(cards.HasCards).IsFalse();
    }
}
