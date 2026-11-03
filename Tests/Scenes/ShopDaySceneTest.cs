using System.Threading.Tasks;
using CardCleaner.Scripts.Core.DependencyInjection;
using CardCleaner.Scripts.Features.Player.Controllers;
using CardCleaner.Scripts.Features.Shop.Components;
using CardCleaner.Scripts.Features.Shop.Services;
using CardCleaner.Scripts.Features.SaveSlots.Services;
using CardCleaner.Scripts.Features.Shop.Ui;
using CardCleaner.Tests.TestUtilities;
using Godot;

namespace CardCleaner.Tests.Scenes;

/// <summary>
/// The shop day wired into the shop scene: the day service sits under Services, the sign is a body on the
/// interaction layer that the player's ray hits, and interacting with it opens, then closes, the shop and shows
/// the day report until its Continue button is pressed.
/// </summary>
[TestSuite]
[RequireGodotRuntime]
public class ShopDaySceneTest
{
    private const float SignCentreHeight = 0.6f;
    private const float StandingDistanceToSign = 1.5f;

    private Node3D _shop = null!;
    private ShopDayService _service = null!;
    private ShopSign _sign = null!;
    private InteractionSystem _interaction = null!;
    private DayReportPanel _reportPanel = null!;
    private MoneyService _money = null!;

    [BeforeTest]
    public async Task Setup()
    {
        _shop = ShopOrderingRig.LoadShopWithServices();
        _service = _shop.GetNode<ShopDayService>("Services/ShopDayService");
        TestServices.Container.RegisterSingleton<IShopDayService>(_service);
        TestServices.Container.RegisterSingleton<IComplaintLog>(_service);
        TestServices.Container.RegisterSingleton<IDailyBillBook>(new DailyBillBook(new RentBillSource()));

        _sign = _shop.GetNode<ShopSign>("World/Storefront/ShopSign");
        _interaction = _shop.GetNode<InteractionSystem>("Player/Head/Camera3D/InteractionSystem");
        _reportPanel = _shop.GetNode<DayReportPanel>("DayUi/ReportPanel");
        _money = _shop.GetNode<MoneyService>("Services/MoneyService");

        AddNode(_shop);
        await ISceneRunner.SyncPhysicsFrame;
        await ISceneRunner.SyncPhysicsFrame;
    }

    [AfterTest]
    public static void Teardown()
    {
        TestServices.Reset();
    }

    [TestCase]
    [TestCategory("Unit")]
    public void ShopDayWiring()
    {
        AssertThat(_shop.GetNodeOrNull("Services/ShopDayService")).IsNotNull();
        AssertThat(_shop.GetNodeOrNull("World/Storefront/ShopSign")).IsNotNull();
        AssertThat(_shop.GetNodeOrNull("DayUi/Hud")).IsNotNull();
        AssertThat(_shop.GetNodeOrNull("DayUi/ReportPanel")).IsNotNull();

        AssertBool((_sign.CollisionLayer & 4u) != 0).IsTrue();
        AssertBool((_sign.CollisionLayer & _interaction.InteractableCollisionMask) != 0).IsTrue();
        AssertBool(ReferenceEquals(_sign.DayService, _service)).IsTrue();
    }

    [TestCase]
    [TestCategory("Unit")]
    public void SignIsInteractableByTheRay()
    {
        var centre = _sign.GlobalPosition + new Vector3(0f, SignCentreHeight, 0f);
        var from = centre + new Vector3(StandingDistanceToSign, 0f, 0f);

        var hit = _shop.GetWorld3D().DirectSpaceState.IntersectRay(new PhysicsRayQueryParameters3D
        {
            From = from, To = centre, CollideWithBodies = true, CollisionMask = _interaction.InteractableCollisionMask
        });

        AssertThat(hit.Count).IsGreater(0);
        AssertThat(hit["collider"].Obj).IsEqual(_sign);
    }

    [TestCase]
    [TestCategory("Unit")]
    public void SignOpensAndClosesTheShopInTheScene()
    {
        var save = new FakeSaveGame();
        _service.SaveCommand = save;

        _sign.Interact();

        AssertBool(_service.IsOpen).IsTrue();

        _sign.Interact();

        AssertBool(_service.IsOpen).IsFalse();
        AssertThat(_service.Day).IsEqual(2);
        AssertThat(_money.Balance).IsEqual(460);
        AssertBool(_reportPanel.Visible).IsTrue();
        AssertThat(save.Calls).IsEqual(1);

        _reportPanel.ContinueButton!.EmitSignal(BaseButton.SignalName.Pressed);

        AssertBool(_reportPanel.Visible).IsFalse();
    }

    // Stands in for the slot writer so the day close writes no file during the scene test.
    private sealed class FakeSaveGame : ISaveGameCommand
    {
        public int Calls { get; private set; }

        public bool ManualSave()
        {
            Calls++;
            return true;
        }
    }
}
