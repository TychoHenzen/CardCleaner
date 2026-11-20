using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using CardCleaner.Scripts.Core.Interfaces;
using CardCleaner.Scripts.Features.Card.Components;
using CardCleaner.Scripts.Features.Card.Controllers;
using CardCleaner.Scripts.Features.Card.Models;
using CardCleaner.Scripts.Features.Card.Services;
using CardCleaner.Scripts.Features.CardStacks.Components;
using CardCleaner.Scripts.Features.CardStacks.Models;
using CardCleaner.Scripts.Features.Packs.Models;
using CardCleaner.Tests.Features.Card.Services;
using CardCleaner.Tests.Features.Shop;
using CardCleaner.Tests.Mocking;
using CardCleaner.Tests.TestUtilities;
using CardCleaner.Tests.TestUtilities.Helpers;
using GdUnit4;
using Godot;
using NSubstitute;

namespace CardCleaner.Tests.Features.CardStacks.Components;

/// <summary>
///     A pile takes in a free card that touches it on the next physics frame, without waiting for it to rest, and lays
///     each card at a pose fixed when it joined: a join or a take moves no card the pile already shows. A card held
///     under the camera stays out of it.
/// </summary>
[TestSuite]
[RequireGodotRuntime]
public class CardPileTest
{
    // Loose cards lie on layer 2 and collide with the world on layer 1, as Scenes/Components/CardShader.tscn sets.
    private const uint CardLayer = 2;
    private const uint WorldLayer = 1;

    // Half a second of physics frames at the default 60 a second.
    private const int HalfSecond = 30;

    private const int PileCount = 100;

    private Node3D _world = null!;

    [BeforeTest]
    public void Setup()
    {
        TestServices.Reset();
        _world = AddNode(new Node3D());
        var floor = new StaticBody3D { Name = "Floor", CollisionLayer = WorldLayer };
        floor.AddChild(new CollisionShape3D { Shape = new BoxShape3D { Size = new Vector3(20f, 0.2f, 20f) } });
        _world.AddChild(floor);
        floor.Position = new Vector3(0f, -0.1f, 0f);
    }

    [AfterTest]
    public static void Cleanup()
    {
        TestServices.Reset();
        CardEffectNormalMapCache.ResetForTesting();
    }

    // A card dropped from a little above the pile lands on it and joins it, all within half a second of its release.
    [TestCase]
    public async Task ADroppedCardJoinsThePileWithinHalfASecondOfLanding()
    {
        var pile = AddPile();
        var card = DropCard(new Vector3(0.1f, CardBundleRules.PileHeight(PileCount) + 0.3f, 0f), lookSeed: 900);

        var frames = 0;
        while (IsLoose(card) && frames < HalfSecond)
        {
            await ISceneRunner.SyncPhysicsFrame;
            frames++;
        }

        AssertBool(IsLoose(card)).OverrideFailureMessage($"still loose after {frames} physics frames").IsFalse();
        AssertThat(pile.Count).IsEqual(PileCount + 1);
        AssertThat(pile.Cards[^1].LookSeed).IsEqual(900);
        await BoundedWait.ForQueuedFrees();
    }

