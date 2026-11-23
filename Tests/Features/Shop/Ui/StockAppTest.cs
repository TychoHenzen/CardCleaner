using CardCleaner.Scripts.Features.Shop.Ordering;
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
    public void BuildsOneCardPerCatalogItem()
    {
        AssertThat(_f.App.CardGrid.Count).IsEqual(4);
    }

    [TestCase]
    public void LockedItemShowsRequiredLevelAndCannotBeAdded()
    {
        AssertBool(_f.App.CardGrid.IsLocked(2)).IsTrue();
        AssertThat(_f.App.CardGrid.ButtonText(2)).IsEqual("Level 4");

        _f.App.CardGrid.PressAdd(2);

        AssertThat(_f.App.CartPanel.LineCount).IsEqual(0);
        AssertThat(_f.App.CartPanel.TotalText).IsEqual("Total: 0");
    }

    [TestCase]
    public void ItemWithNoUnlockRowShowsUnavailable()
    {
        AssertBool(_f.App.CardGrid.IsLocked(3)).IsTrue();
        AssertThat(_f.App.CardGrid.ButtonText(3)).IsEqual("Unavailable");
    }

    [TestCase]
    public void AddingShowsLineAndTotal()
    {
        _f.App.CardGrid.PressAdd(0);

        AssertThat(_f.App.CartPanel.LineCount).IsEqual(1);
        AssertThat(_f.App.CartPanel.TotalText).IsEqual("Total: 40");
    }

    [TestCase]
    public void RemoveOneUpdatesTotal()
    {
        _f.App.CardGrid.PressAdd(0);
        _f.App.CardGrid.PressAdd(1);
        AssertThat(_f.App.CartPanel.TotalText).IsEqual("Total: 130");

        _f.App.CartPanel.PressRemoveOne("cheap");

        AssertThat(_f.App.CartPanel.TotalText).IsEqual("Total: 90");
    }

    [TestCase]
    public void CheckoutDisabledWhileCartEmpty()
    {
        AssertBool(_f.App.CartPanel.CheckoutDisabled).IsTrue();

        _f.App.CardGrid.PressAdd(0);

        AssertBool(_f.App.CartPanel.CheckoutDisabled).IsFalse();
    }

    [TestCase]
    public void PlaceholderTileWhenThumbnailIsNull()
    {
        AssertBool(_f.App.CardGrid.HasPlaceholder(0)).IsTrue();
    }

    [TestCase]
    public void CardsRefreshWhenProgressionChanges()
    {
        AssertBool(_f.App.CardGrid.IsLocked(2)).IsTrue();

        _f.Progression.Level = 4;
        _f.Progression.RaiseChanged();

        AssertBool(_f.App.CardGrid.IsLocked(2)).IsFalse();
        AssertThat(_f.App.CardGrid.ButtonText(2)).IsEqual("Add");
    }

    [TestCase]
    public static void TitleDefaultsToStock()
    {
        var app = new StockApp();
        try
        {
            _ = app.CardGrid;

            AssertThat(TitleLabelText(app)).IsEqual("Stock");
        }
        finally
        {
            app.Free();
        }
    }

    [TestCase]
    public static void TitleExportRelabelsTheApp()
    {
        var app = new StockApp { Title = "Furniture" };
        try
        {
            // Set before the layout exists: the label is built with the title.
            _ = app.CardGrid;
            AssertThat(TitleLabelText(app)).IsEqual("Furniture");

            // Set again after the layout exists: the label follows the setter.
            app.Title = "Furniture";
            AssertThat(TitleLabelText(app)).IsEqual("Furniture");
        }
        finally
        {
            app.Free();
        }
    }

    private static string TitleLabelText(StockApp app) => app.TitleText;
}
