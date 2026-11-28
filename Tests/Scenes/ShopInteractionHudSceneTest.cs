using System.Threading.Tasks;
using CardCleaner.Scripts.Core.Interfaces;
using CardCleaner.Scripts.Core.Randomness;
using CardCleaner.Scripts.Core.Services;
using CardCleaner.Scripts.Features.Player.Controllers;
using CardCleaner.Scripts.Features.Player.Ui;
using CardCleaner.Scripts.Features.Shop.Customers;
using CardCleaner.Scripts.Features.Shop.Day;
using CardCleaner.Tests.TestUtilities;
using CardCleaner.Tests.TestUtilities.Helpers;
using Godot;

namespace CardCleaner.Tests.Scenes;

/// <summary>
///     The aim HUD in the shop: the prompt under the crosshair names what a click on the aimed object does, it
///     clears when the aim moves away, and the hint row shows what the hint board holds.
/// </summary>
[TestSuite]
[RequireGodotRuntime]
public class ShopInteractionHudSceneTest
{
    private const string HudPath = "Player/AimHud/InteractionHud";
    private const float SignCentreHeight = 0.6f;
    private const float StandingDistanceToSign = 1.5f;
    // Half the 1.8 m capsule plus the 0.9 m head: the camera sits 0.9 m above the player's origin.
    private const float CameraHeight = 0.9f;
    // InteractionSystem scans every 0.05 s of physics time, so an aim has to outlast one interval.
    private const int AimFrames = 6;

    private Node3D _shop = null!;
    private InteractionHud _hud = null!;
    private InteractionSystem _interaction = null!;
    private ControlHintBoard _board = null!;
    private Vector2I _rootSize;

    [BeforeTest]
    public async Task Setup()
    {
        // The headless window is 64 x 64, which collapses the HUD's rows to nothing.
        _rootSize = ((SceneTree)Engine.GetMainLoop()).Root.Size;
        ((SceneTree)Engine.GetMainLoop()).Root.Size = new Vector2I(1280, 720);

        _shop = ShopOrderingRig.LoadShopWithServices();
        var day = _shop.GetNode<ShopDayService>("Services/ShopDayService");
        TestServices.Container.RegisterSingleton<IShopDayService>(day);
        TestServices.Container.RegisterSingleton<IComplaintLog>(day);
        TestServices.Container.RegisterSingleton<IDailyBillBook>(
            new DailyBillBook(new IDailyBillSource[] { new RentBillSource() }));
        TestServices.Container.RegisterSingleton(RandomSources.Create(401UL));
        _board = new ControlHintBoard();
        TestServices.Container.RegisterSingleton<IControlHints>(_board);

        _hud = _shop.GetNode<InteractionHud>(HudPath);
        _interaction = _shop.GetNode<InteractionSystem>("Player/Head/Camera3D/InteractionSystem");
        AddNode(_shop);
        await ISceneRunner.SyncPhysicsFrame;
        await ISceneRunner.SyncPhysicsFrame;
    }

    [AfterTest]
    public void Teardown()
    {
        ((SceneTree)Engine.GetMainLoop()).Root.Size = _rootSize;
        TestServices.Reset();
    }

    [TestCase]
    public async Task AimingAtTheSignPromptsToOpenTheShop()
    {
        AimAtSign();
        await WaitForScan();

        AssertBool(_interaction.Target is ShopSign).IsTrue();
        AssertThat(_hud.PromptText).IsEqual("Open the shop");
    }

    [TestCase]
    public async Task LookingAwayClearsThePrompt()
    {
        AimAtSign();
        await WaitForScan();
        _interaction.Camera!.LookAt(_interaction.Camera.GlobalPosition + Vector3.Up * 5f + Vector3.Forward * 0.01f);
        await WaitForScan();

        AssertThat(_hud.PromptText).IsEqual(string.Empty);
        AssertObject(_interaction.Target).IsNull();
    }

    [TestCase]
    public async Task TheHintRowShowsThePublishedHints()
    {
        _board.Publish(this, [new ControlHint("Q", "Open the pack"), new ControlHint("RMB", "Put it down")]);
        await ISceneRunner.SyncProcessFrame;

        AssertThat(_hud.HintLines).ContainsExactly("Q: Open the pack", "RMB: Put it down");

        _board.Clear(this);
        await ISceneRunner.SyncProcessFrame;

        AssertThat(_hud.HintLines.Count).IsEqual(0);
    }

    private void AimAtSign()
    {
        var sign = _shop.GetNode<ShopSign>("World/Storefront/ShopSign");
        var centre = sign.GlobalPosition + new Vector3(0f, SignCentreHeight, 0f);
        var player = _shop.GetNode<Node3D>("Player");
        // Held in place: the camera has to sit level with the sign, which puts the capsule into the floor, and a
        // moving body would be pushed back out of it and lose the aim.
        player.SetPhysicsProcess(false);
        player.GlobalPosition = centre + new Vector3(StandingDistanceToSign, -CameraHeight, 0f);
        _interaction.Camera!.LookAt(centre);
    }

    private static async Task WaitForScan()
    {
        await BoundedWait.SyncPhysicsFrames(AimFrames);
        // An awaited process frame resumes before the nodes' _Process of that frame, so the HUD shows a scan from
        // the last physics step only one process frame later.
        await ISceneRunner.SyncProcessFrame;
        await ISceneRunner.SyncProcessFrame;
    }
}