    // The player's own drop: a card let go from the hand while it already hangs inside the pile's join area (the player
    // stands over the pile) joins it within half a second, though it never crossed into the area from outside.
    [TestCase]
    public async Task ACardLetGoFromTheHandOverThePileJoinsIt()
    {
        var pile = AddPile();
        var anchor = new Camera3D { Name = "HandAnchor" };
        _world.AddChild(anchor);
        var spawner = new MockCardSpawner { Name = "MockSpawner" };
        _world.AddChild(spawner);
        TestServices.Container.RegisterSingleton<ICardSpawner>(spawner);
        TestServices.Container.RegisterSingleton(Substitute.For<IInputService>());
        // Named in full: Hand alone would find the test namespace CardCleaner.Tests.Features.Hand first.
        var hand = TestServices.Inject(new Scripts.Features.Hand.Components.Hand());
        _world.AddChild(hand);
        hand.SetReferences(anchor);
        var card = DropCard(new Vector3(5f, 1f, 5f), lookSeed: 901);
        AssertBool(hand.TryTake(card)).IsTrue();
        // Move the camera so the held card hangs just above the top of the dome, inside the join area.
        var target = new Vector3(0f, CardBundleRules.PileHeight(PileCount) + 0.02f, 0f);
        anchor.GlobalPosition += target - card.GlobalPosition;
        await ISceneRunner.SyncPhysicsFrame;
        AssertThat(pile.Count).IsEqual(PileCount);

        AssertBool(hand.ReleaseCard(card)).IsTrue();

        var frames = 0;
        while (IsLoose(card) && frames < HalfSecond)
        {
            await ISceneRunner.SyncPhysicsFrame;
            frames++;
        }

        AssertBool(IsLoose(card)).OverrideFailureMessage($"still loose after {frames} physics frames").IsFalse();
        AssertThat(pile.Cards[^1].LookSeed).IsEqual(901);
        await BoundedWait.ForQueuedFrees();
    }

    // A card held under the camera (a hand) that reaches into the pile stays where it is.
    [TestCase]
    public async Task ACardHeldUnderTheCameraDoesNotJoin()
    {
        var pile = AddPile();
        var eye = new Camera3D { Name = "Eye" };
        _world.AddChild(eye);
        var held = ShopTestCards.Create(null);
        held.CollisionLayer = CardLayer;
        held.Freeze = true;
        eye.AddChild(held);
        held.GlobalPosition = new Vector3(0f, CardBundleRules.PileHeight(PileCount), 0f);

        for (var i = 0; i < 3; i++)
            await ISceneRunner.SyncPhysicsFrame;

        AssertBool(held.IsInsideTree()).IsTrue();
        AssertThat(pile.Count).IsEqual(PileCount);
    }

    // Each card on a pile shows its own face, dressed the way a loose card's is: the top cards show more than one art,
    // and a face shows the art, name and flavor that a face dressed alone from the same record shows.
    [TestCase]
    public async Task ManyDifferentFacesShowOnAPile()
    {
        var generator = SignatureCardTestData.CreateGeneratorWithBaseTypes(4, out _);
        TestServices.Container.RegisterSingleton<ICardGenerator>(generator);
        var pile = AddPile();
        await BakeFrames();

        var faces = pile.ShownCards;
        AssertThat(faces.Count).IsEqual(PileLook.FacesShown);
        AssertThat(faces.Values.Select(ArtOf).Distinct().Count()).IsGreater(1);

        foreach (var (index, face) in faces.Where(entry => entry.Key % 8 == 0))
        {
            var alone = AddNode(GD.Load<PackedScene>(CardFace.ScenePath).Instantiate<CardFace>());
            alone.Dress(generator, new CardSignature(), pile.Cards[index].LookSeed);
            await BakeFrames();

            AssertObject(ArtOf(face)).OverrideFailureMessage($"card {index} shows another art").IsSame(ArtOf(alone));
            AssertThat(face.Renderer.NameLabel.Text).IsEqual(alone.Renderer.NameLabel.Text);
            AssertThat(face.Renderer.FlavorLabel.Text).IsEqual(alone.Renderer.FlavorLabel.Text);
        }
    }

    // Only the newest cards are drawn, and a join adds one node and keeps every other card's node where it was.
    [TestCase]
    public void AJoinMovesNoCardAlreadyShown()
    {
        var pile = AddPile();
        var before = Snapshot(pile);
        AssertThat(before.Count).IsEqual(PileLook.FacesShown);

        AssertBool(pile.Put(new StoredCard(null, 900, null))).IsTrue();

        var after = Snapshot(pile);
        AssertThat(after.Count).IsEqual(PileLook.FacesShown);
        AssertBool(after.ContainsKey(PileCount)).IsTrue();
        foreach (var (index, (node, pose)) in before.Where(entry => entry.Key > PileCount - PileLook.FacesShown))
        {
            AssertObject(after[index].Node).IsSame(node);
            AssertThat(after[index].Pose).IsEqual(pose);
        }

        AssertBool(after.ContainsKey(PileCount - PileLook.FacesShown)).IsFalse();
    }

