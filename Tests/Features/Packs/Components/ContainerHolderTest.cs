using CardCleaner.Scripts.Core.Interfaces;
using CardCleaner.Scripts.Core.Randomness;
using CardCleaner.Scripts.Features.Packs.Components;
using CardCleaner.Scripts.Features.Packs.Services;
using CardCleaner.Tests.TestUtilities;
using System;
using Godot;
using NSubstitute;

namespace CardCleaner.Tests.Features.Packs.Components;

[TestSuite]
[RequireGodotRuntime]
public class ContainerHolderTest
{
    private const string BoxScene = "res://Scenes/Shop/Items/CardboardBox.tscn";
    private const string PackScene = "res://Scenes/Shop/Items/Pack.tscn";
    private const string BoosterScene = "res://Scenes/Shop/Items/Booster.tscn";
    private const ulong Seed = 777;

    private Node3D _world = null!;
    private Node3D _hand = null!;
    private Mocking.MockCardSpawner _cardSpawner = null!;
    private RecordingCardSpawner _spawner = null!;
    private ContainerHolder _holder = null!;
    private Action? _qPress;

    [Before]
    public static void SetupSuite()
    {
        TestServices.Reset();
    }

    [BeforeTest]
    public void Setup()
    {
        _world = Assertions.AddNode(new Node3D());
        // The hand anchor is the camera in the game (CardHighlighter passes it), so a held container's parent is a Camera3D.
        _hand = new Camera3D { Name = "HandAnchor" };
        _world.AddChild(_hand);

        _cardSpawner = new Mocking.MockCardSpawner { Name = "MockSpawner" };
        _world.AddChild(_cardSpawner);
        TestServices.Container.RegisterSingleton<ICardSpawner>(_cardSpawner);

        _spawner = new RecordingCardSpawner();

        var input = Substitute.For<IInputService>();
        TestServices.Container.RegisterSingleton(input);
        input.When(x => x.RegisterAction(Arg.Any<object>(), "container_open", Key.Q, Arg.Any<Action>()))
            .Do(ci => _qPress = ci.Arg<Action>());
        _holder = TestServices.Inject(new ContainerHolder());
        _world.AddChild(_holder);
        _holder.SetReferences(_hand);
    }

    [AfterTest]
    public static void Cleanup()
    {
        TestServices.Reset();
    }

    [TestCase]
    [TestCategory("Unit")]
    public void PickUp_Box_Held_Frozen_NoCollision_ChildOfAnchor()
    {
        var box = Make(BoxScene);
        box.CollisionLayer = 4;
        box.CollisionMask = 6;

        AssertBool(_holder.TryPickUp(box)).IsTrue();

        AssertBool(_holder.IsHolding).IsTrue();
        AssertThat(_holder.Held).IsSame(box);
        AssertBool(box.IsHeld).IsTrue();
        AssertBool(box.Freeze).IsTrue();
        AssertThat(box.CollisionLayer).IsEqual(0u);
        AssertThat(box.CollisionMask).IsEqual(0u);
        AssertBool(box.GetParent() == _hand).IsTrue();
    }

    [TestCase]
    [TestCategory("Unit")]
    public void PickUp_Pack_Works()
    {
        var pack = Make(PackScene);

        AssertBool(_holder.TryPickUp(pack)).IsTrue();

        AssertThat(_holder.Held).IsSame(pack);
        AssertBool(pack.GetParent() == _hand).IsTrue();
    }

    [TestCase]
    [TestCategory("Unit")]
    public void PickUp_Booster_Works()
    {
        var booster = Make(BoosterScene);

        AssertBool(_holder.TryPickUp(booster)).IsTrue();

        AssertThat(_holder.Held).IsSame(booster);
        AssertBool(booster.GetParent() == _hand).IsTrue();
    }

    [TestCase]
    [TestCategory("Unit")]
    public void PickUp_WhileHolding_ReturnsFalse_HeldUnchanged()
    {
        var first = Make(BoxScene);
        var second = Make(PackScene);
        _holder.TryPickUp(first);

        AssertBool(_holder.TryPickUp(second)).IsFalse();

        AssertThat(_holder.Held).IsSame(first);
        AssertBool(second.GetParent() == _world).IsTrue();
        AssertBool(second.Freeze).IsFalse();
    }

    [TestCase]
    [TestCategory("Unit")]
    public void Drop_RestoresLayerMaskFreezeAndParent()
    {
        var box = Make(BoxScene);
        box.CollisionLayer = 4;
        box.CollisionMask = 6;
        _holder.TryPickUp(box);

        _holder.Drop();

        AssertBool(_holder.IsHolding).IsFalse();
        AssertBool(box.GetParent() == _cardSpawner).IsTrue();
        AssertThat(box.CollisionLayer).IsEqual(4u);
        AssertThat(box.CollisionMask).IsEqual(6u);
        AssertBool(box.Freeze).IsFalse();
    }

    [TestCase]
    [TestCategory("Unit")]
    public void Drop_PutsContainerDropDistanceInFront()
    {
        var box = Make(BoxScene);
        _holder.TryPickUp(box);

        _holder.Drop();

        AssertThat(box.GlobalPosition).IsEqual(new Vector3(0f, 0f, -_holder.DropDistance));
    }

    [TestCase]
    [TestCategory("Unit")]
    public void OpenHeld_DropsThenOpens()
    {
        var box = Make(BoxScene);
        _holder.TryPickUp(box);

        AssertBool(_holder.OpenHeld()).IsTrue();

        AssertBool(_holder.IsHolding).IsFalse();
        AssertBool(box.IsOpened).IsTrue();
        AssertBool(box.GetParent() == _cardSpawner).IsTrue();
    }

    [TestCase]
    [TestCategory("Unit")]
    public void QKey_OpensHeldContainer()
    {
        var box = Make(BoxScene);
        _holder.TryPickUp(box);

        _qPress!.Invoke();

        AssertBool(box.IsOpened).IsTrue();
        AssertBool(_holder.IsHolding).IsFalse();
    }

    private CardContainer Make(string scenePath)
    {
        var container = GD.Load<PackedScene>(scenePath).Instantiate<CardContainer>();
        container.Generator = new CardPackGenerator(RandomSources.Create(Seed));
        container.Spawning = _spawner;
        _world.AddChild(container);
        return container;
    }
}
