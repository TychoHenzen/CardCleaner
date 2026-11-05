using System.Linq;
using System.Threading.Tasks;
using CardCleaner.Scripts.Features.Shop.Components;
using CardCleaner.Scripts.Features.Shop.Models;
using CardCleaner.Scripts.Features.Shop.Ui;
using CardCleaner.Tests.Features.SaveSlots.Services;
using CardCleaner.Tests.TestUtilities;
using Godot;

namespace CardCleaner.Tests.Scenes;

/// <summary>
///     The Expansions app on the backoffice PC, clicked with real mouse input: it lists the first storefront lot
///     with its state, buys it through the Buy button, and shows a message when a purchase is refused.
/// </summary>
[TestSuite]
[RequireGodotRuntime]
public class ShopExpansionsAppSceneTest
{
    private const string DesktopViewportPath = "PcDesktopViewport";
    private const string LotName = "Side wing";
    private const string SecondLotName = "Back wing";
    private const string BuyText = "Buy";
    private const string OwnedText = "Owned";
    private const string MessageName = "Message";
    private const int LotPrice = 1500;
    private const int LotRent = 40;
    private const int LotCustomers = 6;
    private static readonly Vector2I RootWindowSize = new(1280, 720);

    private InMemorySlotFileSystem _files = null!;
    private ShopExpansionRig _rig = null!;
    private PcScreen _screen = null!;
    private Button _expansionsIcon = null!;
    private Control _app = null!;
    private Window _root = null!;
    private Vector2I _originalRootSize;

    [BeforeTest]
    public async Task Setup()
    {
        _files = new InMemorySlotFileSystem();
        _rig = await ShopExpansionRig.StartAsync(_files, TwoLotCatalog());
        _root = _rig.Shop.GetTree().Root;
        _screen = PcScreen.From(_rig.Shop);
        _expansionsIcon = _rig.Shop.GetNode<Button>($"{DesktopViewportPath}/PcDesktop/Icons/ExpansionsIcon");
        _app = _rig.Shop.GetNode<Control>($"{DesktopViewportPath}/ExpansionsApp");
        // A headless window is 64 x 64, which would collapse the desktop layout; mouse positions need the real size.
        _originalRootSize = _root.Size;
        _root.Size = RootWindowSize;
        await ISceneRunner.SyncPhysicsFrame;
        await ISceneRunner.SyncPhysicsFrame;
    }

    [AfterTest]
    public void Teardown()
    {
        _root.Size = _originalRootSize;
        _rig.Close();
        ShopPauseRig.Teardown();
    }

    [TestCase]
    [TestCategory("Unit")]
    public void ExpansionsAppIsWiredToTheDesktop()
    {
        var desktop = _rig.Shop.GetNode<PcDesktop>($"{DesktopViewportPath}/PcDesktop");

        AssertThat(desktop.ExpansionsApp).IsEqual(_app);
    }

    [TestCase]
    [TestCategory("Unit")]
    public async Task ListsTheLot()
    {
        // The money is not added and the shop is still level 1, so the lot reads as locked.
        await OpenExpansionsAppAsync();

        var text = TextOf(CardFor(LotName));
        AssertBool(text.Contains(LotPrice.ToString())).IsTrue();
        AssertBool(text.Contains($"Rent {LotRent}")).IsTrue();
        AssertBool(text.Contains($"+{LotCustomers} customers")).IsTrue();
        AssertBool(text.Contains("Locked level 2")).IsTrue();
    }

    [TestCase]
    [TestCategory("Unit")]
    public async Task BuyThroughTheApp()
    {
        _rig.MeetTheLotRequirements();
        await OpenExpansionsAppAsync();
        int before = _rig.Money.Balance;
        var buy = FindButtonWithText(CardFor(LotName), BuyText);
        AssertBool(buy != null && !buy.Disabled).IsTrue();

        uint regionBefore = NavigationServer3D.RegionGetIterationId(_rig.Region.GetRid());
        await _screen.Click(buy!);
        await ShopNavigationRig.WaitForBakeSync(_rig.Region, regionBefore);
        await ShopExpansionRig.SettleAsync();

        AssertInt(_rig.Money.Balance).IsEqual(before - LotPrice);
        AssertBool(TextOf(CardFor(LotName)).Contains(OwnedText)).IsTrue();
        AssertBool(FindButtonWithText(CardFor(LotName), BuyText) == null).IsTrue();
        AssertBool(MessageText().Contains("Bought")).IsTrue();
        AssertBool(_rig.Reaches(ShopExpansionRig.ShelfArea, ShopExpansionRig.LotStorefrontPart)).IsTrue();
    }

