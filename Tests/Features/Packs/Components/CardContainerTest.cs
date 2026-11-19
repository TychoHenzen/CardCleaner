using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using CardCleaner.Scripts.Features.Packs.Components;
using CardCleaner.Scripts.Features.Packs.Models;
using CardCleaner.Scripts.Features.Packs.Services;
using CardCleaner.Scripts.Core.Randomness;
using Godot;
using static CardCleaner.Tests.Features.Packs.Components.ContainerOpeningWait;

namespace CardCleaner.Tests.Features.Packs.Components;

[TestSuite]
[RequireGodotRuntime]
public class CardContainerTest
{
    private const string BoxScene = "res://Scenes/Shop/Items/CardboardBox.tscn";
    private const string PackScene = "res://Scenes/Shop/Items/Pack.tscn";
    private const string BoosterScene = "res://Scenes/Shop/Items/Booster.tscn";
    private const string CardScene = "res://Scenes/Components/CardShader.tscn";
    private const ulong Seed = 777;

    // The shop's ceiling height (Scenes/Shop/ShopRoof.tscn).
    private const float ShopCeiling = 3.5f;

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
    public void Ready_AddsToContainersGroup()
    {
        var box = Make(BoxScene);

        AssertBool(box.IsInGroup("Containers")).IsTrue();
    }

    [TestCase]
    [TestCategory("Unit")]
    public async Task PackOpensIntoEightBoostersOneAfterAnother()
    {
        var pack = Make(PackScene);

        AssertBool(pack.Open()).IsTrue();
        AssertThat(pack.PendingCount).IsEqual(8);
        await Frames(1);
        AssertThat(Containers(CardContainerKind.Booster).Length).IsLessEqual(1);
        await UntilOpened(pack);

        AssertThat(Containers(CardContainerKind.Booster).Length).IsEqual(8);
        AssertBool(GodotObject.IsInstanceValid(pack)).IsFalse();
    }

