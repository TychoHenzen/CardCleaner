using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using CardCleaner.Scripts.Features.Packs.Components;
using CardCleaner.Scripts.Features.Packs.Models;
using CardCleaner.Scripts.Features.Packs.Services;
using Godot;

namespace CardCleaner.Tests.Features.Packs.Components;

[TestSuite]
[RequireGodotRuntime]
public class CardContainerTest
{
    private const string BoxScene = "res://Scenes/Shop/Items/CardboardBox.tscn";
    private const string PackScene = "res://Scenes/Shop/Items/Pack.tscn";
    private const string BoosterScene = "res://Scenes/Shop/Items/Booster.tscn";
    private const string CardScene = "res://Scenes/Components/CardShader.tscn";
    private const float StorageWallHeight = 3.75f;
    private const ulong Seed = 777;

    // The storage room of ShopScene (x 0..8, z -8..0) and the box delivery marker inside it.
    private static readonly Rect2 StorageRoom = new(0f, -8f, 8f, 8f);
    private static readonly Vector3 StorageDelivery = new(4f, 0f, -5.5f);
    private const int GenerousFrameBudget = 200;
    private const int OpenFrames = 12;

    private Node3D _world = null!;
    private RecordingCardSpawner _spawner = null!;

    [BeforeTest]
    public void Setup()
    {
        _world = AddNode(new Node3D());
        _spawner = new RecordingCardSpawner();
    }

    [TestCase]
    [TestCategory("Unit")]
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
    [TestCategory("Unit")]
    public async Task BoxOpensIntoEightPacksOneAfterAnother()
    {
        var box = Make(BoxScene);

        AssertBool(box.Open()).IsTrue();
        AssertThat(box.PendingCount).IsEqual(8);
        await Frames(1);
        AssertThat(Containers(CardContainerKind.Pack).Length).IsLessEqual(1);
        await Frames(OpenFrames);

        AssertThat(Containers(CardContainerKind.Pack).Length).IsEqual(8);
        AssertBool(GodotObject.IsInstanceValid(box)).IsFalse();
    }

    [TestCase]
    [TestCategory("Unit")]
    public async Task PackOpensIntoEightBoosters()
    {
        var pack = Make(PackScene);

        pack.Open();
        await Frames(OpenFrames);

        AssertThat(Containers(CardContainerKind.Booster).Length).IsEqual(8);
    }

    [TestCase]
    [TestCategory("Unit")]
    public async Task BoosterOpensIntoEightCardsWithGeneratedSignatures()
    {
        var booster = Make(BoosterScene);

        booster.Open();
        await Frames(OpenFrames);

        AssertThat(_spawner.Spawned.Count).IsEqual(8);
        var expected = new CardPackGenerator(new RandomNumberGenerator { Seed = Seed }).OpenBooster();
        for (var i = 0; i < expected.Length; i++)
            AssertThat(_spawner.Spawned[i].Signature.Elements).IsEqual(expected[i].Elements);
    }

    [TestCase]
    [TestCategory("Unit")]
    public async Task WholeBoxOpensToFiveHundredAndTwelveCards()
    {
        await OpenEverything(Make(BoxScene));

        AssertThat(_spawner.Spawned.Count).IsEqual(CardContainerLayout.CardsPerBox);
    }

    [TestCase]
    [TestCategory("Unit")]
    public async Task SameSeedPutsSpecialCardsInTheSamePositionsAndCommonCardsAreAllZero()
    {
        await OpenEverything(Make(BoxScene));
        var first = _spawner.Spawned.Select(s => s.Signature).ToArray();

        _spawner = new RecordingCardSpawner();
        await OpenEverything(Make(BoxScene));
        var second = _spawner.Spawned.Select(s => s.Signature).ToArray();

        AssertThat(second.Length).IsEqual(first.Length);
        for (var i = 0; i < first.Length; i++)
        {
            AssertThat(second[i].HasMagicalPotential()).IsEqual(first[i].HasMagicalPotential());
            if (!first[i].HasMagicalPotential())
                AssertThat(first[i].Elements).IsEqual(new float[8]);
        }
    }

    [TestCase]
    [TestCategory("Unit")]
    public void ContainerCanOnlyBeOpenedOnce()
    {
        var booster = Make(BoosterScene);

        AssertBool(booster.Open()).IsTrue();

        AssertBool(booster.CanInteract).IsFalse();
        AssertBool(booster.Open()).IsFalse();
        AssertThat(booster.PendingCount).IsEqual(8);
    }

    [TestCase]
    [TestCategory("Unit")]
    public void BoosterWithoutACardSpawnerCannotOpen()
    {
        var booster = Make(BoosterScene);
        booster.Spawning = null;

        AssertBool(booster.CanInteract).IsFalse();
        AssertBool(booster.Open()).IsFalse();
    }

    [TestCase]
    [TestCategory("Unit")]
    public void OpeningDisablesCollisionSoTheEmptyShellCannotBeInteractedWithAgain()
    {
        var box = Make(BoxScene);

        box.Interact();

        AssertThat(box.CollisionLayer).IsEqual(0u);
        AssertBool(box.Visible).IsFalse();
    }

