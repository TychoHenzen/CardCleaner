using System.Linq;
using System.Threading.Tasks;
using CardCleaner.Scripts.Core.DependencyInjection;
using CardCleaner.Scripts.Core.Interfaces;
using CardCleaner.Scripts.Features.Shop.Components;
using CardCleaner.Scripts.Features.Shop.Services;
using CardCleaner.Scripts.Features.Shop.Ui;
using CardCleaner.Tests.TestUtilities;
using Godot;

namespace CardCleaner.Tests.Scenes;

/// <summary>
///     The shop's progression in the real shop scene: the progression node is the one the scene's services hand
///     out, a fresh shop starts at level one, and the backoffice PC refuses the level-locked item until a checkout
///     has earned the level it needs.
/// </summary>
[TestSuite]
[RequireGodotRuntime]
public class ShopProgressionSceneTest
{
    private const string LockedId = "bulk_packing_station";
    private const int LevelTwoXp = 500; // a 500 checkout earns 50 XP, which is level 2

    private Node3D _shop = null!;
    private ScreenTerminal _terminal = null!;
    private StockApp _stock = null!;
    private MoneyService _money = null!;
    private ShopProgression _progression = null!;
    private OrderingService _ordering = null!;
    private StockPresses _presses = null!;

    [BeforeTest]
    public async Task Setup()
    {
        _shop = ShopOrderingRig.LoadShopWithServices();
        _terminal = _shop.GetNode<ScreenTerminal>("World/Markers/PcLocation/PcTerminal");
        _stock = _shop.GetNode<StockApp>("PcDesktopViewport/StockApp");
        _money = _shop.GetNode<MoneyService>("Services/MoneyService");
        _progression = _shop.GetNode<ShopProgression>("Services/ShopProgression");
        _ordering = _shop.GetNode<OrderingService>("Services/OrderingService");
        _presses = new StockPresses(_stock, _ordering, _shop.GetNode("World"),
            OrderingService.SlotColumns * OrderingService.SlotRows);

        AddNode(_shop);
        await ISceneRunner.SyncPhysicsFrame;
        await ISceneRunner.SyncPhysicsFrame;
    }

    [AfterTest]
    public static void Teardown()
    {
        TestServices.Reset();
    }

    [TestCase]
    [TestCategory("Unit")]
    public void SceneProvidersResolveTheProgressionNode()
    {
        var tree = (SceneTree)Engine.GetMainLoop();
        var previous = tree.CurrentScene;
        tree.CurrentScene = _shop;
        try
        {
            var container = new ServiceContainer();
            _shop.GetNode<AutoServiceProvider>("Services").RegisterServices(container);

            AssertBool(ReferenceEquals(container.Resolve<IShopProgression>(), _progression)).IsTrue();
        }
        finally
        {
            tree.CurrentScene = previous;
        }
    }

    [TestCase]
    [TestCategory("Unit")]
    public void ShopStartsAtLevelOneWithNoXp()
    {
        AssertThat(_progression.Xp).IsEqual(0);
        AssertThat(_progression.Level).IsEqual(1);
    }

    [TestCase]
    [TestCategory("Unit")]
    public void LevelLockedBackofficeItemIsRefusedWithALockMessage()
    {
        var index = LockedIndex();
        var balance = _money.Balance;
        _terminal.Interact();

        // The Stock app shows a level-locked item as a disabled card naming the level, so it never reaches the cart.
        AssertBool(_stock.CardIsLocked(index)).IsTrue();
        AssertThat(_stock.CardLockText(index)).IsEqual("Level 2");
        _presses.PressForRefusal(index, "cart is empty");
        AssertThat(_money.Balance).IsEqual(balance);
    }

    [TestCase]
    [TestCategory("Unit")]
    public void ReachingLevelTwoDeliversThatItem()
    {
        var index = LockedIndex();
        var price = _stock.Catalog!.Items[index].Price;
        _progression.AwardCheckout(LevelTwoXp);
        _money.Add(price);
        var balance = _money.Balance;
        _terminal.Interact();

        _presses.PressForDelivery(index);

        AssertThat(_money.Balance).IsEqual(balance - price);
    }

    [TestCase]
    [TestCategory("Unit")]
    public void EveryOtherBackofficeItemOrdersAtLevelOne()
    {
        var items = _stock.Catalog!.Items;
        _terminal.Interact();

        for (var i = 0; i < items.Length; i++)
        {
            if (items[i].Id == LockedId)
                continue;

            _money.Add(items[i].Price);
            _presses.PressForDelivery(i);
        }

        AssertThat(_ordering.DeliveredCount).IsEqual(items.Length - 1);
    }

    private int LockedIndex()
    {
        var index = _stock.Catalog!.Items.ToList().FindIndex(item => item.Id == LockedId);
        AssertBool(index >= 0).OverrideFailureMessage($"catalog has no {LockedId} row").IsTrue();
        return index;
    }
}
