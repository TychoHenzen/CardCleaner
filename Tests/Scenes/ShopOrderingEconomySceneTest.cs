using System.Linq;
using System.Threading.Tasks;
using CardCleaner.Scripts.Core.DependencyInjection;
using CardCleaner.Scripts.Core.Interfaces;
using CardCleaner.Scripts.Features.Shop.Components;
using CardCleaner.Scripts.Features.Shop.Services;
using CardCleaner.Scripts.Features.Shop.Ui;
using Godot;

namespace CardCleaner.Tests.Scenes;

/// <summary>
/// The shop PC and the workshop terminal reuse one ordering framework: two catalogs that never share an item,
/// one money service resolved through the service locator, and one ordering service.
/// </summary>
[TestSuite]
[RequireGodotRuntime]
public class ShopOrderingEconomySceneTest
{
    private Node3D _shop = null!;
    private OrderTerminal _pc = null!;
    private OrderTerminal _workshop = null!;
    private OrderTerminalUi _pcUi = null!;
    private OrderTerminalUi _workshopUi = null!;
    private MoneyService _money = null!;

    [BeforeTest]
    public async Task Setup()
    {
        _shop = ShopOrderingRig.LoadShopWithServices();
        _pc = _shop.GetNode<OrderTerminal>("World/Markers/PcLocation/PcTerminal");
        _workshop = _shop.GetNode<OrderTerminal>("World/Workshop/OrderingRoom/OrderTerminal");
        _pcUi = _shop.GetNode<OrderTerminalUi>("OrderUi");
        _workshopUi = _shop.GetNode<OrderTerminalUi>("World/Workshop/OrderUi");
        _money = _shop.GetNode<MoneyService>("Services/MoneyService");

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
    public void ShopPcAndWorkshopTerminalCatalogsShareNoItem()
    {
        var pcIds = _pc.Catalog!.Items.Select(i => i.Id).ToArray();
        var workshopIds = _workshop.Catalog!.Items.Select(i => i.Id).ToArray();

        AssertThat(pcIds.Length).IsGreater(0);
        AssertThat(workshopIds.Length).IsGreater(0);
        AssertThat(pcIds.Intersect(workshopIds).Count()).IsEqual(0);
        AssertThat(_pc.Catalog.Items.Select(i => i.Scene!.ResourcePath)
            .Intersect(_workshop.Catalog.Items.Select(i => i.Scene!.ResourcePath)).Count()).IsEqual(0);
        AssertBool(ReferenceEquals(_pc.Catalog, _workshop.Catalog)).IsFalse();
    }

    [TestCase]
    [TestCategory("Unit")]
    public void EachUiListsOnlyItsOwnTerminalsCatalog()
    {
        AssertThat(_pcUi.ItemRowCount).IsEqual(_pc.Catalog!.Items.Length);
        AssertThat(_workshopUi.ItemRowCount).IsEqual(_workshop.Catalog!.Items.Length);
    }

    [TestCase]
    [TestCategory("Unit")]
    public void BothTerminalsUseTheOneMoneyServiceTheLocatorHandsOut()
    {
        var resolved = ServiceLocator.Container.Resolve<IMoneyService>();

        AssertBool(ReferenceEquals(resolved, _money)).IsTrue();
        AssertThat(_shop.FindChildren("*", string.Empty, true, false).OfType<MoneyService>().Count()).IsEqual(1);
    }

    [TestCase]
    [TestCategory("Unit")]
    public void OrderingAtTheWorkshopTerminalUpdatesTheShopPcBalanceToo()
    {
        var item = _workshop.Catalog!.Items[0];
        var expected = _money.Balance - item.Price;
        _workshop.Interact();

        _workshopUi.PressItem(0);

        AssertThat(_money.Balance).IsEqual(expected);
        AssertThat(_workshopUi.BalanceText).IsEqual($"Balance: {expected}");
        AssertThat(_pcUi.BalanceText).IsEqual($"Balance: {expected}");
    }

    [TestCase]
    [TestCategory("Unit")]
    public void OrderingAtTheShopPcUpdatesTheWorkshopBalanceToo()
    {
        var item = _pc.Catalog!.Items[0];
        var expected = _money.Balance - item.Price;
        _pc.Interact();

        _pcUi.PressItem(0);

        AssertThat(_money.Balance).IsEqual(expected);
        AssertThat(_pcUi.BalanceText).IsEqual($"Balance: {expected}");
        AssertThat(_workshopUi.BalanceText).IsEqual($"Balance: {expected}");
    }
}
