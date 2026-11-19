using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using CardCleaner.Scripts.Core.Interfaces;
using CardCleaner.Scripts.Features.Card.Controllers;
using CardCleaner.Scripts.Features.Card.Models;
using CardCleaner.Scripts.Features.Packs.Components;
using CardCleaner.Scripts.Features.Packs.Models;
using CardCleaner.Scripts.Features.Shop.Components;
using CardCleaner.Tests.Features.Shop;
using CardCleaner.Tests.TestUtilities;
using Godot;
using NSubstitute;

namespace CardCleaner.Tests.Features.Packs.Components;

/// <summary>
///     A cardboard box as storage: standing open on the floor it takes in what lands on top of it, a shut one takes in
///     nothing, items come out into the slot that takes them or onto the floor where the player aims, and an emptied
///     box in hand goes into a trash can.
/// </summary>
[TestSuite]
[RequireGodotRuntime]
public class CartonStorageTest
{
    private const string BoxScene = "res://Scenes/Shop/Items/CardboardBox.tscn";
    private const string BoosterScene = "res://Scenes/Shop/Items/Booster.tscn";
    private const string PackScene = "res://Scenes/Shop/Items/Pack.tscn";
    private const string CardShelfScene = "res://Scenes/Shop/Items/Shelf.tscn";
    private const string ProductShelfScene = "res://Scenes/Shop/Items/ProductShelf.tscn";
    private const string TrashCanScene = "res://Scenes/Shop/Items/TrashCan.tscn";
    private const ulong Seed = 777;
    private const int FallFrames = 120;

    private Node3D _world = null!;
    private CardBuilder _cards = null!;

    [Before]
    public static void SetupSuite()
    {
        TestServices.Reset();
    }

    [BeforeTest]
    public void Setup()
    {
        TestServices.Reset();
        _world = AddNode(new Node3D());
        _cards = new CardBuilder();

        // Loose items go under the card spawner's node, so they are freed with the world.
        var spawner = new Mocking.MockCardSpawner { Name = "Spawner" };
        _world.AddChild(spawner);
        TestServices.Container.RegisterSingleton<ICardSpawner>(spawner);

        var floor = new StaticBody3D { Name = "Floor", Position = new Vector3(0f, -0.5f, 0f) };
        floor.AddChild(new CollisionShape3D { Shape = new BoxShape3D { Size = new Vector3(40f, 1f, 40f) } });
        _world.AddChild(floor);
    }

    [AfterTest]
    public static void Teardown()
    {
        TestServices.Reset();
    }

    [TestCase]
    [TestCategory("Unit")]
    public async Task AnOpenBoxOnTheFloorTakesInABoosterDroppedOnIt()
    {
        var box = StandingBox([]);
        box.SetLidOpen(true, instantly: true);
        var booster = Dropped(BoosterScene, new Vector3(0f, 1.2f, 0f));
        var seed = booster.SeedForSave();

        var caught = await Until(() => !GodotObject.IsInstanceValid(booster) || booster.IsQueuedForDeletion());

        AssertBool(caught).IsTrue();
        AssertThat(box.Remaining).IsEqual(1);
        AssertThat(StoredItemKeys.Of(box.Storage!.Items))
            .IsEqual(StoredItemKeys.Of([new StoredContainer(BoosterScene, CardContainerKind.Booster, seed, null)]));
    }

    [TestCase]
    [TestCategory("Unit")]
    public async Task AShutBoxLeavesABoosterDroppedOnItLyingOnTop()
    {
        var box = StandingBox([]);
        var booster = Dropped(BoosterScene, new Vector3(0f, 1.2f, 0f));

        await Until(() => false, FallFrames);

        AssertBool(GodotObject.IsInstanceValid(booster) && !booster.IsQueuedForDeletion()).IsTrue();
        AssertThat(box.Remaining).IsEqual(0);
        AssertThat(booster.GlobalPosition.Y).IsGreater(0.5f);
    }

    [TestCase]
    [TestCategory("Unit")]
    public void ACardPutInKeepsItsSignatureLookAndPrice()
    {
        var box = StandingBox([]);
        var signature = new CardSignature([0.5f, 0f, 0f, -0.5f, 0f, 0f, 0.25f, 0f]);
        var card = ShopTestCards.Create(signature);
        card.LookSeed = 31;
        card.ShopPrice = 9;
        _world.AddChild(card);

        AssertBool(box.Storage!.TryPut(card)).IsTrue();

        AssertBool(card.IsQueuedForDeletion()).IsTrue();
        AssertThat(StoredItemKeys.Of(box.Storage.Items))
            .IsEqual(StoredItemKeys.Of([new StoredCard(signature, 31, 9)]));
    }

