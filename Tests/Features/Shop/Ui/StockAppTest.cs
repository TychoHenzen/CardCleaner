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
        AssertThat(_f.App.CardCount).IsEqual(4);
    }

    [TestCase]
    [TestCategory("Unit")]
    public void LockedItemShowsRequiredLevelAndCannotBeAdded()
    {
        AssertBool(_f.App.CardIsLocked(2)).IsTrue();
        AssertThat(_f.App.CardLockText(2)).IsEqual("Level 4");

        _f.App.PressAdd(2);

        AssertThat(_f.App.CartLineCount).IsEqual(0);
        AssertThat(_f.App.CartTotalText).IsEqual("Total: 0");
    }

    [TestCase]
    [TestCategory("Unit")]
    public void ItemWithNoUnlockRowShowsUnavailable()
    {
        AssertBool(_f.App.CardIsLocked(3)).IsTrue();
        AssertThat(_f.App.CardLockText(3)).IsEqual("Unavailable");
    }

    [TestCase]
    [TestCategory("Unit")]
    public void AddingShowsLineAndTotal()
    {
        _f.App.PressAdd(0);

        AssertThat(_f.App.CartLineCount).IsEqual(1);
        AssertThat(_f.App.CartTotalText).IsEqual("Total: 40");
    }

    [TestCase]
    [TestCategory("Unit")]
    public void RemoveOneUpdatesTotal()
    {
        _f.App.PressAdd(0);
        _f.App.PressAdd(1);
        AssertThat(_f.App.CartTotalText).IsEqual("Total: 130");

        _f.App.PressRemoveOne("cheap");

        AssertThat(_f.App.CartTotalText).IsEqual("Total: 90");
    }

    [TestCase]
    [TestCategory("Unit")]
    public void CheckoutDisabledWhileCartEmpty()
    {
        AssertBool(_f.App.CheckoutDisabled).IsTrue();

        _f.App.PressAdd(0);

        AssertBool(_f.App.CheckoutDisabled).IsFalse();
    }

    [TestCase]
    [TestCategory("Unit")]
    public void PlaceholderTileWhenThumbnailIsNull()
    {
        AssertBool(_f.App.CardHasPlaceholder(0)).IsTrue();
    }

    [TestCase]
    [TestCategory("Unit")]
    public void CardsRefreshWhenProgressionChanges()
    {
        AssertBool(_f.App.CardIsLocked(2)).IsTrue();

        _f.Progression.Level = 4;
        _f.Progression.RaiseChanged();

        AssertBool(_f.App.CardIsLocked(2)).IsFalse();
        AssertThat(_f.App.CardLockText(2)).IsEqual("Add");
    }
}
