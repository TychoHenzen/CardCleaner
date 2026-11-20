using System;
using System.Linq;
using System.Threading.Tasks;
using CardCleaner.Scripts.Core.Interfaces;
using CardCleaner.Scripts.Features.Card.Controllers;
using CardCleaner.Scripts.Features.Hand.Components;
using CardCleaner.Scripts.Features.Card.Services.SaveGame;
using CardCleaner.Scripts.Features.Packs.Components;
using CardCleaner.Scripts.Features.Packs.Services;
using CardCleaner.Scripts.Core.Randomness;
using CardCleaner.Tests.TestUtilities;
using CardCleaner.Tests.TestUtilities.Helpers;
using Godot;
using NSubstitute;
using static CardCleaner.Tests.Features.Packs.Components.ContainerOpeningWait;

namespace CardCleaner.Tests.Features.Packs.Components;

/// <summary>
///     A booster opened in the player's hand (<see cref="CardContainer.OpenInHand" />, through
///     <see cref="IHand.UseFront" />): its cards are the ones its seed gives, they never meet the world, the
///     hand stays busy until they are in it, and they end up in the hand of cards after the cards it already held, in
///     the order they left the booster.
/// </summary>
[TestSuite]
[RequireGodotRuntime]
public class BoosterRevealTest
{
    private const string BoosterScene = "res://Scenes/Shop/Items/Booster.tscn";
    private const string BoxScene = "res://Scenes/Shop/Items/CardboardBox.tscn";
    private const ulong Seed = 0xB0057EEUL;

    private Node3D _world = null!;
    private Camera3D _anchor = null!;
    private RecordingCardSpawner _spawner = null!;
    private Scripts.Features.Hand.Components.Hand _hand = null!;

    [Before]
    public static void SetupSuite() => TestServices.Reset();

    [BeforeTest]
    public void Setup()
    {
        _world = AddNode(new Node3D());
        _anchor = new Camera3D { Name = "HandAnchor" };
        _world.AddChild(_anchor);
        _spawner = new RecordingCardSpawner(buildBodies: true);
        TestServices.Container.RegisterSingleton(Substitute.For<IInputService>());

        _hand = TestServices.Inject(new Scripts.Features.Hand.Components.Hand());
        _world.AddChild(_hand);
        _hand.SetReferences(_anchor);
    }

    [AfterTest]
    public static void Cleanup() => TestServices.Reset();

    [TestCase]
    public async Task TheCardsGoIntoTheHandInTheOrderTheyLeftTheBooster()
    {
        var booster = HeldBooster();

        AssertBool(_hand.UseFront()).IsTrue();
        await Until(() => !_hand.IsOpening, "the booster opening in the hand");

        AssertThat(_hand.Cards.Count).IsEqual(8);
        AssertThat(_hand.Cards.ToArray()).ContainsExactly(_spawner.Bodies.ToArray());
        AssertBool(!GodotObject.IsInstanceValid(booster) || booster.IsQueuedForDeletion()).IsTrue();
        AssertThat(_hand.Count).IsEqual(8);
    }

    // The booster's seed decides its cards and their order.
    [TestCase]
    public void TheCardsAreTheOnesTheBoostersSeedGives()
    {
        HeldBooster();
        _hand.UseFront();
        FinishTheOpeningBooster();

        var expected = new CardPackGenerator(RandomSources.Create(Seed)).OpenBooster();
        AssertThat(_spawner.Spawned.Select(s => s.Signature).ToArray()).ContainsExactly(expected);
    }

    [TestCase]
    public async Task TheCardsAlreadyHeldStayFirstAndTheWholeHandEndsFanned()
    {
        var held = new[] { LooseCard("Held0"), LooseCard("Held1") };
        foreach (var card in held)
            _hand.TryTake(card);
        HeldBooster();

        _hand.UseFront();
        await Until(() => !_hand.IsOpening, "the booster opening in the hand");

        var hand = _hand.Items;
        AssertThat(hand.Count).IsEqual(10);
        AssertThat(hand.Take(2).ToArray()).ContainsExactly(held);
        for (var i = 0; i < hand.Count; i++)
            AssertBool(hand[i].Transform.IsEqualApprox(_hand.PoseOf(i, hand.Count)))
                .OverrideFailureMessage($"card {i} is at {hand[i].Transform}, not its fan pose")
                .IsTrue();
    }

    [TestCase]
    public async Task WhileTheBoosterOpensTheHandTakesNothingElse()
    {
        var booster = HeldBooster();
        var box = GD.Load<PackedScene>(BoxScene).Instantiate<CardContainer>();
        _world.AddChild(box);

        _hand.UseFront();
        await Frames(2);

        AssertBool(_hand.IsOpening).IsTrue();
        AssertThat(_hand.Front).IsNull();
        AssertThat(_hand.Room).IsEqual(0);
        AssertBool(_hand.TryTake(box)).IsFalse();
        AssertBool(_hand.PutFrontDown()).IsFalse();
        AssertBool(_hand.UseFront()).IsFalse();
        AssertBool(booster.GetParent() == _anchor).IsTrue();
        AssertBool(box.GetParent() == _world).IsTrue();

        FinishTheOpeningBooster();
    }

    // The cards hang in front of the camera, so none may collide with the world or fall while the booster opens.
    [TestCase]
    public async Task ARevealedCardIsFrozenWithItsCollisionOff()
    {
        HeldBooster();

        _hand.UseFront();
        await BoundedWaitFor(() => _spawner.Bodies.Count >= 3, "three cards out of the booster");

        foreach (var card in _spawner.Bodies)
        {
            AssertBool(card.Freeze).IsTrue();
            AssertBool(card.GetNode<CollisionShape3D>("CardCollision").Disabled).IsTrue();
            AssertBool(card.GetParent() == _anchor).IsTrue();
        }

        FinishTheOpeningBooster();
    }

    // A save finishes every opening first, so a booster opening in the hand is saved as the cards it holds.
    [TestCase]
    public async Task ASaveMidOpeningPutsEveryCardInTheHandAndStoresNoBooster()
    {
        HeldBooster();
        _hand.UseFront();
        await Frames(2);

        SavedContainers.FinishOpenings(_world.GetTree());

        AssertThat(_hand.Cards.Count).IsEqual(8);
        AssertBool(_hand.IsOpening).IsFalse();
        AssertThat(SavedContainers.Capture(_world.GetTree()).Count).IsEqual(0);
    }

    private CardContainer HeldBooster()
    {
        var booster = GD.Load<PackedScene>(BoosterScene).Instantiate<CardContainer>();
        booster.ContentSeed = Seed;
        booster.Spawning = _spawner;
        _world.AddChild(booster);
        AssertBool(_hand.TryTake(booster)).IsTrue();
        // Only the front item is used, so the booster comes to the front.
        _hand.SetFront(_hand.Count - 1);
        return booster;
    }

    private CardController LooseCard(string name)
    {
        var card = new CardController { Name = name };
        card.AddChild(new CollisionShape3D { Name = "CardCollision", Shape = new BoxShape3D() });
        _world.AddChild(card);
        return card;
    }

    // The booster opening in the hand is no longer Held, so it is found among the hand anchor's children.
    private void FinishTheOpeningBooster()
    {
        foreach (var container in _anchor.GetChildren().OfType<CardContainer>())
            container.FinishOpening();
    }

    private static Task BoundedWaitFor(Func<bool> done, string what) => BoundedWait.Expect(done, what, BoundSeconds);
}
