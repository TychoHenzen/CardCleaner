using System.Linq;
using System.Threading.Tasks;
using CardCleaner.Scripts.Core.Randomness;
using CardCleaner.Scripts.Features.Shop.Components;
using CardCleaner.Tests.TestUtilities;
using Godot;
using static CardCleaner.Tests.Scenes.ShopCustomerRig;

namespace CardCleaner.Tests.Scenes;

/// <summary>
/// Arrivals follow the shop day: opening the shop brings customers in, closing it stops new ones and lets the
/// customers already inside finish. The walk and the purchase are covered by <see cref="ShopCustomerSceneTest"/>.
/// </summary>
[TestSuite]
[RequireGodotRuntime]
public class ShopCustomerDaySceneTest
{
    private ShopCustomerRig _rig = null!;

    [BeforeTest]
    public async Task Setup()
    {
        _rig = await ShopCustomerRig.Create();
    }

    [AfterTest]
    public static void Teardown()
    {
        TestServices.Reset();
    }

    [TestCase]
    [TestCategory("Unit")]
    public async Task ClosingStopsArrivalsButACustomerInsideFinishes()
    {
        _rig.StockBooster();
        _rig.Spawner.CustomersPerDay = 6;
        _rig.Spawner.Rng = new Pcg32Random(400UL);
        _rig.Day.OpenShop();
        TickUntilFirstArrival();

        AssertBool(_rig.Spawner.SpawnedCount >= 1).IsTrue();
        AssertThat(_rig.Spawner.SpawnedCount + _rig.Spawner.PendingArrivals).IsEqual(6);
        var inside = CustomersInside().ToList();

        _rig.Day.CloseShop();
        AssertThat(_rig.Spawner.PendingArrivals).IsEqual(0);
        var balanceAtClose = _rig.Money.Balance;

        await WaitUntil(() => inside.All(Gone));

        AssertThat(_rig.Money.Balance).IsEqual(balanceAtClose + 4);
    }

    [TestCase]
    [TestCategory("Unit")]
    public async Task OpeningAtTheSignBringsACustomerWhoBuys()
    {
        _rig.StockBooster();
        _rig.Spawner.CustomersPerDay = 1;
        _rig.Spawner.Rng = new Pcg32Random(400UL);
        var sign = _rig.Shop.GetNode<ShopSign>("World/Storefront/ShopSign");
        var balanceBefore = _rig.Money.Balance;

        sign.Interact();
        AssertBool(_rig.Day.IsOpen).IsTrue();
        AssertThat(_rig.Spawner.PendingArrivals).IsEqual(1);

        TickUntilFirstArrival();

        AssertThat(_rig.Spawner.SpawnedCount).IsEqual(1);
        var customer = CustomersInside().Single();
        await WaitUntil(() => Gone(customer));

        AssertThat(_rig.Money.Balance).IsEqual(balanceBefore + 4);
        AssertThat(_rig.Day.Takings).IsEqual(4);
    }

    [TestCase]
    [TestCategory("Unit")]
    public void ZeroCustomersPerDaySpawnsNobody()
    {
        _rig.Spawner.CustomersPerDay = 0;
        var sign = _rig.Shop.GetNode<ShopSign>("World/Storefront/ShopSign");

        sign.Interact();
        AssertThat(_rig.Spawner.PendingArrivals).IsEqual(0);

        for (var i = 0; i < DayTickBudget; i++)
        {
            _rig.Day.Tick(1.0);
        }

        AssertThat(_rig.Spawner.SpawnedCount).IsEqual(0);
        AssertBool(CustomersInside().Any()).IsFalse();
    }

    private void TickUntilFirstArrival()
    {
        for (var i = 0; i < DayTickBudget && _rig.Spawner.SpawnedCount == 0; i++)
        {
            _rig.Day.Tick(1.0);
        }
    }

    private System.Collections.Generic.IEnumerable<Customer> CustomersInside() =>
        _rig.Shop.GetNode("World").GetChildren().OfType<Customer>();
}
