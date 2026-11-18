using System;
using System.Linq;
using System.Threading.Tasks;
using CardCleaner.Scripts.Core.Interfaces;
using CardCleaner.Scripts.Features.Card.Components;
using CardCleaner.Scripts.Features.Card.Services.SaveGame;
using CardCleaner.Scripts.Features.Packs.Components;
using CardCleaner.Tests.TestUtilities;
using CardCleaner.Tests.TestUtilities.Helpers;
using Godot;
using NSubstitute;
using static CardCleaner.Tests.Features.Packs.Components.ContainerOpeningWait;

namespace CardCleaner.Tests.Features.Packs.Components;

/// <summary>
///     A booster opened in the player's hand (<see cref="CardContainer.OpenInHand" />, through
///     <see cref="ContainerHolder.UseHeld" />): its cards are the ones opening it on the floor gives, they never meet
///     the world, the hand stays busy until they are in it, and they end up in the hand of cards after the cards it
///     already held, in the order they left the booster.
/// </summary>
[TestSuite]
[RequireGodotRuntime]
public class BoosterRevealTest
{
    private const string BoosterScene = "res://Scenes/Shop/Items/Booster.tscn";
    private const string BoxScene = "res://Scenes/Shop/Items/CardboardBox.tscn";
    private const ulong Seed = 0xB0057EEUL;

    private Node3D _world = null!;
    private Camera3D _hand = null!;
    private RecordingCardSpawner _spawner = null!;
    private CardHolder _cards = null!;
    private ContainerHolder _holder = null!;

    [Before]
    public static void SetupSuite() => TestServices.Reset();

    [BeforeTest]
    public void Setup()
    {
        _world = AddNode(new Node3D());
        _hand = new Camera3D { Name = "HandAnchor" };
        _world.AddChild(_hand);
        _spawner = new RecordingCardSpawner(buildBodies: true);
        TestServices.Container.RegisterSingleton(Substitute.For<IInputService>());

        _cards = TestServices.Inject(new CardHolder());
        _world.AddChild(_cards);
        _cards.SetReferences(_hand);
        _holder = TestServices.Inject(new ContainerHolder());
        _world.AddChild(_holder);
        _holder.SetReferences(_hand, _cards);
    }

    [AfterTest]
    public static void Cleanup() => TestServices.Reset();

    [TestCase]
    [TestCategory("Unit")]
    public async Task TheCardsGoIntoTheHandInTheOrderTheyLeftTheBooster()
    {
        var booster = HeldBooster();

        AssertBool(_holder.UseHeld()).IsTrue();
        await Until(() => !_holder.IsOpening, "the booster opening in the hand");

        AssertThat(_cards.HeldCount).IsEqual(8);
        AssertThat(_cards.HeldCards).ContainsExactly(_spawner.Bodies.ToArray());
        AssertBool(!GodotObject.IsInstanceValid(booster) || booster.IsQueuedForDeletion()).IsTrue();
        AssertBool(_holder.IsHolding).IsFalse();
    }

    // The same seed gives the same cards in the same order whether the booster is opened in the hand or on the floor.
    [TestCase]
    [TestCategory("Unit")]
    public void TheCardsAreTheOnesOpeningTheBoosterOnTheFloorGives()
    {
        var floorSpawner = new RecordingCardSpawner();
        var loose = GD.Load<PackedScene>(BoosterScene).Instantiate<CardContainer>();
        loose.ContentSeed = Seed;
        loose.Spawning = floorSpawner;
        _world.AddChild(loose);
        AssertBool(loose.Open()).IsTrue();
        loose.FinishOpening();

        HeldBooster();
        _holder.UseHeld();
        FinishTheOpeningBooster();

        AssertThat(_spawner.Spawned.Select(s => s.Signature).ToArray())
            .ContainsExactly(floorSpawner.Spawned.Select(s => s.Signature).ToArray());
    }