    // A take frees the top card's node only; the card that comes back among the newest gets one, and no other moves.
    [TestCase]
    public void ATakeRemovesTheTopCardAndMovesNoOther()
    {
        var pile = AddPile();
        var before = Snapshot(pile);

        AssertThat(((StoredCard)pile.TakeLast()!).LookSeed).IsEqual(PileCount - 1);

        var after = Snapshot(pile);
        AssertBool(GodotObject.IsInstanceValid(before[PileCount - 1].Node)).IsFalse();
        AssertBool(after.ContainsKey(PileCount - 1)).IsFalse();
        AssertBool(after.ContainsKey(PileCount - 1 - PileLook.FacesShown)).IsTrue();
        foreach (var (index, (node, pose)) in before.Where(entry => entry.Key < PileCount - 1))
        {
            AssertObject(after[index].Node).IsSame(node);
            AssertThat(after[index].Pose).IsEqual(pose);
        }
    }

    // A pile made again from the same cards and seed (as a reload makes it) shows the same heap.
    [TestCase]
    public void TheSameCardsAndSeedShowTheSameHeap()
    {
        var pile = AddPile();
        var again = CardBundle.Create(CardBundleKind.Pile, pile.Cards, pile.PileSeed);
        _world.AddChild(again);

        AssertThat(Poses(again)).IsEqual(Poses(pile));
    }

    private CardBundle AddPile()
    {
        var pile = CardBundle.Create(CardBundleKind.Pile,
            Enumerable.Range(0, PileCount).Select(seed => new StoredCard(null, seed, null)));
        _world.AddChild(pile);
        // The pile stays where it is made, so a card landing on it cannot shove it about.
        pile.Freeze = true;
        return pile;
    }

    private CardController DropCard(Vector3 at, int lookSeed)
    {
        var card = ShopTestCards.Create(new CardSignature([0, 0.5f, 0, 0, 0, 0, 0, 0]));
        card.LookSeed = lookSeed;
        card.CollisionLayer = CardLayer;
        card.CollisionMask = WorldLayer | CardLayer;
        _world.AddChild(card);
        card.GlobalPosition = at;
        return card;
    }

    // A joined card leaves the tree and is freed, so its wrapper may already be disposed.
    private static bool IsLoose(CardController card) => GodotObject.IsInstanceValid(card) && card.IsInsideTree();

    // A face's material is applied a frame after it is dressed (CardShaderRenderer bakes deferred).
    private static async Task BakeFrames()
    {
        var tree = (SceneTree)Engine.GetMainLoop();
        for (var i = 0; i < 2; i++)
            await tree.ToSignal(tree, SceneTree.SignalName.ProcessFrame);
    }

    // The art texture a face's material draws: its layer at the art layer's index in the template.
    private static Texture2D? ArtOf(CardFace face)
    {
        var template = new CardTemplate();
        var artIndex = System.Array.IndexOf(template.GatherAllLayers(), template.Art);
        var material = face.GetNode<MeshInstance3D>("OuterBox_Baked").MaterialOverride as ShaderMaterial;
        var textures = material?.GetShaderParameter("textures").AsGodotArray<Texture2D>();
        return textures is { Count: > 0 } ? textures[artIndex] : null;
    }

    private static Dictionary<int, (CardFace Node, Transform3D Pose)> Snapshot(CardBundle pile) =>
        pile.ShownCards.ToDictionary(entry => entry.Key, entry => (entry.Value, entry.Value.Transform));

    private static Transform3D[] Poses(CardBundle pile) =>
        pile.ShownCards.OrderBy(entry => entry.Key).Select(entry => entry.Value.Transform).ToArray();
}
