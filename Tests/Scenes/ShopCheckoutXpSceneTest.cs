using System.Threading.Tasks;
using CardCleaner.Scripts.Features.Shop.Models;
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

    [BeforeTest]
    public async Task Setup()
    {
        _rig = await ShopCheckoutRig.Create();
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
        var bar = _rig.Hud.LevelBar!;

        AssertThat(ReferenceEquals(bar.Progression, _rig.Progression)).IsTrue();
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
        var bar = _rig.Hud.LevelBar!;
        AssertThat(_rig.Progression.Xp).IsEqual(expected);
        AssertThat(Mathf.IsEqualApprox(bar.Bar!.Value, expected / (double)LevelTwoXp)).IsTrue();
        AssertThat(bar.LevelLabel!.Text).IsEqual("Level 1");

        // Let the first basket's props finish freeing before the next basket is scanned.
        await ISceneRunner.SyncProcessFrame;
        _rig.Counter.Checkout(new FakeCheckoutCustomer(PaymentMethod.Card, SecondBasketTotal));
        _rig.ScanAll();

        AssertThat(_rig.Counter.PayCard(SecondBasketTotal)).IsTrue();

        AssertThat(_rig.Progression.Xp).IsEqual(LevelTwoXp);
        AssertThat(bar.LevelLabel!.Text).IsEqual("Level 2");
    }

    [TestCase]
    [TestCategory("Unit")]
    public void WrongTotalRaisesNoXp()
    {
        _rig.Counter.Checkout(new FakeCheckoutCustomer(PaymentMethod.Card, 30, 40));
        _rig.ScanAll();

        AssertThat(_rig.Counter.PayCard(FirstBasketTotal + 1)).IsFalse();

        AssertThat(_rig.Progression.Xp).IsEqual(0);
        AssertThat(_rig.Hud.LevelBar!.LevelLabel!.Text).IsEqual("Level 1");
    }

    [TestCase]
    [TestCategory("Unit")]
    public void TopLevelShowsMax()
    {
        _rig.Progression.AwardCheckout(MaxLevelAmount);

        var bar = _rig.Hud.LevelBar!;
        AssertThat(bar.LevelLabel!.Text).IsEqual("Level 5 (max)");
        AssertThat(Mathf.IsEqualApprox(bar.Bar!.Value, 1.0)).IsTrue();
    }
}
