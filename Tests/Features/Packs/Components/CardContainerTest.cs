using CardCleaner.Scripts.Features.Hand.Components;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using CardCleaner.Scripts.Features.Card.Components;
using CardCleaner.Scripts.Features.Card.Models;
using CardCleaner.Scripts.Features.Packs.Components;
using CardCleaner.Scripts.Features.Packs.Models;
using CardCleaner.Scripts.Features.Packs.Services;
using CardCleaner.Scripts.Core.Randomness;
using CardCleaner.Tests.TestUtilities.Helpers;
using Godot;
using NSubstitute;
using static CardCleaner.Tests.Features.Packs.Components.ContainerOpeningWait;

namespace CardCleaner.Tests.Features.Packs.Components;

[TestSuite]
[RequireGodotRuntime]
public class CardContainerTest
{
    private const string BoxScene = "res://Scenes/Shop/Items/CardboardBox.tscn";
    private const string PackScene = "res://Scenes/Shop/Items/Pack.tscn";
    private const string BoosterScene = "res://Scenes/Shop/Items/Booster.tscn";
    private const ulong Seed = 777;

    private Node3D _world = null!;
    private Camera3D _hand = null!;
    private RecordingCardSpawner _spawner = null!;

    // Several cases check the opening stage by stage (the lid swings before the boosters rise, items hang until the
    // wrapper is gone), so a frame must span little game time; at the default factor of 16 they skipped stages.
    private const int StageByStageFactor = 4;

    private FastClock? _clock;

    [BeforeTest]
    public void Setup()
    {
        _clock = FastClock.Start(StageByStageFactor);
        _world = AddNode(new Node3D());
        _hand = InHandOpening.AddHand(_world);
        _spawner = new RecordingCardSpawner();
    }

    [AfterTest]
    public void StopClock()
    {
        _clock?.Dispose();
    }

    [TestCase]
    public void ScenesAreTheMatchingKindsAndChainBoxToPackToBooster()
    {
        var box = Make(BoxScene);
        var pack = Make(PackScene);
        var booster = Make(BoosterScene);

        AssertThat(box.Kind).IsEqual(CardContainerKind.Box);
        AssertThat(pack.Kind).IsEqual(CardContainerKind.Pack);
        AssertThat(booster.Kind).IsEqual(CardContainerKind.Booster);
        AssertThat(box.ChildScene!.ResourcePath).IsEqual(PackScene);
        AssertThat(pack.ChildScene!.ResourcePath).IsEqual(BoosterScene);
        AssertThat(booster.ChildScene).IsNull();
    }

    [TestCase]
    public void Ready_AddsToContainersGroup()
    {
        var box = Make(BoxScene);

        AssertBool(box.IsInGroup("Containers")).IsTrue();
    }

    [TestCase]
    public void ABoosterBoxOnTheFloorDoesNotEmpty()
    {
        var pack = Make(PackScene);

        AssertThat(pack.EmptyIntoHand().Count).IsEqual(0);

        AssertBool(pack.IsOpened).IsFalse();
        AssertBool(pack.GetParent() == _world).IsTrue();
    }

    [TestCase]
    public void ABoosterOpensInTheHandIntoEightCardsWithGeneratedSignatures()
    {
        var booster = Make(BoosterScene);

        InHandOpening.Open(booster, _hand);

        AssertThat(_spawner.Spawned.Count).IsEqual(8);
        var expected = new CardPackGenerator(RandomSources.Create(Seed)).OpenBooster();
        for (var i = 0; i < expected.Length; i++)
            AssertThat(_spawner.Spawned[i].Signature.Elements).IsEqual(expected[i].Elements);
    }

    // The boosters share the booster box's generator, so opening them one after another in the hand gives the
    // generator's cards in the order it rolls them.
    [TestCase]
    public async Task AWholeBoosterBoxOpensInTheHandToTheGeneratorsFirstSixtyFourCards()
    {
        foreach (var booster in InHandOpening.Empty(Make(PackScene), _hand))
            InHandOpening.Open(booster, _hand);

        const int perPack = CardContainerLayout.ItemsPerContainer * CardContainerLayout.ItemsPerContainer;
        var expected = new CardPackGenerator(RandomSources.Create(Seed)).OpenBox().Take(perPack);
        AssertThat(Keys(_spawner.Spawned.Select(spawned => spawned.Signature))).IsEqual(Keys(expected));
        await BoundedWait.ForQueuedFrees();
    }