    // The card went in first and the booster last, so each slot takes the last item it can take, not the last item.
    [TestCase]
    [TestCategory("Unit")]
    public void AMixedBoxFillsACardShelfSlotWithTheCardAndAProductSlotWithTheBooster()
    {
        var signature = new CardSignature([0f, 0.75f, 0f, 0f, 0f, 0f, 0f, 0f]);
        var box = StandingBox(
        [
            new StoredCard(signature, 5, null),
            new StoredContainer(BoosterScene, CardContainerKind.Booster, Seed, 6),
        ]);
        var cardSlot = Shelf(CardShelfScene, new Vector3(0f, 0f, -5f)).Slots.First();
        var productSlot = Shelf(ProductShelfScene, new Vector3(5f, 0f, -5f)).Slots.First();

        AssertBool(box.Storage!.TakeInto(cardSlot)).IsTrue();
        AssertBool(box.Storage.TakeInto(productSlot)).IsTrue();

        var card = (CardController)cardSlot.Cards.Single();
        AssertThat(card.Signature.Elements).IsEqual(signature.Elements);
        AssertThat(card.LookSeed).IsEqual(5);
        var booster = (CardContainer)productSlot.Cards.Single();
        AssertThat(booster.Kind).IsEqual(CardContainerKind.Booster);
        AssertThat(booster.ContentSeed).IsEqual(Seed);
        AssertThat(booster.ShopPrice).IsEqual(6);
        AssertThat(box.Remaining).IsEqual(0);
        AssertBool(box.Storage.TakeInto(cardSlot)).IsFalse();
    }

    [TestCase]
    [TestCategory("Unit")]
    public async Task PutOneDownSetsTheNextBoosterBoxOnTheFloorWhereThePlayerAims()
    {
        var (holder, eye) = Holder(new Vector3(0f, 1.6f, 0f));
        eye.LookAt(new Vector3(0f, 0f, -2f));
        var box = StandingBox(null);
        AssertBool(holder.TryPickUp(box)).IsTrue();
        box.SetLidOpen(true, instantly: true);
        await ISceneRunner.SyncPhysicsFrame;

        AssertBool(holder.PutOneDown()).IsTrue();

        AssertThat(box.Remaining).IsEqual(CardContainerLayout.ItemsPerContainer - 1);
        var put = _world.GetChildren().OfType<Mocking.MockCardSpawner>().Single().GetChildren()
            .OfType<CardContainer>().Single();
        AssertThat(put.Kind).IsEqual(CardContainerKind.Pack);
        AssertThat(put.ContentSeed).IsEqual(ContainerSeeds.ChildSeed(Seed, 0));
        AssertThat(new Vector2(put.GlobalPosition.X, put.GlobalPosition.Z).DistanceTo(new Vector2(0f, -2f)))
            .IsLess(0.01f);
    }

    [TestCase]
    [TestCategory("Unit")]
    public async Task PutOneDownWithNothingUnderTheAimTakesNothing()
    {
        var (holder, eye) = Holder(new Vector3(0f, 1.6f, 0f));
        eye.LookAt(new Vector3(0f, 5f, -2f));
        var box = StandingBox(null);
        AssertBool(holder.TryPickUp(box)).IsTrue();
        box.SetLidOpen(true, instantly: true);
        await ISceneRunner.SyncPhysicsFrame;

        AssertBool(holder.PutOneDown()).IsFalse();

        AssertThat(box.Remaining).IsEqual(CardContainerLayout.ItemsPerContainer);
    }

    [TestCase]
    [TestCategory("Unit")]
    public void ATrashCanTakesOnlyAnEmptyBoxFromTheHand()
    {
        var (holder, _) = Holder(Vector3.Zero);
        var bin = GD.Load<PackedScene>(TrashCanScene).Instantiate<TrashCan>();
        _world.AddChild(bin);
        AssertBool(bin.CanInteract).IsFalse();

        var full = StandingBox(null);
        AssertBool(holder.TryPickUp(full)).IsTrue();
        AssertBool(bin.CanInteract).IsFalse();
        AssertBool(holder.Drop()).IsTrue();

        var empty = StandingBox([]);
        AssertBool(holder.TryPickUp(empty)).IsTrue();
        AssertBool(bin.CanInteract).IsTrue();
        bin.Interact();

        AssertBool(holder.IsHolding).IsFalse();
        AssertBool(!GodotObject.IsInstanceValid(empty) || empty.IsQueuedForDeletion()).IsTrue();
        AssertBool(GodotObject.IsInstanceValid(full) && !full.IsQueuedForDeletion()).IsTrue();
    }