    [TestCase]
    [TestCategory("Unit")]
    public async Task BoosterOpensIntoEightCardsWithGeneratedSignatures()
    {
        var booster = Make(BoosterScene);

        booster.Open();
        await UntilOpened(booster);

        AssertThat(_spawner.Spawned.Count).IsEqual(8);
        var expected = new CardPackGenerator(RandomSources.Create(Seed)).OpenBooster();
        for (var i = 0; i < expected.Length; i++)
            AssertThat(_spawner.Spawned[i].Signature.Elements).IsEqual(expected[i].Elements);
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
    public void AnOpeningContainerStaysInViewButCannotBeHitOrAimedAtAgain()
    {
        var pack = Make(PackScene);

        pack.Open();

        AssertThat(pack.CollisionLayer).IsEqual(0u);
        AssertThat(pack.CollisionMask).IsEqual(0u);
        AssertBool(pack.Freeze).IsTrue();
        AssertBool(pack.Visible).IsTrue();
    }

    [TestCase]
    [TestCategory("Unit")]
    public void Interact_EmitsPickUpRequested_AndDoesNotOpen()
    {
        var box = Make(BoxScene);
        var received = new List<CardContainer>();
        box.Connect(CardContainer.SignalName.PickUpRequested, Callable.From<CardContainer>(received.Add));

        box.Interact();

        AssertBool(box.IsOpened).IsFalse();
        AssertThat(received.Count).IsEqual(1);
        AssertBool(received[0] == box).IsTrue();
    }

    [TestCase]
    [TestCategory("Unit")]
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
    [TestCategory("Unit")]
    public async Task FreeingTheParentWhileOpeningStopsSpawningWithoutErrors()
    {
        var booster = Make(BoosterScene);
        booster.Open();
        await Until(() => _spawner.Spawned.Count > 0, "the first card leaving the booster");
        var spawnedSoFar = _spawner.Spawned.Count;

        _world.Free();
        await For(1.5);

        AssertThat(_spawner.Spawned.Count).IsEqual(spawnedSoFar);
    }

    [TestCase]
    [TestCategory("Unit")]
    public async Task AContainerOpenedInTheAirFallsToTheFloorFirst()
    {
        AddFloor();
        var pack = Make(PackScene);
        pack.Position = new Vector3(0f, 1.6f, 0f);
        await PhysicsFrames(2);

        pack.Open();
        await Until(() => pack.SpawnedCount > 0, "the first booster leaving the pack");

        AssertThat(pack.GlobalPosition.Y).IsEqualApprox(0f, 0.001f);
        var booster = Containers(CardContainerKind.Booster).Single();
        // The booster rises out of the pack toward its spot above where the pack landed, not above where it was opened.
        await UntilOpened(pack);
        AssertThat(booster.GlobalPosition.Y).IsLess(1f);
    }

    [TestCase]
    [TestCategory("Unit")]
    public async Task ThePackLidSwingsOpenBeforeTheBoostersRiseAndTheEmptyPackShrinksAway()
    {
        var pack = Make(PackScene);
        var lid = pack.GetNode<OpeningPart>("Visual/Lid");
        var visual = pack.GetNode<Node3D>("Visual");
        var closed = lid.Basis;

        pack.Open();
        await Until(() => pack.SpawnedCount > 0, "the first booster leaving the pack");
        var swung = Mathf.RadToDeg(closed.GetRotationQuaternion().AngleTo(lid.Basis.GetRotationQuaternion()));
        AssertThat(swung).IsEqualApprox(115f, 1f);

        var smallest = 1f;
        await Until(() =>
        {
            if (!GodotObject.IsInstanceValid(pack) || pack.IsQueuedForDeletion())
                return true;

            smallest = Mathf.Min(smallest, visual.Scale.X);
            return false;
        }, "opening the pack");
        AssertThat(smallest).IsLess(0.5f);
    }

    [TestCase]
    [TestCategory("Unit")]
    public async Task ItemsHangFrozenAtTheirSpotsUntilTheWrapperIsGoneThenDrop()
    {
        var pack = Make(PackScene);

        pack.Open();
        await Until(() => pack.SpawnedCount == CardContainerLayout.ItemsPerContainer,
            "every booster leaving the pack");
        var boosters = Containers(CardContainerKind.Booster);
        AssertBool(boosters.All(b => b.Freeze)).IsTrue();

        await UntilOpened(pack);
        AssertBool(boosters.All(b => !b.Freeze)).IsTrue();
        var spots = Enumerable.Range(0, boosters.Length)
            .Select(i => CardContainer.SpawnOffset(i, CardContainerKind.Pack)).ToHashSet();
        foreach (var booster in boosters)
            AssertBool(spots.Any(spot => spot.DistanceTo(booster.GlobalPosition) < 0.05f)).IsTrue();
    }

    [TestCase]
    [TestCategory("Unit")]
    public void FinishOpeningPutsEveryItemAtItsSpotAtOnce()
    {
        var pack = Make(PackScene);
        pack.Open();

        pack.FinishOpening();

        AssertThat(pack.PendingCount).IsEqual(0);
        AssertBool(pack.IsQueuedForDeletion()).IsTrue();
        var boosters = Containers(CardContainerKind.Booster);
        AssertThat(boosters.Length).IsEqual(8);
        AssertBool(boosters.All(b => !b.Freeze)).IsTrue();
        for (var i = 0; i < boosters.Length; i++)
        {
            var spot = CardContainer.SpawnOffset(i, CardContainerKind.Pack);
            AssertBool(boosters.Any(b => b.GlobalPosition.DistanceTo(spot) < 0.001f)).IsTrue();
        }
    }

    [TestCase]
    [TestCategory("Unit")]
    public void FinishOpeningDoesNothingToAContainerThatIsNotOpening()
    {
        var pack = Make(PackScene);

        pack.FinishOpening();

        AssertBool(pack.IsOpened).IsFalse();
        AssertBool(pack.IsQueuedForDeletion()).IsFalse();
        AssertThat(Containers(CardContainerKind.Booster).Length).IsEqual(0);
    }

    [TestCase]
    [TestCategory("Unit")]
    public static void PacksAndBoostersOpenIntoOneColumnStraightAboveTheContainer()
    {
        foreach (var kind in new[] { CardContainerKind.Pack, CardContainerKind.Booster })
        {
            var offsets = Offsets(kind);

            AssertThat(offsets[0].Y).IsEqual(CardContainer.LiftHeight(kind));
            foreach (var offset in offsets)
                AssertBool(offset.X == 0f && offset.Z == 0f).IsTrue();
            for (var i = 1; i < offsets.Length; i++)
                AssertThat(offsets[i].Y - offsets[i - 1].Y).IsEqualApprox(CardContainer.StackStep(kind), 0.0001f);
        }
    }

    [TestCase]
    [TestCategory("Unit")]
    public static void NoItemSpawnsInsideTheContainerOrAnotherItem()
    {
        foreach (var (kind, scene, childScene) in Chain())
        {
            var own = ColliderOf(scene);
            var child = ColliderOf(childScene);
            var offsets = Offsets(kind);

            AssertBool(CardContainer.LiftHeight(kind) > own.Top).IsTrue();
            AssertBool(CardContainer.StackStep(kind) > child.Height).IsTrue();
            var xs = offsets.Select(o => o.X).Distinct().OrderBy(x => x).ToArray();
            for (var i = 1; i < xs.Length; i++)
                AssertBool(xs[i] - xs[i - 1] > child.Width).IsTrue();
        }
    }

    [TestCase]
    [TestCategory("Unit")]
    public static void TheHighestItemOfAContainerOnTheFloorStaysBelowTheShopCeiling()
    {
        foreach (var (kind, _, childScene) in Chain())
        {
            var top = Offsets(kind).Max(o => o.Y) + ColliderOf(childScene).Height;

            AssertThat(top).IsLess(ShopCeiling);
        }
    }

    private static Vector3[] Offsets(CardContainerKind kind) =>
        Enumerable.Range(0, CardContainerLayout.ItemsPerContainer)
            .Select(i => CardContainer.SpawnOffset(i, kind)).ToArray();

    private static (CardContainerKind Kind, string Scene, string ChildScene)[] Chain() =>
    [
        (CardContainerKind.Pack, PackScene, BoosterScene),
        (CardContainerKind.Booster, BoosterScene, CardScene)
    ];

    // The size of the first box collider of a scene, and the height of its top above the scene's origin.
    private static (float Width, float Height, float Top) ColliderOf(string scene)
    {
        var node = GD.Load<PackedScene>(scene).Instantiate<Node3D>();
        var collider = node.FindChildren("*", nameof(CollisionShape3D), true, false).OfType<CollisionShape3D>()
            .First();
        var size = ((BoxShape3D)collider.Shape).Size;
        var top = collider.Position.Y + size.Y / 2f;
        node.Free();
        return (size.X, size.Y, top);
    }

    private void AddFloor()
    {
        var floor = new StaticBody3D();
        floor.AddChild(new CollisionShape3D { Shape = new BoxShape3D { Size = new Vector3(10f, 0.2f, 10f) } });
        _world.AddChild(floor);
        floor.Position = new Vector3(0f, -0.1f, 0f);
    }

    private CardContainer Make(string scenePath)
    {
        var container = GD.Load<PackedScene>(scenePath).Instantiate<CardContainer>();
        container.Generator = new CardPackGenerator(RandomSources.Create(Seed));
        container.Spawning = _spawner;
        _world.AddChild(container);
        return container;
    }

    private CardContainer[] Containers(CardContainerKind kind) =>
        _world.GetChildren().OfType<CardContainer>().Where(c => c.Kind == kind && !c.IsOpened).ToArray();

    private static async Task PhysicsFrames(int count)
    {
        for (var i = 0; i < count; i++)
            await Engine.GetMainLoop().ToSignal(Engine.GetMainLoop(), SceneTree.SignalName.PhysicsFrame);
    }
}