    [TestCase]
    public void ABoosterOpensOnlyOnce()
    {
        var booster = Make(BoosterScene);

        InHandOpening.Begin(booster, _hand);

        AssertBool(booster.IsOpened).IsTrue();
        AssertBool(booster.OpenInHand(Substitute.For<IHand>())).IsFalse();
        booster.FinishOpening();
        AssertThat(_spawner.Spawned.Count).IsEqual(CardContainerLayout.ItemsPerContainer);
    }

    [TestCase]
    public void BoosterWithoutACardSpawnerCannotOpen()
    {
        var booster = Make(BoosterScene);
        booster.Spawning = null;

        AssertBool(booster.CanInteract).IsFalse();
        InHandOpening.Hold(booster, _hand);
        AssertBool(booster.OpenInHand(Substitute.For<IHand>())).IsFalse();
        AssertBool(booster.IsOpened).IsFalse();
    }

    [TestCase]
    // A click on a container is the hand router's (CardHighlighter); on its own it never opens or moves the box.
    public void Interact_OnItsOwn_NeitherOpensNorMovesTheBox()
    {
        var box = Make(BoxScene);
        var parent = box.GetParent();

        box.Interact();

        AssertBool(box.IsOpened).IsFalse();
        AssertObject(box.GetParent()).IsSame(parent);
    }

    [TestCase]
    public void CanInteract_FalseWhenParentIsCamera()
    {
        var box = Make(BoxScene);
        AssertBool(box.CanInteract).IsTrue();

        var camera = AddNode(new Camera3D());
        box.Reparent(camera);

        AssertBool(box.IsHeld).IsTrue();
        AssertBool(box.CanInteract).IsFalse();
    }

    [TestCase]
    public void ContainersJoinTheInteractionLayerAndHighlightOnLook()
    {
        foreach (var path in new[] { BoxScene, PackScene, BoosterScene })
        {
            var container = Make(path);

            AssertThat(container.CollisionLayer & 4u).IsEqual(4u);
            AssertBool(container.HighlightMesh!.Visible).IsFalse();
            container.Highlight();
            AssertBool(container.HighlightMesh.Visible).IsTrue();
            container.ClearHighlight();
            AssertBool(container.HighlightMesh.Visible).IsFalse();
        }
    }

    [TestCase]
    public async Task OpeningABoosterSpawnsAtMostOneCardPerFrame()
    {
        var booster = Make(BoosterScene);
        InHandOpening.Begin(booster, _hand);

        var previous = 0;
        var stepsAtOnce = 0;
        await Until(() =>
        {
            stepsAtOnce = System.Math.Max(stepsAtOnce, _spawner.Spawned.Count - previous);
            previous = _spawner.Spawned.Count;
            return !GodotObject.IsInstanceValid(booster) || booster.IsQueuedForDeletion();
        }, "opening the booster");

        AssertThat(stepsAtOnce).IsLessEqual(1);
        AssertThat(_spawner.Spawned.Count).IsEqual(CardContainerLayout.ItemsPerContainer);
    }

    [TestCase]
    public async Task FreeingTheHandWhileABoosterOpensStopsSpawningWithoutErrors()
    {
        var booster = Make(BoosterScene);
        InHandOpening.Begin(booster, _hand);
        await Until(() => _spawner.Spawned.Count > 0, "the first card leaving the booster");
        var spawnedSoFar = _spawner.Spawned.Count;

        _world.Free();
        await For(1.5);

        AssertThat(_spawner.Spawned.Count).IsEqual(spawnedSoFar);
    }

    [TestCase]
    public void FinishOpeningDoesNothingToAContainerThatIsNotOpening()
    {
        var pack = Make(PackScene);

        pack.FinishOpening();

        AssertBool(pack.IsOpened).IsFalse();
        AssertBool(pack.IsQueuedForDeletion()).IsFalse();
        AssertThat(_world.GetChildren().OfType<CardContainer>().Count()).IsEqual(1);
    }

    private static string Keys(IEnumerable<CardSignature> signatures) =>
        string.Join(";", signatures.Select(signature => string.Join(",", signature.Elements)));

    private CardContainer Make(string scenePath)
    {
        var container = GD.Load<PackedScene>(scenePath).Instantiate<CardContainer>();
        container.Generator = new CardPackGenerator(RandomSources.Create(Seed));
        container.Spawning = _spawner;
        _world.AddChild(container);
        return container;
    }
}