    // The booster went in last, so taking one out takes its look away and leaves the card stack.
    [TestCase]
    [TestCategory("Unit")]
    public void AnOpenBoxShowsWhatItHoldsInsideItsWalls()
    {
        var box = StandingBox([
            new StoredCard(null, 3, null),
            new StoredCard(null, 4, null),
            new StoredContainer(PackScene, CardContainerKind.Pack, 1, null),
            new StoredContainer(BoosterScene, CardContainerKind.Booster, 2, null),
        ]);
        box.SetLidOpen(true, instantly: true);

        var shown = ShownMeshes(box);
        AssertThat(shown.Count).IsGreater(0);
        foreach (var mesh in shown)
        {
            var bounds = box.GlobalTransform.AffineInverse() * mesh.GlobalTransform * mesh.GetAabb();
            AssertBool(InsideTheWalls(bounds))
                .OverrideFailureMessage($"{mesh.Name} spans {bounds}, outside the box's inside")
                .IsTrue();
        }

        AssertBool(box.Storage!.TakeOnto(new Transform3D(Basis.Identity, new Vector3(3f, 0f, 0f)))).IsTrue();
        AssertThat(ShownMeshes(box).Count).IsLess(shown.Count);
    }

    // What the box's contents view draws.
    private static List<MeshInstance3D> ShownMeshes(CardContainer box) =>
        box.Storage!.GetNode("Contents").FindChildren("*", nameof(MeshInstance3D), owned: false)
            .OfType<MeshInstance3D>().ToList();

    // Within the box's inside, in its own frame (origin at the middle of its base), give or take 2 mm.
    private static bool InsideTheWalls(Aabb bounds)
    {
        const float tolerance = 0.002f;
        var half = CartonSpace.Interior / 2f;
        var min = new Vector3(-half.X, CartonSpace.Floor, -half.Z) - Vector3.One * tolerance;
        var max = new Vector3(half.X, CartonSpace.Floor + CartonSpace.Interior.Y, half.Z) + Vector3.One * tolerance;
        return bounds.Position.X >= min.X && bounds.Position.Y >= min.Y && bounds.Position.Z >= min.Z
               && bounds.End.X <= max.X && bounds.End.Y <= max.Y && bounds.End.Z <= max.Z;
    }

    // A seeded box standing frozen at the origin, holding items when they are given (a fresh box when null).
    private CardContainer StandingBox(IReadOnlyList<StoredItem>? items)
    {
        var box = GD.Load<PackedScene>(BoxScene).Instantiate<CardContainer>();
        box.ContentSeed = Seed;
        box.Spawning = _cards;
        box.Freeze = true;
        box.Storage!.Restore(items);
        _world.AddChild(box);
        return box;
    }

    private CardContainer Dropped(string scenePath, Vector3 at)
    {
        var container = GD.Load<PackedScene>(scenePath).Instantiate<CardContainer>();
        container.ContentSeed = Seed + 1;
        _world.AddChild(container);
        container.GlobalPosition = at;
        container.Freeze = false;
        return container;
    }

    private CardShelf Shelf(string scenePath, Vector3 at)
    {
        var shelf = GD.Load<PackedScene>(scenePath).Instantiate<CardShelf>();
        shelf.Position = at;
        _world.AddChild(shelf);
        return shelf;
    }

    // A holder whose hand anchor is a camera at eye, the way the player's camera provides one.
    private (ContainerHolder Holder, Camera3D Eye) Holder(Vector3 eye)
    {
        TestServices.Container.RegisterSingleton(Substitute.For<IInputService>());
        var camera = new Camera3D { Name = "Eye" };
        _world.AddChild(camera);
        camera.GlobalPosition = eye;
        var holder = TestServices.Inject(new ContainerHolder());
        _world.AddChild(holder);
        holder.SetReferences(camera);
        return (holder, camera);
    }

    private static async Task<bool> Until(Func<bool> done, int frames = FallFrames)
    {
        for (var i = 0; i < frames; i++)
        {
            if (done())
                return true;
            await ISceneRunner.SyncPhysicsFrame;
        }

        return done();
    }

    // Builds real card bodies for items taken out of a box, with the signature and look the box kept.
    private sealed class CardBuilder : ICardSpawningService
    {
        public Node3D? SpawnCard(CardSignature signature, Transform3D spawnTransform, Node3D parent) =>
            SpawnCard(signature, null, spawnTransform, parent);

        public Node3D? SpawnCard(CardSignature signature, int? lookSeed, Transform3D spawnTransform, Node3D parent)
        {
            var card = ShopTestCards.Create(signature);
            card.LookSeed = lookSeed;
            parent.AddChild(card);
            card.GlobalTransform = spawnTransform;
            return card;
        }

        public Node3D? SpawnRandomCard(Transform3D spawnTransform, Node3D parent) => null;

        public Vector3 GetRandomOffset(Vector3 offsetRange) => Vector3.Zero;
    }
}