    [TestCase]
    [TestCategory("Unit")]
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
    [TestCategory("Unit")]
    public async Task OpeningABoosterSpawnsAtMostOneCardPerFrame()
    {
        var booster = Make(BoosterScene);
        booster.Open();

        var previous = 0;
        for (var frame = 0; frame < OpenFrames; frame++)
        {
            await Frames(1);
            AssertBool(_spawner.Spawned.Count - previous <= 1).IsTrue();
            previous = _spawner.Spawned.Count;
        }

        AssertThat(previous).IsEqual(CardContainerLayout.ItemsPerContainer);
    }

    [TestCase]
    [TestCategory("Unit")]
    public async Task FreeingTheParentWhileOpeningStopsSpawningWithoutErrors()
    {
        var booster = Make(BoosterScene);
        booster.Open();
        await Frames(2);
        var spawnedSoFar = _spawner.Spawned.Count;

        _world.Free();
        await Frames(OpenFrames);

        AssertThat(_spawner.Spawned.Count).IsEqual(spawnedSoFar);
    }

    [TestCase]
    [TestCategory("Unit")]
    public static void OpenedItemsStackInAColumnStraightAboveTheContainer()
    {
        foreach (var kind in new[] { CardContainerKind.Box, CardContainerKind.Pack, CardContainerKind.Booster })
        {
            var offsets = Enumerable.Range(0, CardContainerLayout.ItemsPerContainer).Select(i => CardContainer.SpawnOffset(i, kind)).ToArray();

            AssertThat(offsets[0].Y).IsEqual(CardContainer.LiftHeight);
            foreach (var offset in offsets)
                AssertBool(offset.X == 0f && offset.Z == 0f).IsTrue();
            for (var i = 1; i < offsets.Length; i++)
                AssertThat(offsets[i].Y - offsets[i - 1].Y).IsEqualApprox(CardContainer.StackStep(kind), 0.0001f);
        }
    }

    [TestCase]
    [TestCategory("Unit")]
    public static void EachStepClearsTheHeightOfTheItemBelowSoNoneSpawnsInsideAnother()
    {
        var children = new Dictionary<CardContainerKind, string>
        {
            [CardContainerKind.Box] = PackScene,
            [CardContainerKind.Pack] = BoosterScene,
            [CardContainerKind.Booster] = CardScene
        };

        foreach (var (kind, childScene) in children)
        {
            var child = GD.Load<PackedScene>(childScene).Instantiate<Node3D>();
            var collider = child.FindChildren("*", nameof(CollisionShape3D), true, false).OfType<CollisionShape3D>().First();
            var height = ((BoxShape3D)collider.Shape).Size.Y;
            child.Free();

            AssertBool(CardContainer.StackStep(kind) > height).IsTrue();
        }
    }

    [TestCase]
    [TestCategory("Unit")]
    public static void AFullBoxOpenedAtTheStorageDeliveryPointStaysInsideTheStorageRoom()
    {
        // Every item rests where it spawned before it is opened, so each level's column starts on the floor
        // under the one before. The tallest column must stay under the storage walls.
        var positions = new List<Vector3>();
        var level = new List<Vector3> { StorageDelivery };
        foreach (var kind in new[] { CardContainerKind.Box, CardContainerKind.Pack, CardContainerKind.Booster })
        {
            var next = new List<Vector3>();
            foreach (var container in level)
            for (var i = 0; i < CardContainerLayout.ItemsPerContainer; i++)
                next.Add(container + CardContainer.SpawnOffset(i, kind));
            positions.AddRange(next);
            level = next.Select(p => new Vector3(p.X, StorageDelivery.Y, p.Z)).ToList();
        }

        AssertThat(positions.Count).IsEqual(8 + 64 + CardContainerLayout.CardsPerBox);
        foreach (var position in positions)
        {
            AssertBool(StorageRoom.HasPoint(new Vector2(position.X, position.Z))).IsTrue();
            AssertBool(position.Y < StorageWallHeight).IsTrue();
        }
    }

    private CardContainer Make(string scenePath)
    {
        var container = GD.Load<PackedScene>(scenePath).Instantiate<CardContainer>();
        container.Generator = new CardPackGenerator(new RandomNumberGenerator { Seed = Seed });
        container.Spawning = _spawner;
        _world.AddChild(container);
        return container;
    }

    private CardContainer[] Containers(CardContainerKind kind) =>
        _world.GetChildren().OfType<CardContainer>().Where(c => c.Kind == kind && !c.IsOpened).ToArray();

    private async Task OpenEverything(CardContainer box)
    {
        box.Open();
        var expected = CardContainerLayout.ItemsPerContainer;
        foreach (var kind in new[] { CardContainerKind.Pack, CardContainerKind.Booster })
        {
            await Frames(OpenFrames);
            var next = Containers(kind);
            AssertThat(next.Length).IsEqual(expected);
            foreach (var container in next)
                container.Open();
            expected *= CardContainerLayout.ItemsPerContainer;
        }

        var budget = GenerousFrameBudget;
        while (_spawner.Spawned.Count < CardContainerLayout.CardsPerBox && budget-- > 0)
            await Frames(1);
    }

    private static async Task Frames(int count)
    {
        for (var i = 0; i < count; i++)
            await ISceneRunner.SyncProcessFrame;
    }
}
