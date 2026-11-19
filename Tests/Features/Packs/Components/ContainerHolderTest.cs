using CardCleaner.Scripts.Core.Interfaces;
using CardCleaner.Scripts.Core.Randomness;
using CardCleaner.Scripts.Features.Card.Components;
using CardCleaner.Scripts.Features.Packs.Components;
using CardCleaner.Scripts.Features.Packs.Services;
using CardCleaner.Tests.TestUtilities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using CardCleaner.Scripts.Features.Packs.Models;
using Godot;
using NSubstitute;

namespace CardCleaner.Tests.Features.Packs.Components;

[TestSuite]
[RequireGodotRuntime]
public partial class ContainerHolderTest
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
        // The hand anchor is the camera in the game (CardHighlighter passes it), so a held container's parent is a
        // Camera3D.
        _hand = new Camera3D { Name = "HandAnchor" };
        _world.AddChild(_hand);

        _cardSpawner = new Mocking.MockCardSpawner { Name = "MockSpawner" };
        _world.AddChild(_cardSpawner);
        TestServices.Container.RegisterSingleton<ICardSpawner>(_cardSpawner);

        _spawner = new RecordingCardSpawner();

        var input = Substitute.For<IInputService>();
        TestServices.Container.RegisterSingleton(input);
        input.When(x => x.RegisterAction(Arg.Any<object>(), "container_put_down", Key.Q, Arg.Any<Action>()))
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

    // A booster can be held with cards in hand, which fan out on the right, so it is held on the left, upright and
    // facing the player; a box is carried level.
    [TestCase]
    [TestCategory("Unit")]
    public void PickUp_Booster_HeldUprightAtTheBoosterOffsetAndABoxLevelAtTheUsualOne()
    {
        var booster = Make(BoosterScene);
        _holder.TryPickUp(booster);
        AssertThat(booster.Position).IsEqual(_holder.BoosterHoldOffset);
        AssertBool(booster.Basis.IsEqualApprox(CardFan.FacingCamera)).IsTrue();
        _holder.Drop();

        var box = Make(BoxScene);
        _holder.TryPickUp(box);
        AssertThat(box.Position).IsEqual(_holder.HoldOffset);
        AssertBool(box.Basis.IsEqualApprox(Basis.Identity)).IsTrue();
        AssertBool(_holder.BoosterHoldOffset.X < 0f).IsTrue();
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
    public void UseHeld_Box_OpensItsLidInTheHandAndSpillsNothing()
    {
        var box = Make(BoxScene);
        _holder.TryPickUp(box);

        AssertBool(_holder.UseHeld()).IsTrue();

        AssertBool(box.IsLidOpen).IsTrue();
        AssertBool(box.IsOpened).IsFalse();
        AssertThat(_holder.Held).IsSame(box);
        AssertThat(box.Remaining).IsEqual(8);
    }

    [TestCase]
    [TestCategory("Unit")]
    public void UseHeld_OpenBox_ClosesItsFlaps()
    {
        var box = Make(BoxScene);
        _holder.TryPickUp(box);
        _holder.UseHeld();

        AssertBool(_holder.UseHeld()).IsTrue();

        AssertBool(box.IsLidOpen).IsFalse();
        AssertThat(_holder.Held).IsSame(box);
    }

    // A booster box opened in the hand empties into it: the hand holds its eight booster packs, in the order and with
    // the seeds opening it whole gives them, and the empty box is gone from the hand.
    [TestCase]
    [TestCategory("Unit")]
    public void UseHeld_BoosterBox_EmptiesIntoTheHandAsItsEightBoosterPacks()
    {
        var pack = Make(PackScene);
        pack.ContentSeed = Seed;
        _holder.TryPickUp(pack);

        AssertBool(_holder.UseHeld()).IsTrue();

        var held = HeldContainers();
        AssertThat(held.Select(booster => booster.ContentSeed!.Value).ToArray()).IsEqual(
            Enumerable.Range(0, ContainerHolder.MostBoosters).Select(i => ContainerSeeds.ChildSeed(Seed, i)).ToArray());
        AssertThat(_holder.HeldCount).IsEqual(ContainerHolder.MostBoosters);
        AssertThat(_holder.Held).IsSame(held[^1]);
        AssertBool(held.All(booster => booster is { Kind: CardContainerKind.Booster, Freeze: true, CollisionLayer: 0 }))
            .IsTrue();
        AssertBool(pack.IsOpened).IsTrue();
        AssertBool(pack.IsInsideTree()).IsFalse();
    }

    [TestCase]
    [TestCategory("Unit")]
    public void TheHandStacksUpToEightBoosterPacksAndRefusesANinth()
    {
        var boosters = Enumerable.Range(0, ContainerHolder.MostBoosters + 1).Select(_ => Make(BoosterScene)).ToList();
        for (var i = 0; i < ContainerHolder.MostBoosters; i++)
            AssertBool(_holder.TryPickUp(boosters[i])).IsTrue();

        AssertBool(_holder.TryPickUp(boosters[^1])).IsFalse();

        AssertThat(_holder.HeldCount).IsEqual(ContainerHolder.MostBoosters);
        AssertThat(_holder.Held).IsSame(boosters[ContainerHolder.MostBoosters - 1]);
        AssertBool(boosters[^1].GetParent() == _world).IsTrue();
        // Each booster pack after the first is held nearer the player, so none sits inside another.
        for (var i = 1; i < ContainerHolder.MostBoosters; i++)
            AssertBool(boosters[i].Position.Z - boosters[i - 1].Position.Z > 0.045f).IsTrue();
    }

    [TestCase]
    [TestCategory("Unit")]
    public void AHandOfBoosterPacksTakesNoBoxAndABoxInHandTakesNoBoosterPack()
    {
        var booster = Make(BoosterScene);
        var box = Make(BoxScene);
        _holder.TryPickUp(booster);

        AssertBool(_holder.TryPickUp(box)).IsFalse();
        AssertBool(_holder.TryPickUp(Make(PackScene))).IsFalse();
        _holder.Drop();
        _holder.TryPickUp(box);
        AssertBool(_holder.TryPickUp(booster)).IsFalse();

        AssertThat(_holder.HeldCount).IsEqual(1);
        AssertThat(_holder.Held).IsSame(box);
    }

    // Q sets down only the top booster pack, with its own collision back; the one under it stays in the hand.
    [TestCase]
    [TestCategory("Unit")]
    public void QPutsDownOnlyTheTopBoosterPack()
    {
        var first = Make(BoosterScene);
        var second = Make(BoosterScene);
        first.CollisionLayer = 4;
        second.CollisionLayer = 6;
        _holder.TryPickUp(first);
        _holder.TryPickUp(second);

        _qPress!.Invoke();

        AssertBool(second.GetParent() == _cardSpawner).IsTrue();
        AssertThat(second.CollisionLayer).IsEqual(6u);
        AssertThat(_holder.Held).IsSame(first);
        AssertBool(first.GetParent() == _hand).IsTrue();

        _qPress!.Invoke();

        AssertThat(first.CollisionLayer).IsEqual(4u);
        AssertBool(_holder.IsHolding).IsFalse();
    }

    [TestCase]
    [TestCategory("Unit")]
    public void ABoosterPackFreedFromTheMiddleOfTheStackClosesItUp()
    {
        var boosters = Enumerable.Range(0, 3).Select(_ => Make(BoosterScene)).ToList();
        foreach (var booster in boosters)
            _holder.TryPickUp(booster);
        var middle = boosters[1].Position;

        boosters[1].Free();

        AssertThat(_holder.HeldCount).IsEqual(2);
        AssertThat(boosters[2].Position).IsEqual(middle);
        AssertThat(_holder.Held).IsSame(boosters[2]);
    }

    // A booster opens into the hand of cards (BoosterRevealTest); a holder given no hand keeps it held and shut.
    [TestCase]
    [TestCategory("Unit")]
    public void UseHeld_Booster_WithNoHandOfCards_StaysHeldAndShut()
    {
        var booster = Make(BoosterScene);
        _holder.TryPickUp(booster);

        AssertBool(_holder.UseHeld()).IsFalse();

        AssertThat(_holder.Held).IsSame(booster);
        AssertBool(booster.IsOpened).IsFalse();
        AssertThat(_spawner.Spawned.Count).IsEqual(0);
    }

    [TestCase]
    [TestCategory("Unit")]
    public void QKey_PutsTheHeldContainerDownShut()
    {
        var box = Make(BoxScene);
        _holder.TryPickUp(box);

        _qPress!.Invoke();

        AssertBool(_holder.IsHolding).IsFalse();
        AssertBool(box.IsOpened).IsFalse();
        AssertBool(box.GetParent() == _cardSpawner).IsTrue();
    }

    [TestCase]
    [TestCategory("Unit")]
    public void QKey_PutsAnOpenBoxDownWithItsLidStillOpen()
    {
        var box = Make(BoxScene);
        _holder.TryPickUp(box);
        _holder.UseHeld();

        _qPress!.Invoke();

        AssertBool(_holder.IsHolding).IsFalse();
        AssertBool(box.IsLidOpen).IsTrue();
        AssertBool(box.IsOpened).IsFalse();
    }

    [TestCase]
    [TestCategory("Unit")]
    public void HeldContainerFreed_ClearsHeld()
    {
        var log = new RecordingLog();
        var previous = ILog.Sink;
        ILog.Sink = log;
        try
        {
            var box = Make(BoxScene);
            _holder.TryPickUp(box);

            box.Free();

            AssertThat(_holder.Held).IsNull();
            AssertBool(_holder.IsHolding).IsFalse();
            AssertThat(log.Errors).IsEmpty();
        }
        finally
        {
            ILog.Sink = previous;
        }
    }

    [TestCase]
    [TestCategory("Unit")]
    public void HeldContainerOpened_ClearsHeld()
    {
        var log = new RecordingLog();
        var previous = ILog.Sink;
        ILog.Sink = log;
        try
        {
            // A camera anchor refuses Open() while the container is held, so this case holds it on a plain Node3D.
            var anchor = new Node3D();
            _world.AddChild(anchor);
            _holder.SetReferences(anchor);
            // A cardboard box never opens whole, so the case holds a booster box.
            var pack = Make(PackScene);
            _holder.TryPickUp(pack);

            AssertBool(pack.Open()).IsTrue();

            AssertThat(_holder.Held).IsNull();
            AssertBool(_holder.IsHolding).IsFalse();
            AssertThat(log.Errors).IsEmpty();
        }
        finally
        {
            ILog.Sink = previous;
        }
    }

    [TestCase]
    [TestCategory("Unit")]
    public void PickUp_FreedContainer_ReturnsFalse()
    {
        var log = new RecordingLog();
        var previous = ILog.Sink;
        ILog.Sink = log;
        try
        {
            var box = Make(BoxScene);
            box.Free();

            AssertBool(_holder.TryPickUp(box)).IsFalse();

            AssertThat(_holder.Held).IsNull();
            AssertThat(log.Errors).IsEmpty();
        }
        finally
        {
            ILog.Sink = previous;
        }
    }

    [TestCase]
    [TestCategory("Unit")]
    public void PickUp_OpenedContainer_ReturnsFalse()
    {
        var log = new RecordingLog();
        var previous = ILog.Sink;
        ILog.Sink = log;
        try
        {
            var pack = Make(PackScene);
            AssertBool(pack.Open()).IsTrue();

            AssertBool(_holder.TryPickUp(pack)).IsFalse();

            AssertThat(_holder.Held).IsNull();
            AssertThat(log.Errors).IsEmpty();
        }
        finally
        {
            ILog.Sink = previous;
        }
    }

    [TestCase]
    [TestCategory("Unit")]
    public void Drop_NothingHeld_NoOp()
    {
        var log = new RecordingLog();
        var previous = ILog.Sink;
        ILog.Sink = log;
        try
        {
            var box = Make(BoxScene);

            _holder.Drop();

            AssertBool(box.GetParent() == _world).IsTrue();
            AssertBool(box.Freeze).IsFalse();
            AssertBool(_holder.IsHolding).IsFalse();
            AssertThat(log.Errors).IsEmpty();
        }
        finally
        {
            ILog.Sink = previous;
        }
    }

    [TestCase]
    [TestCategory("Unit")]
    public void UseHeld_NothingHeld_False()
    {
        var log = new RecordingLog();
        var previous = ILog.Sink;
        ILog.Sink = log;
        try
        {
            var box = Make(BoxScene);

            AssertBool(_holder.UseHeld()).IsFalse();

            AssertBool(box.IsOpened).IsFalse();
            AssertBool(box.IsLidOpen).IsFalse();
            AssertThat(log.Errors).IsEmpty();
        }
        finally
        {
            ILog.Sink = previous;
        }
    }

    [TestCase]
    [TestCategory("Unit")]
    public async Task InTree_IsInContainerHoldersGroup()
    {
        var tree = _world.GetTree();
        // Setup already put one holder in the tree, so the group holds that one and this one.
        var holder = TestServices.Inject(new ContainerHolder());
        _world.AddChild(holder);

        AssertBool(holder.IsInGroup(ContainerHolder.GroupName)).IsTrue();
        AssertThat(tree.GetNodesInGroup(ContainerHolder.GroupName).Count).IsEqual(2);

        holder.QueueFree();
        await _world.ToSignal(tree, SceneTree.SignalName.ProcessFrame);

        AssertBool(GodotObject.IsInstanceValid(holder)).IsFalse();
        AssertThat(tree.GetNodesInGroup(ContainerHolder.GroupName).Count).IsEqual(1);
    }

    private sealed class RecordingLog : ILog
    {
        public List<string> Errors { get; } = new();

        public void LogMessage(string message)
        {
        }

        public void LogWarning(string message)
        {
        }

        public void LogError(string message) => Errors.Add(message);

        public void LogError(string message, Exception exception) => Errors.Add(message);
    }

    private List<CardContainer> HeldContainers() => _hand.GetChildren().OfType<CardContainer>().ToList();

    private CardContainer Make(string scenePath)
    {
        var container = GD.Load<PackedScene>(scenePath).Instantiate<CardContainer>();
        container.Generator = new CardPackGenerator(RandomSources.Create(Seed));
        container.Spawning = _spawner;
        _world.AddChild(container);
        return container;
    }
}
