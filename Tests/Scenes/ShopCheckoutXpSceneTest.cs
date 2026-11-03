using System.Threading.Tasks;
using CardCleaner.Scripts.Features.Shop.Models;
using CardCleaner.Scripts.Features.Shop.Services;
using CardCleaner.Scripts.Features.Shop.Ui;
using CardCleaner.Tests.Features.Shop;
using CardCleaner.Tests.TestUtilities;
using Godot;

namespace CardCleaner.Tests.Scenes;

/// <summary>
/// End to end in the shop scene: the level bar on the HUD follows the scene's progression, a paid checkout
/// raises its XP by the award for the total, and a wrong total raises nothing.
/// </summary>
[TestSuite]
[RequireGodotRuntime]
public class ShopCheckoutXpSceneTest
{
    private const int FirstBasketTotal = 70;
    private const int SecondBasketTotal = 430;
    private const int MaxLevelAmount = 5000;
    private const int LevelTwoXp = 50;

    private ShopCheckoutRig _rig = null!;
    private ShopProgression _progression = null!;
    private ShopHud _hud = null!;

    [BeforeTest]
    public async Task Setup()
    {
        _rig = await ShopCheckoutRig.Create();
        _progression = _rig.Shop.GetNode<ShopProgression>("Services/ShopProgression");
        _hud = _rig.Shop.GetNode<ShopHud>("DayUi/Hud");
    }

    [AfterTest]
    public static void Teardown()
    {
        TestServices.Reset();
    }

    [TestCase]
    [TestCategory("Unit")]
    public void HudBarIsWiredToTheSceneProgressionAndStartsAtLevelOne()
    {
        var bar = _hud.LevelBar!;

        AssertThat(ReferenceEquals(bar.Progression, _progression)).IsTrue();
        AssertThat(bar.LevelLabel!.Text).IsEqual("Level 1");
        AssertThat(Mathf.IsEqualApprox(bar.Bar!.Value, 0.0)).IsTrue();
    }

    [TestCase]
    [TestCategory("Unit")]
    public async Task PaidCheckoutRaisesXpByTheAwardAndMovesTheBar()
    {
        _rig.Counter.Checkout(new FakeCheckoutCustomer(PaymentMethod.Card, 30, 40));
        _rig.ScanAll();

        AssertThat(_rig.Counter.PayCard(FirstBasketTotal)).IsTrue();

        var expected = CheckoutXp.For(FirstBasketTotal);
        AssertThat(_progression.Xp).IsEqual(expected);
        AssertThat(Mathf.IsEqualApprox(_hud.LevelBar!.Bar!.Value, expected / (double)LevelTwoXp)).IsTrue();
        AssertThat(_hud.LevelBar.LevelLabel!.Text).IsEqual("Level 1");

        // Let the first basket's props finish freeing before the next basket is scanned.
        await ISceneRunner.SyncProcessFrame;
        _rig.Counter.Checkout(new FakeCheckoutCustomer(PaymentMethod.Card, SecondBasketTotal));
        _rig.ScanAll();

        AssertThat(_rig.Counter.PayCard(SecondBasketTotal)).IsTrue();

        AssertThat(_progression.Xp).IsEqual(LevelTwoXp);
        AssertThat(_hud.LevelBar.LevelLabel!.Text).IsEqual("Level 2");
    }

    [TestCase]
    [TestCategory("Unit")]
    public void WrongTotalRaisesNoXp()
    {
        _rig.Counter.Checkout(new FakeCheckoutCustomer(PaymentMethod.Card, 30, 40));
        _rig.ScanAll();

        AssertThat(_rig.Counter.PayCard(FirstBasketTotal + 1)).IsFalse();

        AssertThat(_progression.Xp).IsEqual(0);
        AssertThat(_hud.LevelBar!.LevelLabel!.Text).IsEqual("Level 1");
    }

    [TestCase]
    [TestCategory("Unit")]
    public void TopLevelShowsMax()
    {
        _progression.AwardCheckout(MaxLevelAmount);

        AssertThat(_hud.LevelBar!.LevelLabel!.Text).IsEqual("Level 5 (max)");
        AssertThat(Mathf.IsEqualApprox(_hud.LevelBar.Bar!.Value, 1.0)).IsTrue();
    }
}
