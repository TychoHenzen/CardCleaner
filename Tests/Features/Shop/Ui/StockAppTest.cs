using CardCleaner.Tests.TestUtilities;
using Godot;

namespace CardCleaner.Tests.Features.Shop.Ui;

/// <summary>
/// The Stock app's card grid and cart: what each card shows, and how adding and removing move the total.
/// </summary>
[TestSuite]
[RequireGodotRuntime]
public class StockAppTest
{
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
    public void BuildsOneCardPerCatalogItem()
    {
        AssertThat(_f.App.CardGrid.Count).IsEqual(4);
    }

    [TestCase]
    [TestCategory("Unit")]
    public void LockedItemShowsRequiredLevelAndCannotBeAdded()
    {
        AssertBool(_f.App.CardGrid.IsLocked(2)).IsTrue();
        AssertThat(_f.App.CardGrid.ButtonText(2)).IsEqual("Level 4");

        _f.App.CardGrid.PressAdd(2);

        AssertThat(_f.App.CartPanel.LineCount).IsEqual(0);
        AssertThat(_f.App.CartPanel.TotalText).IsEqual("Total: 0");
    }

    [TestCase]
    [TestCategory("Unit")]
    public void ItemWithNoUnlockRowShowsUnavailable()
    {
        AssertBool(_f.App.CardGrid.IsLocked(3)).IsTrue();
        AssertThat(_f.App.CardGrid.ButtonText(3)).IsEqual("Unavailable");
    }

    [TestCase]
    [TestCategory("Unit")]
    public void AddingShowsLineAndTotal()
    {
        _f.App.CardGrid.PressAdd(0);

        AssertThat(_f.App.CartPanel.LineCount).IsEqual(1);
        AssertThat(_f.App.CartPanel.TotalText).IsEqual("Total: 40");
    }

    [TestCase]
    [TestCategory("Unit")]
    public void RemoveOneUpdatesTotal()
    {
        _f.App.CardGrid.PressAdd(0);
        _f.App.CardGrid.PressAdd(1);
        AssertThat(_f.App.CartPanel.TotalText).IsEqual("Total: 130");

        _f.App.CartPanel.PressRemoveOne("cheap");

        AssertThat(_f.App.CartPanel.TotalText).IsEqual("Total: 90");
    }

    [TestCase]
    [TestCategory("Unit")]
    public void CheckoutDisabledWhileCartEmpty()
    {
        AssertBool(_f.App.CartPanel.CheckoutDisabled).IsTrue();

        _f.App.CardGrid.PressAdd(0);

        AssertBool(_f.App.CartPanel.CheckoutDisabled).IsFalse();
    }

    [TestCase]
    [TestCategory("Unit")]
    public void PlaceholderTileWhenThumbnailIsNull()
    {
        AssertBool(_f.App.CardGrid.HasPlaceholder(0)).IsTrue();
    }

    [TestCase]
    [TestCategory("Unit")]
    public void CardsRefreshWhenProgressionChanges()
    {
        AssertBool(_f.App.CardGrid.IsLocked(2)).IsTrue();

        _f.Progression.Level = 4;
        _f.Progression.RaiseChanged();

        AssertBool(_f.App.CardGrid.IsLocked(2)).IsFalse();
        AssertThat(_f.App.CardGrid.ButtonText(2)).IsEqual("Add");
    }
}
