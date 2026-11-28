using System.Linq;
using System.Threading.Tasks;
using CardCleaner.Scripts.Features.Card.Controllers;
using CardCleaner.Scripts.Features.Hand.Components;
using CardCleaner.Scripts.Features.Packs.Components;
using CardCleaner.Scripts.Features.Packs.Models;
using CardCleaner.Scripts.Features.Packs.Services;
using CardCleaner.Scripts.Features.Player.Controllers;
using CardCleaner.Scripts.Features.Shop.Ui;
using CardCleaner.Scripts.Features.Shop.Models;
using CardCleaner.Scripts.Features.Shop.Services;
using CardCleaner.Scripts.Core.Randomness;
using CardCleaner.Tests.TestUtilities.Helpers;
using Godot;
using CardCleaner.Tests.TestUtilities;
using static CardCleaner.Tests.Features.Packs.Components.ContainerOpeningWait;

namespace CardCleaner.Tests.Scenes;

/// <summary>
/// End to end in the shop scene: an ordered cardboard box can be found by the player's interaction ray, and an
/// ordered booster box empties into the player's hand as booster packs, the top one of which opens into real cards
/// carrying the signatures the generator rolled. A cardboard box is storage and never opens whole.
/// </summary>
[TestSuite]
[RequireGodotRuntime]
public class ShopPackOpeningSceneTest
{
    private const ulong Seed = 4242;
    private const float StandingDistance = 2.0f;

    private Node3D _shop = null!;
    private PlayerController _player = null!;
    private MoneyService _money = null!;
    private OrderingService _ordering = null!;

    private FastClock? _clock;

    [BeforeTest]
    public async Task Setup()
    {
        _clock = FastClock.Start();
        _shop = GD.Load<PackedScene>(ShopSceneProbe.ScenePath).Instantiate<Node3D>();
        _player = _shop.GetNode<PlayerController>("Player");
        _money = _shop.GetNode<MoneyService>("Services/MoneyService");
        _ordering = _shop.GetNode<OrderingService>("Services/OrderingService");

        // The test scene is not the current scene, so hand the scene-owned services over directly.
        ShopSceneServices.Register(_shop, Seed);

        AddNode(_shop);
        await ISceneRunner.SyncPhysicsFrame;
        await ISceneRunner.SyncPhysicsFrame;
    }

    [AfterTest]
    public void Teardown()
    {
        _clock?.Dispose();
        TestServices.Reset();
    }

    [TestCase]
    public async Task OrderedBoxIsACardContainerTheInteractionRayCanReach()
    {
        var box = Order("cardboard_box");
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

    // The player's hand empties an ordered booster box into its eight booster packs, and the front one opens into eight
    // real cards when the front one is opened. The booster box was ordered unseeded, so its cards come from the shared random source.
    [TestCase]
    public async Task AnOrderedBoosterBoxOpensInTheHandToRealCardsWithGeneratedSignatures()
    {
        var hand = _player.GetNode<IHand>("CardInteraction/Hand");
        var pack = Order("pack");

        AssertBool(hand.TryTake(pack)).IsTrue();
        AssertBool(hand.UseFront()).IsTrue();
        AssertThat(hand.Count).IsEqual(CardContainerLayout.ItemsPerContainer);
        AssertBool(hand.UseFront()).IsTrue();
        await Until(() => !hand.IsOpening, "the booster opening in the hand");

        var cards = hand.Cards.OfType<CardController>().ToArray();
        var expected = new CardPackGenerator(RandomSources.Create(Seed)).OpenBooster();
        AssertThat(cards.Length).IsEqual(CardContainerLayout.ItemsPerContainer);
        for (var i = 0; i < cards.Length; i++)
            AssertThat(cards[i].Signature.Elements).IsEqual(expected[i].Elements);
    }

    private CardContainer Order(string id)
    {
        var item = Catalog().Items.Single(i => i.Id == id);
        _money.Add(item.Price);
        var result = _ordering.Order(item);
        AssertBool(result.Succeeded).IsTrue();
        return (CardContainer)result.Spawned!;
    }

    private OrderCatalog Catalog() =>
        _shop.GetNode<StockApp>("PcDesktopViewport/StockApp").Catalog!;
}
