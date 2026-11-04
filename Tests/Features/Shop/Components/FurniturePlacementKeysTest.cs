using CardCleaner.Scripts.Core.Interfaces;
using CardCleaner.Scripts.Core.Services;
using CardCleaner.Scripts.Features.Shop.Components;
using CardCleaner.Tests.TestUtilities;
using Godot;

namespace CardCleaner.Tests.Features.Shop.Components;

/// <summary>Key wiring of the placement controller through the real input service.</summary>
[TestSuite]
[RequireGodotRuntime]
public class FurniturePlacementKeysTest
{
    private const string BoxScenePath = "res://Scenes/Shop/Items/ShelfBox.tscn";

    private InputService _input = null!;
    private Node3D _root = null!;
    private Node3D _world = null!;
    private FurniturePlacementController _controller = null!;

    [BeforeTest]
    public void Setup()
    {
        TestServices.Reset();
        _input = new InputService();
        TestServices.Container.RegisterSingleton<IInputService>(_input);
        _root = new Node3D { Name = "Furniture" };
        _world = new Node3D { Name = "World" };
        _controller = TestServices.Inject(new FurniturePlacementController { FurnitureRoot = _root });
        // Boxes live under their own node so the furniture root only counts placed pieces.
        AddNode(_root);
        AddNode(_world);
        AddNode(_input);
        AddNode(_controller);
        TestServices.ExecutePendingCallbacks();
        // The camera aim is not driven headless; the cases place the pose with AimAt.
        _controller.AimOverride = true;
    }

    [AfterTest]
    public void TearDown()
    {
        // Cancelling a move in progress puts the carried piece back down before the runner frees the nodes.
        _controller.Cancel();
        TestServices.Reset();
        // The nodes are freed by the test runner; drop the references so no case reads a freed node.
        _controller = null!;
        _world = null!;
        _root = null!;
        _input = null!;
    }

    [TestCase]
    [TestCategory("Unit")]
    public void RotateKeyTurnsTheGhost()
    {
        AssertBool(_controller.Begin(NewBox())).IsTrue();

        Press(Key.T);

        AssertInt(_controller.CurrentPose.QuarterTurns).IsEqual(1);
    }

    [TestCase]
    [TestCategory("Unit")]
    public void ConfirmKeyPlacesAndCancelKeyKeepsTheBox()
    {
        AssertBool(_controller.Begin(NewBox())).IsTrue();
        _controller.AimAt(8f, 6f);

        Press(Key.E);

        AssertInt(_root.GetChildCount()).IsEqual(1);
        AssertBool(_root.GetChild(0) is PlacedFurniture).IsTrue();

        var second = NewBox();
        AssertBool(_controller.Begin(second)).IsTrue();

        Press(Key.C);

        AssertBool(_controller.IsActive).IsFalse();
        AssertInt(_root.GetChildCount()).IsEqual(1);
        AssertBool(GodotObject.IsInstanceValid(second)).IsTrue();
    }

    [TestCase]
    [TestCategory("Unit")]
    public void KeysDoNothingWithoutASession()
    {
        Press(Key.T);
        Press(Key.E);
        Press(Key.C);

        AssertBool(_controller.IsActive).IsFalse();
        AssertInt(_root.GetChildCount()).IsEqual(0);
    }

    /// <summary>Instances a shelf box, injects its services and adds it under the World node.</summary>
    private FurnitureBox NewBox()
    {
        var box = GD.Load<PackedScene>(BoxScenePath).Instantiate<FurnitureBox>();
        TestServices.Inject(box);
        _world.AddChild(box);
        return box;
    }

    private void Press(Key key) =>
        _input._Input(new InputEventKey { Keycode = key, Pressed = true });
}
