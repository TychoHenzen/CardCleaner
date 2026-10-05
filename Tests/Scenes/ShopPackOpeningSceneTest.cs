using System.Linq;
using System.Threading.Tasks;
using CardCleaner.Scripts.Core.Data;
using CardCleaner.Scripts.Core.DependencyInjection;
using CardCleaner.Scripts.Core.Interfaces;
using CardCleaner.Scripts.Features.Card.Controllers;
using CardCleaner.Scripts.Features.Card.Services;
using CardCleaner.Scripts.Features.Packs.Components;
using CardCleaner.Scripts.Features.Packs.Models;
using CardCleaner.Scripts.Features.Packs.Services;
using CardCleaner.Scripts.Features.Player.Controllers;
using CardCleaner.Scripts.Features.Shop.Components;
using CardCleaner.Scripts.Features.Shop.Models;
using CardCleaner.Scripts.Features.Shop.Services;
using Godot;
using NSubstitute;

namespace CardCleaner.Tests.Scenes;

/// <summary>
/// End to end in the shop scene: an ordered cardboard box can be found by the player's interaction ray and
/// opened down to packs, boosters and finally real cards carrying the signatures the generator rolled.
/// </summary>
[TestSuite]
[RequireGodotRuntime]
public class ShopPackOpeningSceneTest
{
    private const ulong Seed = 4242;
    private const float StandingDistance = 2.0f;
    private const int OpenFrames = 12;

    private Node3D _shop = null!;
    private Node3D _world = null!;
    private PlayerController _player = null!;
    private MoneyService _money = null!;
    private OrderingService _ordering = null!;

    [BeforeTest]
    public async Task Setup()
    {
        _shop = GD.Load<PackedScene>(ShopSceneProbe.ScenePath).Instantiate<Node3D>();
        _world = _shop.GetNode<Node3D>("World");
        _player = _shop.GetNode<PlayerController>("Player");
        _money = _shop.GetNode<MoneyService>("Services/MoneyService");
        _ordering = _shop.GetNode<OrderingService>("Services/OrderingService");

        // The test scene is not the current scene, so hand the scene-owned services over directly.
        ServiceLocator.ResetForTesting();
        ServiceLocator.Container.RegisterSingleton<IGameSettings>(_shop.GetNode<GameSettings>("Services/GameSettings"));
        ServiceLocator.Container.RegisterSingleton<IMoneyService>(_money);
        ServiceLocator.Container.RegisterSingleton<IOrderingService>(_ordering);
        ServiceLocator.Container.RegisterSingleton(new RandomNumberGenerator { Seed = Seed });
        ServiceLocator.Container.RegisterSingleton(Substitute.For<ICardGenerator>());
        ServiceLocator.Container.RegisterSingleton<ICardSpawningService>(
            _shop.GetNode<CardSpawningService>("Services/SpawningService"));

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
    public async Task OrderedBoxIsACardContainerTheInteractionRayCanReach()
    {
        var box = OrderBox();
        await ISceneRunner.SyncPhysicsFrame;
        await ISceneRunner.SyncPhysicsFrame;
        var interaction = _player.GetNode<InteractionSystem>("Head/Camera3D/InteractionSystem");
        var from = box.GlobalPosition + new Vector3(0f, 1.0f, StandingDistance);
        var to = box.GlobalPosition + new Vector3(0f, 0.2f, 0f);

        var hit = _shop.GetWorld3D().DirectSpaceState.IntersectRay(new PhysicsRayQueryParameters3D
        {
            From = from, To = to, CollideWithBodies = true, CollisionMask = interaction.InteractableCollisionMask
        });

        AssertThat(hit.Count).IsGreater(0);
        AssertThat(hit["collider"].Obj).IsEqual(box);
        AssertBool(from.DistanceTo(box.GlobalPosition) <= box.InteractionRange).IsTrue();
        AssertBool(box.CanInteract).IsTrue();
    }

    [TestCase]
    [TestCategory("Unit")]
    public async Task OpeningAnOrderedBoxLeadsThroughPacksAndBoostersToRealCardsWithGeneratedSignatures()
    {
        var box = OrderBox();
        box.Interact();
        await Frames(OpenFrames);

        var packs = Containers(CardContainerKind.Pack);
        AssertThat(packs.Length).IsEqual(CardContainerLayout.ItemsPerContainer);

        packs[0].Interact();
        await Frames(OpenFrames);
        var boosters = Containers(CardContainerKind.Booster);
        AssertThat(boosters.Length).IsEqual(CardContainerLayout.ItemsPerContainer);

        boosters[0].Interact();
        await Frames(OpenFrames);
        var cards = _world.GetChildren().OfType<CardController>().ToArray();

        var expected = new CardPackGenerator(new RandomNumberGenerator { Seed = Seed }).OpenBooster();
        AssertThat(cards.Length).IsEqual(CardContainerLayout.ItemsPerContainer);
        for (var i = 0; i < cards.Length; i++)
            AssertThat(cards[i].Signature.Elements).IsEqual(expected[i].Elements);
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
        _world.GetChildren().OfType<CardContainer>().Where(c => c.Kind == kind && !c.IsOpened).ToArray();

    private static async Task Frames(int count)
    {
        for (var i = 0; i < count; i++)
            await ISceneRunner.SyncProcessFrame;
    }
}
