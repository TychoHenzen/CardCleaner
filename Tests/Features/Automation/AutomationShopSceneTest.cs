using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Nodes;
using System.Threading.Tasks;
using CardCleaner.Scripts.Core.Interfaces;
using CardCleaner.Scripts.Features.Automation;
using CardCleaner.Scripts.Features.Player.Controllers;
using CardCleaner.Scripts.Features.Shop.Components;
using CardCleaner.Tests.Scenes;
using CardCleaner.Tests.TestUtilities;
using Godot;
using IServiceProvider = CardCleaner.Scripts.Core.Interfaces.IServiceProvider;

namespace CardCleaner.Tests.Features.Automation;

/// <summary>
///     The automation commands against the real shop scene, headless: aiming and walking go through the player
///     controller's own mouse look and movement input, and "use" opens the backoffice PC the way a click does.
/// </summary>
[TestSuite]
[RequireGodotRuntime]
public class AutomationShopSceneTest
{
    private static readonly Vector2I RootWindowSize = new(1280, 720);

    private SceneTree _tree = null!;
    private Node? _previousScene;
    private Node3D _shop = null!;
    private PlayerController _player = null!;
    private AutomationCommands _commands = null!;
    private Vector2I _originalRootSize;
    private readonly List<Node> _coreServiceNodes = new();

    // Composed the way the game composes the shop (as ShopWorkshopMapSessionSceneTest does): the player's mouse look
    // and movement need the core InputService and every scene provider, which a hand-registered rig leaves out.
    [BeforeTest]
    public void Setup()
    {
        _tree = (SceneTree)Engine.GetMainLoop();
        _previousScene = _tree.CurrentScene;
        _originalRootSize = _tree.Root.Size;
        _tree.Root.Size = RootWindowSize;

        TestServices.Reinitialize();
        _coreServiceNodes.Add((Node)TestServices.Resolver.Get<IInputService>());
        _coreServiceNodes.Add((Node)TestServices.Resolver.Get<IPlayerResetService>());

        _shop = GD.Load<PackedScene>(ShopSceneProbe.ScenePath).Instantiate<Node3D>();
        _tree.Root.AddChild(_shop);
        _tree.CurrentScene = _shop;
        foreach (var node in _tree.GetNodesInGroup("service_providers"))
        {
            if (node is IServiceProvider provider)
                provider.RegisterServices(TestServices.Container);
        }

        TestServices.ExecutePendingCallbacks();
        _player = _shop.GetNode<PlayerController>("Player");
        _commands = new AutomationCommands(_shop);
    }

    [AfterTest]
    public void Teardown()
    {
        Input.ParseInputEvent(new InputEventAction { Action = PathWalker.ForwardAction, Pressed = false });
        TestServices.Reset();
        _tree.CurrentScene = _previousScene;
        _tree.Root.RemoveChild(_shop);
        _shop.Free();
        foreach (var node in _coreServiceNodes.Where(GodotObject.IsInstanceValid))
            node.QueueFree();
        _coreServiceNodes.Clear();
        _tree.Root.Size = _originalRootSize;
    }

    [TestCase]
    [TestCategory("Unit")]
    public async Task LookTurnsThePlayerThroughMouseMotion()
    {
        await Settle();

        var reply = await Run("look", new JsonObject { ["yaw"] = 90, ["pitch"] = -20 });

        AssertBool(reply["ok"]!.GetValue<bool>()).OverrideFailureMessage(reply.ToJsonString()).IsTrue();
        var angles = ViewAim.Angles(_commands);
        AssertFloat(Mathf.Abs(AutomationView.AngleDelta(angles.X, 90))).IsLessEqual(1.01f);
        AssertFloat(angles.Y).IsEqualApprox(-20, 1.01f);
    }

    [TestCase]
    [TestCategory("Unit")]
    public async Task UnknownCommandAndMissingNodeAreErrorReplies()
    {
        var unknown = await Run("fly", new JsonObject());
        var missing = await Run("walk_to", new JsonObject { ["node"] = "/root/Nowhere" });

        AssertThat(unknown["error"]!.GetValue<string>()).Contains("unknown command");
        AssertThat(missing["error"]!.GetValue<string>()).Contains("no node at /root/Nowhere");
    }

    [TestCase]
    [TestCategory("Unit")]
    public async Task FindByScriptTypeReturnsTheNodesThatCarryThatScript()
    {
        var reply = await Run("find", new JsonObject { ["type"] = nameof(PlayerController) });

        AssertBool(reply["ok"]!.GetValue<bool>()).OverrideFailureMessage(reply.ToJsonString()).IsTrue();
        AssertThat(reply["result"]!.AsArray().Select(path => path!.GetValue<string>()).ToList())
            .Contains(_player.GetPath().ToString());
    }

    [TestCase]
    [TestCategory("Unit")]
    public void TheAgentIsRegisteredAsAnAutoload()
    {
        // A line outside [autoload] is a plain setting, and the game would never start the agent.
        AssertThat(ProjectSettings.GetSetting("autoload/Automation", "").AsString())
            .IsEqual("*res://Scripts/Features/Automation/AutomationAgent.cs");
    }

    [TestCase(Timeout = 60000)]
    public async Task UseWalksToThePcAndOpensIt()
    {
        await Settle();
        var terminal = _shop.GetNode<ScreenTerminal>("World/Markers/PcLocation/PcTerminal");

        var reply = await Run("use",
            new JsonObject { ["node"] = terminal.GetPath().ToString(), ["distance"] = 1.2 });

        AssertBool(reply["ok"]!.GetValue<bool>()).OverrideFailureMessage(reply.ToJsonString()).IsTrue();
        AssertFloat(reply["result"]!["walk"]!["distance"]!.GetValue<double>()).IsLessEqual(1.2);
        AssertBool(terminal.IsOpen).IsTrue();
        AssertBool(_player.ControlEnabled).IsFalse();
    }

    private static async Task Settle()
    {
        await WorkshopSceneRig.Settle();
    }

    private async Task<JsonNode> Run(string command, JsonObject args)
    {
        var line = new JsonObject { ["id"] = 1, ["cmd"] = command, ["args"] = args }.ToJsonString();
        return JsonNode.Parse(await _commands.Execute(AutomationProtocol.Parse(line)))!;
    }
}
