using System.Threading.Tasks;
using CardCleaner.Scripts.Core.DependencyInjection;
using CardCleaner.Scripts.Core.Randomness;
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
    private const string PcTerminalPath = "World/Markers/PcLocation/PcTerminal";
    private const string WorkshopTerminalPath = "World/Workshop/OrderingRoom/OrderTerminal";

    // Far longer than a shop day, so one tick runs the clock past closing time.
    private const double PastClosingTimeSeconds = 100000.0;

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
        TestServices.Container.RegisterSingleton<IDailyBillBook>(
            new DailyBillBook(new IDailyBillSource[] { new RentBillSource() }));
        TestServices.Container.RegisterSingleton(RandomSources.Create(400UL));

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
    public void ShopDayWiring()
    {
        AssertThat(_shop.GetNodeOrNull("Services/ShopDayService")).IsNotNull();
        AssertThat(_shop.GetNodeOrNull("World/Storefront/ShopSign")).IsNotNull();
        AssertThat(_shop.GetNodeOrNull("DayUi/Hud")).IsNotNull();
        AssertThat(_shop.GetNodeOrNull("DayUi/ReportPanel")).IsNotNull();

        AssertBool((_sign.CollisionLayer & 4u) != 0).IsTrue();
        AssertBool((_sign.CollisionLayer & _interaction.InteractableCollisionMask) != 0).IsTrue();
        AssertBool(ReferenceEquals(_sign.DayService, _service)).IsTrue();

        // The screens the day report closes before it shows: the shop PC, the workshop PC and the payment screen.
        foreach (var path in new[] { PcTerminalPath, WorkshopTerminalPath, "CheckoutUi" })
            AssertBool(_shop.GetNode(path).IsInGroup(IModalScreen.GroupName)).IsTrue();
    }

    [TestCase]
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

    // Closing time can come while the player is at the PC. The PC's fullscreen overlay sits above the report and
    // takes every click, so the report closes the PC before it shows.
    [TestCase]
    public void ClosingTimeAtThePcClosesThePcAndShowsAUsableReport()
    {
        _service.SaveCommand = new FakeSaveGame();
        var terminal = _shop.GetNode<ScreenTerminal>(PcTerminalPath);
        var overlay = _shop.GetNode<PcScreenOverlay>($"{PcTerminalPath}/PcScreenOverlay");
        var player = _shop.GetNode<PlayerController>("Player");
        _service.OpenShop();
        terminal.Open();
        AssertBool(overlay.Active).IsTrue();

        _service.Tick(PastClosingTimeSeconds);

        AssertBool(_service.IsOpen).IsFalse();
        AssertBool(terminal.IsOpen).IsFalse();
        AssertBool(overlay.Active).IsFalse();
        AssertBool(_reportPanel.Visible).IsTrue();
        AssertBool(player.ControlEnabled).IsFalse();

        _reportPanel.ContinueButton!.EmitSignal(BaseButton.SignalName.Pressed);

        AssertBool(player.ControlEnabled).IsTrue();
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