    [TestCase]
    [TestCategory("Unit")]
    public async Task LaterLotsLockedByOrder()
    {
        _rig.MeetTheLotRequirements();
        await OpenExpansionsAppAsync();

        var second = CardFor(SecondLotName);
        AssertBool(TextOf(second).Contains($"After {LotName}")).IsTrue();
        AssertBool(FindButtonWithText(second, BuyText) == null).IsTrue();

        uint regionBefore = NavigationServer3D.RegionGetIterationId(_rig.Region.GetRid());
        await _screen.Click(FindButtonWithText(CardFor(LotName), BuyText)!);
        await ShopNavigationRig.WaitForBakeSync(_rig.Region, regionBefore);
        await ShopExpansionRig.SettleAsync();

        AssertBool(TextOf(CardFor(SecondLotName)).Contains($"After {LotName}")).IsFalse();
        AssertBool(FindButtonWithText(CardFor(SecondLotName), BuyText) != null).IsTrue();
    }

    [TestCase]
    [TestCategory("Unit")]
    public async Task RefusalsShowAMessage()
    {
        // Locked: the money is there but the shop is still level 1. Nothing is charged and the lot stays closed.
        _rig.Money.Add(ShopExpansionRig.LotFunds);
        await OpenExpansionsAppAsync();
        int balance = _rig.Money.Balance;
        var locked = FindButtonWithText(CardFor(LotName), BuyText);
        AssertBool(locked != null).IsTrue();

        await _screen.Click(locked!);

        AssertBool(MessageText().Length > 0).IsTrue();
        AssertInt(_rig.Money.Balance).IsEqual(balance);
        AssertBool(_rig.View.IsOpen).IsFalse();

        // Unaffordable: the level is reached but the balance is empty. Nothing is charged.
        _rig.MeetTheLotRequirements();
        _rig.Money.TrySpend(_rig.Money.Balance);
        string lockedMessage = MessageText();
        balance = _rig.Money.Balance;
        // The rebuilt cards need a frame to lay out before a click can land on their button.
        await ShopExpansionRig.SettleAsync();
        var unaffordable = FindButtonWithText(CardFor(LotName), BuyText);
        AssertBool(unaffordable != null && !unaffordable.Disabled).IsTrue();

        await _screen.Click(unaffordable!);

        AssertBool(MessageText().Length > 0 && MessageText() != lockedMessage).IsTrue();
        AssertInt(_rig.Money.Balance).IsEqual(balance);
        AssertBool(TextOf(CardFor(LotName)).Contains(OwnedText)).IsFalse();
    }

    // The shipped first lot plus a synthetic second one, so the purchase order shows on the cards.
    private static ExpansionLotCatalog TwoLotCatalog()
    {
        var shipped = GD.Load<ExpansionLotCatalog>("res://Data/Shop/ExpansionLots.tres");
        var second = new ExpansionLotData
        {
            Id = "side_wing_2",
            Order = shipped.Lots.Max(lot => lot.Order) + 1,
            Price = 4000,
            UnlockId = shipped.Lots[0].UnlockId,
            DailyRent = 80,
            ExtraCustomers = 8,
            DisplayName = SecondLotName,
            StorefrontMinX = shipped.Lots[0].StorefrontMinX - 4f,
        };
        return new ExpansionLotCatalog { Lots = [.. shipped.Lots, second] };
    }

    private async Task OpenExpansionsAppAsync()
    {
        _rig.Shop.GetNode<ScreenTerminal>("World/Markers/PcLocation/PcTerminal").Interact();
        await _screen.Click(_expansionsIcon);
        AssertBool(_app.Visible).IsTrue();
    }

    private string MessageText() => _app.FindChild(MessageName, true, false) is Label label ? label.Text : string.Empty;

    private PanelContainer CardFor(string lotName) =>
        _app.FindChildren("*", "PanelContainer", true, false).OfType<PanelContainer>()
            .First(panel => panel.FindChildren("*", "Label", true, false).OfType<Label>()
                .Any(label => label.Text == lotName));

    private static string TextOf(Node card) =>
        string.Join(" | ", card.FindChildren("*", "Label", true, false).OfType<Label>().Select(label => label.Text));

    private static Button? FindButtonWithText(Node root, string text)
    {
        foreach (var child in root.GetChildren())
        {
            if (child is Button button && button.Text == text)
                return button;
            var found = FindButtonWithText(child, text);
            if (found != null)
                return found;
        }
        return null;
    }
}
