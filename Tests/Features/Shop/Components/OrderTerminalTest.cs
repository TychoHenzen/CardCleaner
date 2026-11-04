using CardCleaner.Scripts.Features.Shop.Components;
using CardCleaner.Scripts.Features.Shop.Services;
using CardCleaner.Scripts.Features.Shop.Ui;
using CardCleaner.Tests.TestUtilities;

namespace CardCleaner.Tests.Features.Shop.Components;

/// <summary>Ordering through the terminal: charging, delivery, refusals and tree changes.</summary>
[TestSuite]
[RequireGodotRuntime]
public class OrderTerminalTest
{
    private const int StartingBalance = OrderTerminalFixture.StartingBalance;
    private const int CheapPrice = OrderTerminalFixture.CheapPrice;

    private OrderTerminalFixture _f = null!;

    [BeforeTest]
    public void Setup()
    {
        _f = new OrderTerminalFixture();
        AddNode(_f.World);
    }

    [AfterTest]
    public static void Teardown()
    {
        TestServices.Reset();
    }

    [TestCase]
    [TestCategory("Unit")]
    public void UiRowsAreBuiltFromTheCatalogExport()
    {
        AssertThat(_f.Ui.ItemRowCount).IsEqual(3);

        _f.Ui.Populate(OrderTerminalFixture.MakeCatalog(("a", 1), ("b", 2), ("c", 3)));

        AssertThat(_f.Ui.ItemRowCount).IsEqual(3);
    }

    [TestCase]
    [TestCategory("Unit")]
    public void SecondTerminalWithItsOwnCatalogOrdersItsOwnItemsWithoutCodeChanges()
    {
        var workshopUi = new OrderTerminalUi();
        var workshop = new OrderTerminal { Catalog = OrderTerminalFixture.MakeCatalog(("tool", 30)), Ui = workshopUi };
        _f.World.AddChild(workshopUi);
        _f.World.AddChild(workshop);

        workshop.Interact();
        workshopUi.PressItem(0);

        AssertThat(workshopUi.ItemRowCount).IsEqual(1);
        AssertThat(_f.Ui.ItemRowCount).IsEqual(3);
        AssertThat(_f.Money.Balance).IsEqual(StartingBalance - 30);
        AssertThat(_f.Ordering.DeliveredCount).IsEqual(1);
    }

    [TestCase]
    [TestCategory("Unit")]
    public void PressingAnAffordableItemChargesSpawnsAndUpdatesTheBalanceDisplay()
    {
        _f.Terminal.Interact();

        _f.Ui.PressItem(0);

        AssertThat(_f.Money.Balance).IsEqual(StartingBalance - CheapPrice);
        AssertThat(_f.Ui.BalanceText).IsEqual($"Balance: {StartingBalance - CheapPrice}");
        AssertThat(_f.Ordering.DeliveredCount).IsEqual(1);
        AssertBool(_f.Ui.MessageText.Contains("Ordered")).IsTrue();
    }

    [TestCase]
    [TestCategory("Unit")]
    public void PressingAnUnaffordableItemShowsAMessageAndChangesNothing()
    {
        _f.Terminal.Interact();

        _f.Ui.PressItem(1);

        AssertThat(_f.Money.Balance).IsEqual(StartingBalance);
        AssertThat(_f.Ordering.DeliveredCount).IsEqual(0);
        AssertBool(_f.Ui.MessageText.Contains("Not enough money")).IsTrue();
    }

    [TestCase]
    [TestCategory("Unit")]
    public void PressingALockedItemShowsTheLockMessageAndChangesNothing()
    {
        _f.Terminal.Interact();

        _f.Ui.PressItem(2);

        AssertThat(_f.Terminal.LastOrder?.Status).IsEqual(OrderStatus.Locked);
        AssertThat(_f.Money.Balance).IsEqual(StartingBalance);
        AssertThat(_f.Ordering.DeliveredCount).IsEqual(0);
        AssertThat(_f.Ui.MessageText).IsEqual("locked is locked. Reach a higher shop level to order it.");
        AssertBool(_f.Ui.MessageText.Contains("cannot be ordered right now")).IsFalse();
    }

    [TestCase]
    [TestCategory("Unit")]
    public void RapidDoubleOrderChargesAndSpawnsExactlyTwice()
    {
        _f.Money.Add(500);
        _f.Terminal.Interact();

        _f.Ui.PressItem(0);
        _f.Ui.PressItem(0);

        AssertThat(_f.Money.Balance).IsEqual(StartingBalance + 500 - 2 * CheapPrice);
        AssertThat(_f.Ordering.DeliveredCount).IsEqual(2);
    }

    [TestCase]
    [TestCategory("Unit")]
    public void TerminalThatLeavesAndReentersTheTreeStillOrdersExactlyOncePerClick()
    {
        _f.World.RemoveChild(_f.Terminal);
        _f.World.AddChild(_f.Terminal);
        _f.Money.Add(500);
        _f.Terminal.Interact();

        _f.Ui.PressItem(0);

        AssertThat(_f.Money.Balance).IsEqual(StartingBalance + 500 - CheapPrice);
        AssertThat(_f.Ordering.DeliveredCount).IsEqual(1);
    }

    [TestCase]
    [TestCategory("Unit")]
    public void RemovedTerminalNoLongerReactsToTheUi()
    {
        _f.World.RemoveChild(_f.Terminal);

        _f.Ui.PressItem(0);

        AssertThat(_f.Ordering.DeliveredCount).IsEqual(0);
        AssertThat(_f.Money.Balance).IsEqual(StartingBalance);
        _f.Terminal.Free();
    }
}