    [TestCase]
    [TestCategory("Unit")]
    public async Task TheCardsAlreadyHeldStayFirstAndTheWholeHandEndsFanned()
    {
        var held = new[] { LooseCard("Held0"), LooseCard("Held1") };
        foreach (var card in held)
            _cards.AddCard(card);
        HeldBooster();

        _holder.UseHeld();
        await Until(() => !_holder.IsOpening, "the booster opening in the hand");

        var hand = _cards.HeldCards;
        AssertThat(hand.Length).IsEqual(10);
        AssertThat(hand.Take(2).ToArray()).ContainsExactly(held);
        for (var i = 0; i < hand.Length; i++)
            AssertBool(hand[i].Transform.IsEqualApprox(_cards.FanPose(i, hand.Length)))
                .OverrideFailureMessage($"card {i} is at {hand[i].Transform}, not its fan pose")
                .IsTrue();
    }

    [TestCase]
    [TestCategory("Unit")]
    public async Task WhileTheBoosterOpensTheHandTakesNothingElse()
    {
        var booster = HeldBooster();
        var box = GD.Load<PackedScene>(BoxScene).Instantiate<CardContainer>();
        _world.AddChild(box);

        _holder.UseHeld();
        await Frames(2);

        AssertBool(_holder.IsOpening).IsTrue();
        AssertBool(_holder.IsHolding).IsTrue();
        AssertThat(_holder.Held).IsNull();
        AssertBool(_holder.TryPickUp(box)).IsFalse();
        AssertBool(_holder.Drop()).IsFalse();
        AssertBool(_holder.UseHeld()).IsFalse();
        AssertBool(booster.GetParent() == _hand).IsTrue();
        AssertBool(box.GetParent() == _world).IsTrue();

        FinishTheOpeningBooster();
    }

    // The cards hang in front of the camera, so none may collide with the world or fall while the booster opens.
    [TestCase]
    [TestCategory("Unit")]
    public async Task ARevealedCardIsFrozenWithItsCollisionOff()
    {
        HeldBooster();

        _holder.UseHeld();
        await BoundedWaitFor(() => _spawner.Bodies.Count >= 3, "three cards out of the booster");

        foreach (var card in _spawner.Bodies)
        {
            AssertBool(card.Freeze).IsTrue();
            AssertBool(card.GetNode<CollisionShape3D>("CardCollision").Disabled).IsTrue();
            AssertBool(card.GetParent() == _hand).IsTrue();
        }

        FinishTheOpeningBooster();
    }

    // A save finishes every opening first, so a booster opening in the hand is saved as the cards it holds.
    [TestCase]
    [TestCategory("Unit")]
    public async Task ASaveMidOpeningPutsEveryCardInTheHandAndStoresNoBooster()
    {
        HeldBooster();
        _holder.UseHeld();
        await Frames(2);

        SavedContainers.FinishOpenings(_world.GetTree());

        AssertThat(_cards.HeldCount).IsEqual(8);
        AssertBool(_holder.IsOpening).IsFalse();
        AssertThat(SavedContainers.Capture(_world.GetTree()).Count).IsEqual(0);
    }

    private CardContainer HeldBooster()
    {
        var booster = GD.Load<PackedScene>(BoosterScene).Instantiate<CardContainer>();
        booster.ContentSeed = Seed;
        booster.Spawning = _spawner;
        _world.AddChild(booster);
        AssertBool(_holder.TryPickUp(booster)).IsTrue();
        return booster;
    }

    private RigidBody3D LooseCard(string name)
    {
        var card = new RigidBody3D { Name = name };
        card.AddChild(new CollisionShape3D { Name = "CardCollision", Shape = new BoxShape3D() });
        _world.AddChild(card);
        return card;
    }

    // The booster opening in the hand is no longer Held, so it is found among the hand anchor's children.
    private void FinishTheOpeningBooster()
    {
        foreach (var container in _hand.GetChildren().OfType<CardContainer>())
            container.FinishOpening();
    }

    private static Task BoundedWaitFor(Func<bool> done, string what) => BoundedWait.Expect(done, what, BoundSeconds);
}
