using System.Threading.Tasks;
using CardCleaner.Scripts.Core.Data;
using CardCleaner.Scripts.Core.DependencyInjection;
using CardCleaner.Scripts.Core.Interfaces;
using CardCleaner.Scripts.Core.Services;
using CardCleaner.Scripts.Core.ServiceProviders;
using CardCleaner.Scripts.Features.Card.Models;
using CardCleaner.Scripts.Features.Deckbuilder.Components;
using CardCleaner.Scripts.Features.Deckbuilder.Models;
using CardCleaner.Scripts.Features.Deckbuilder.Services;
using CardCleaner.Scripts.Features.Worldgen.IrregularMesh;
using CardCleaner.Scripts.Features.Workshop.Components;
using CardCleaner.Tests.Features.Shop;
using Godot;

namespace CardCleaner.Tests.Scenes;

[TestSuite]
[RequireGodotRuntime]
public class ShopGrayboxWorkshopSceneTest
{
    private const string ScenePath = "res://Scenes/Graybox/ShopGraybox.tscn";

    private Node3D _shop = null!;
    private Node3D _workshop = null!;
    private DeckSlot _deckSlot = null!;
    private DeckSlot _cardSlot = null!;
    private InteractableButton _button = null!;
    private IrregularWorldMapScreen _screen = null!;
    private CreditCardSeeder _seeder = null!;

    [BeforeTest]
    public async Task Setup()
    {
        _shop = GD.Load<PackedScene>(ScenePath).Instantiate<Node3D>();
        _workshop = _shop.GetNode<Node3D>("World/PortalWorkshop");
        _deckSlot = _workshop.GetNode<DeckSlot>("DeckSlot");
        _cardSlot = _workshop.GetNode<DeckSlot>("CardSlot");
        _button = _workshop.GetNode<InteractableButton>("Button/StaticBody3D");
        _screen = _workshop.GetNode<IrregularWorldMapScreen>("IrregularTileMapScreen");
        _seeder = _workshop.GetNode<CreditCardSeeder>("CreditCardSeeder");

        ServiceLocator.ResetForTesting();
        ServiceLocator.Container.RegisterSingleton<IGameSettings>(_shop.GetNode<GameSettings>("Services/GameSettings"));
        ServiceLocator.Container.RegisterSingleton(new RandomNumberGenerator { Seed = 20241007 });
        _shop.GetNode<CardServiceProvider>("Services/CardServiceProvider").RegisterServices(ServiceLocator.Container);
        ServiceLocator.Container.RegisterSingleton<ICardSpawningService>(new TestCardSpawner());
        ServiceLocator.Container.RegisterSingleton<IGameSessionService>(
            _workshop.GetNode<GameSessionService>("GameSessionService"));
        ServiceLocator.Container.RegisterSingleton<ITileRegistry>(new TileRegistry());

        AddNode(_shop);
        await Settle();
    }

    [AfterTest]
    public void Teardown()
    {
        if (GodotObject.IsInstanceValid(_shop))
            _shop.Free();

        ServiceLocator.ResetForTesting();
    }

    [TestCase]
    [TestCategory("Unit")]
    public void WorkshopIsRemoteAndContainsTheGrayboxPath()
    {
        AssertBool(_workshop.GlobalPosition.X > 50f).IsTrue();
        AssertThat(_workshop.GetNode<Marker3D>("WorkshopEntry")).IsNotNull();
        AssertThat(_workshop.GetNode<StaticBody3D>("Cabinet")).IsNotNull();
        AssertThat(_workshop.GetNode<DeckSlot>("DeckSlot")).IsNotNull();
        AssertThat(_workshop.GetNode<DeckSlot>("CardSlot")).IsNotNull();
        AssertThat(_workshop.GetNode<Node3D>("Wires").GetChildCount()).IsEqual(6);
        AssertThat(_seeder.Card).IsNotNull();
        AssertBool(_deckSlot.HasCards).IsTrue();
        AssertBool(_button.Enabled).IsFalse();
        AssertBool(_screen.IsInitialized).IsFalse();
    }

    [TestCase]
    [TestCategory("Unit")]
    public async Task SecondHolderCardLightsTheButtonAndRunsTheSession()
    {
        var card = ShopTestCards.Create(new CardSignature { Febris = 0.4f });
        _workshop.AddChild(card);
        card.GlobalPosition = _cardSlot.GlobalPosition + _cardSlot.PositionOffset;
        await Settle();

        AssertBool(_cardSlot.HasCards).IsTrue();
        AssertBool(_button.Enabled).IsTrue();
        AssertBool(_button.ButtonMesh!.Visible).IsTrue();

        _button.Interact();
        await Settle();

        AssertBool(_deckSlot.HasCards).IsFalse();
        AssertBool(_cardSlot.HasCards).IsFalse();
        AssertBool(_button.Enabled).IsFalse();
        AssertBool(_screen.IsInitialized).IsTrue();
    }

    private static async Task Settle()
    {
        for (var i = 0; i < 6; i++)
            await ISceneRunner.SyncPhysicsFrame;
    }

    private sealed class TestCardSpawner : ICardSpawningService
    {
        public Node3D? SpawnCard(CardSignature signature, Transform3D spawnTransform, Node3D parent)
        {
            var card = ShopTestCards.Create(signature);
            parent.AddChild(card);
            card.GlobalTransform = spawnTransform;
            return card;
        }

        public Node3D? SpawnRandomCard(Transform3D spawnTransform, Node3D parent) =>
            SpawnCard(new CardSignature(), spawnTransform, parent);

        public Vector3 GetRandomOffset(Vector3 offsetRange) => Vector3.Zero;
    }
}
