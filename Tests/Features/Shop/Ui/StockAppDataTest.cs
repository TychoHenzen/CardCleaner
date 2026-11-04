using CardCleaner.Scripts.Features.Shop.Models;
using CardCleaner.Scripts.Features.Shop.Ui;
using CardCleaner.Tests.Features.Shop.Components;
using CardCleaner.Tests.TestUtilities;

namespace CardCleaner.Tests.Features.Shop.Ui;

/// <summary>
/// The Stock app with bad data and after a partial failure: an empty or missing catalog, items that cannot be
/// ordered, missing services, and a checkout retried once the delivery area has room again.
/// </summary>
[TestSuite]
[RequireGodotRuntime]
public class StockAppDataTest
{
    private const string NothingToOrder = "Nothing to order.";

    private StockAppFixture _f = null!;

    [BeforeTest]
    public void Setup()
    {
        _f = new StockAppFixture();
        AddNode(_f.World);
    }

    [AfterTest]
    public static void Teardown()
    {
        TestServices.Reset();
    }

    [TestCase]
    [TestCategory("Unit")]
    public void EmptyCatalogShowsMessageWithoutThrowing()
    {
        var app = AddApp(new OrderCatalog { Items = [] });

        AssertThat(app.CardGrid.Count).IsEqual(0);
        AssertThat(app.CardGrid.EmptyText).IsEqual(NothingToOrder);
    }

    [TestCase]
    [TestCategory("Unit")]
    public void NullCatalogShowsMessageWithoutThrowing()
    {
        var app = AddApp(null);

        AssertThat(app.CardGrid.Count).IsEqual(0);
        AssertThat(app.CardGrid.EmptyText).IsEqual(NothingToOrder);
    }

    [TestCase]
    [TestCategory("Unit")]
    public void ItemWithoutSceneIsShownDisabled()
    {
        var catalog = OrderTerminalFixture.MakeCatalog(("cheap", 40), ("dear", 90));
        catalog.Items[0].Scene = null;
        var app = AddApp(catalog);

        AssertBool(app.CardGrid.IsLocked(0)).IsTrue();
        AssertThat(app.CardGrid.ButtonText(0)).IsEqual("Unavailable");
        AssertBool(app.CardGrid.IsLocked(1)).IsFalse();
        AssertThat(app.CardGrid.EmptyText).IsEqual(string.Empty);
    }

    [TestCase]
    [TestCategory("Unit")]
    public void ItemWithZeroPriceIsShownDisabled()
    {
        var catalog = OrderTerminalFixture.MakeCatalog(("cheap", 0), ("dear", 90));
        var app = AddApp(catalog);

        AssertBool(app.CardGrid.IsLocked(0)).IsTrue();
        AssertThat(app.CardGrid.ButtonText(0)).IsEqual("Unavailable");
        app.CardGrid.PressAdd(0);
        AssertThat(app.CartPanel.LineCount).IsEqual(0);
    }

    [TestCase]
    [TestCategory("Unit")]
    public void CheckoutWithoutServicesIsRefused()
    {
        // Not injected and not in the tree: no service ever resolves, as before the scene has wired them.
        var app = new StockApp { Catalog = OrderTerminalFixture.MakeCatalog(("cheap", 40)) };
        try
        {
            app.Populate(app.Catalog);

            app.PressCheckout();

            AssertThat(app.CartPanel.MessageText).IsEqual("Ordering is unavailable right now.");
            AssertThat(_f.Ordering.Calls.Count).IsEqual(0);
            AssertThat(_f.Money.Balance).IsEqual(StockAppFixture.StartingBalance);
        }
        finally
        {
            app.Free();
        }
    }

    [TestCase]
    [TestCategory("Unit")]
    public void CheckoutAfterPartialFailureRetriesRemainingUnits()
    {
        _f.Ordering.SucceedLimit = 1;
        _f.App.CardGrid.PressAdd(0);
        _f.App.CardGrid.PressAdd(0);

        _f.App.PressCheckout();

        AssertThat(_f.App.CartPanel.LineCount).IsEqual(1);
        AssertBool(_f.App.CartPanel.CheckoutDisabled).IsFalse();
        AssertThat(_f.Money.Balance).IsEqual(StockAppFixture.StartingBalance - 40);

        _f.Ordering.SucceedLimit = int.MaxValue;
        _f.App.PressCheckout();

        AssertThat(_f.App.CartPanel.LineCount).IsEqual(0);
        AssertBool(_f.App.CartPanel.CheckoutDisabled).IsTrue();
        AssertThat(_f.Money.Balance).IsEqual(StockAppFixture.StartingBalance - 80);
        AssertThat(_f.Ordering.Calls.Count).IsEqual(3);
        AssertThat(_f.App.CartPanel.MessageText).IsEqual("Ordered. Items are at the delivery point.");
    }

    private StockApp AddApp(OrderCatalog? catalog)
    {
        var app = new StockApp { Catalog = catalog };
        TestServices.Inject(app);
        _f.World.AddChild(app);
        return app;
    }
}
