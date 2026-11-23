using System.Collections.Generic;
using System.Threading.Tasks;
using CardCleaner.Scripts.Features.Pause.Models;
using CardCleaner.Scripts.Features.Shop.Pricing;
using CardCleaner.Tests.TestUtilities;
using Godot;

namespace CardCleaner.Tests.Features.Shop.Ui;

/// <summary>
///     The pricing screen a price tag opens: it shows what is priced, what the price covers and the figures for the
///     price in its entry, and only Set price changes the stock's price. Cancel and Escape close it unchanged, and it
///     holds the cursor while it is open.
/// </summary>
[TestSuite]
[RequireGodotRuntime]
public class PricingScreenTest
{
    private RecordingCursor _cursor = null!;
    private PricingScreen _screen = null!;

    [BeforeTest]
    public void Setup()
    {
        TestServices.Reset();
        _cursor = new RecordingCursor();
        TestServices.Container.RegisterSingleton<ICursorOwner>(_cursor);
        _screen = AddNode(new PricingScreen());
    }

    [AfterTest]
    public void Teardown() => TestServices.Reset();

    [TestCase]
    public void OpenShowsTheStockAndItsFiguresAndHoldsTheCursor()
    {
        var stock = new FakeStock();

        _screen.Open(stock);

        AssertBool(_screen.IsOpen).IsTrue();
        AssertThat(_screen.Title.Text).IsEqual(stock.ItemName);
        AssertThat(_screen.ScopeText.Text).IsEqual(stock.Scope);
        AssertThat(_screen.Entry.Value).IsEqual(5.0);
        AssertThat(_screen.Figures.Text).IsEqual(string.Join("\n", PricingSheet.Lines(4, 2, 5)));
        AssertThat(_cursor.Claims).IsEqual(1);
    }

    [TestCase]
    public void TheFiguresFollowTheEntry()
    {
        _screen.Open(new FakeStock());

        _screen.RaiseButton.EmitSignal(BaseButton.SignalName.Pressed);

        AssertThat(_screen.Entry.Value).IsEqual(6.0);
        AssertThat(_screen.Figures.Text).IsEqual(string.Join("\n", PricingSheet.Lines(4, 2, 6)));
    }

    [TestCase]
    public void SetPriceAppliesTheEntryAndCloses()
    {
        var stock = new FakeStock();
        _screen.Open(stock);

        _screen.RaiseButton.EmitSignal(BaseButton.SignalName.Pressed);
        _screen.RaiseButton.EmitSignal(BaseButton.SignalName.Pressed);
        _screen.ApplyButton.EmitSignal(BaseButton.SignalName.Pressed);

        AssertThat(string.Join(",", stock.SetPrices)).IsEqual("7");
        AssertBool(_screen.IsOpen).IsFalse();
        AssertThat(_cursor.Releases).IsEqual(1);
    }

    [TestCase]
    public async Task TypedTextCountsWithoutEnter()
    {
        var stock = new FakeStock();
        _screen.Open(stock);

        // Typing sets the text and emits TextChanged, which setting Text alone does not. The SpinBox reads the text a
        // frame later, long before a player could reach Set price.
        var line = _screen.Entry.GetLineEdit();
        line.Text = "12";
        line.EmitSignal(LineEdit.SignalName.TextChanged, "12");
        await ISceneRunner.SyncProcessFrame;
        await ISceneRunner.SyncProcessFrame;
        _screen.ApplyButton.EmitSignal(BaseButton.SignalName.Pressed);

        AssertThat(string.Join(",", stock.SetPrices)).IsEqual("12");
    }

    [TestCase]
    public void MarketPriceMovesTheEntryToTheMarketPrice()
    {
        var stock = new FakeStock();
        _screen.Open(stock);

        _screen.MarketButton.EmitSignal(BaseButton.SignalName.Pressed);

        AssertThat(_screen.Entry.Value).IsEqual(4.0);
        AssertThat(stock.SetPrices.Count).IsEqual(0);
    }

    [TestCase]
    public void ThePriceCannotGoBelowOne()
    {
        var stock = new FakeStock { Price = 1 };
        _screen.Open(stock);

        _screen.LowerButton.EmitSignal(BaseButton.SignalName.Pressed);
        _screen.ApplyButton.EmitSignal(BaseButton.SignalName.Pressed);

        AssertThat(string.Join(",", stock.SetPrices)).IsEqual($"{ShopPrice.Min}");
    }

    [TestCase]
    public void CancelAndEscapeCloseWithoutAChange()
    {
        var stock = new FakeStock();
        _screen.Open(stock);
        _screen.RaiseButton.EmitSignal(BaseButton.SignalName.Pressed);
        _screen.CancelButton.EmitSignal(BaseButton.SignalName.Pressed);
        AssertBool(_screen.IsOpen).IsFalse();

        _screen.Open(stock);
        _screen._UnhandledInput(new InputEventAction { Action = "ui_cancel", Pressed = true });

        AssertBool(_screen.IsOpen).IsFalse();
        AssertThat(stock.SetPrices.Count).IsEqual(0);
        AssertThat(_cursor.Claims).IsEqual(2);
        AssertThat(_cursor.Releases).IsEqual(2);
    }

    [TestCase]
    public void AReopenShowsTheNewStockWithoutASecondClaim()
    {
        _screen.Open(new FakeStock());
        var other = new FakeStock { ItemName = "Pack", Price = 30, MarketValue = 30, UnitCost = 10 };

        _screen.Open(other);

        AssertThat(_screen.Title.Text).IsEqual("Pack");
        AssertThat(_screen.Entry.Value).IsEqual(30.0);
        AssertThat(_cursor.Claims).IsEqual(1);
    }

    private sealed class FakeStock : IPricedStock
    {
        public List<int> SetPrices { get; } = new();

        public string ItemName { get; init; } = "Booster";

        public string Scope => "The price applies to every booster in the shop.";

        public int MarketValue { get; init; } = 4;

        public int? UnitCost { get; init; } = 2;

        public int Price { get; init; } = 5;

        public void SetPrice(int price) => SetPrices.Add(price);
    }

    private sealed class RecordingCursor : ICursorOwner
    {
        public int Claims { get; private set; }

        public int Releases { get; private set; }

        public void Claim(object owner) => Claims++;

        public void Release(object owner) => Releases++;

        public bool HasClaimOtherThan(object owner) => false;
    }
}
