using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using CardCleaner.Scripts.Features.Packs.Components;
using CardCleaner.Scripts.Features.Packs.Models;
using CardCleaner.Scripts.Features.Packs.Services;
using Godot;

namespace CardCleaner.Tests.Features.Packs.Components;

/// <summary>Opens a whole cardboard box through its packs and boosters down to the 512 cards.</summary>
[TestSuite]
[RequireGodotRuntime]
public class CardContainerFullBoxTest
{
    private const string BoxScene = "res://Scenes/Shop/Items/CardboardBox.tscn";
    private const float StorageWallHeight = 3.75f;
    private const ulong Seed = 777;
    private const int GenerousFrameBudget = 200;
    private const int OpenFrames = 12;
    private const int SettleFrames = 180;

    // Two spawn positions closer than this count as the same spot (items would start inside each other).
    private const float SamePositionTolerance = 0.001f;

    // The storage room of ShopScene (x 0..8, z -8..0) and the box delivery marker inside it.
    private static readonly Rect2 StorageRoom = new(0f, -8f, 8f, 8f);
    private static readonly Vector3 StorageDelivery = new(4f, 0f, -5.5f);

    private readonly List<Vector3> _containerSpawns = [];
    private Node3D _world = null!;
    private RecordingCardSpawner _spawner = null!;

    [BeforeTest]
    public void Setup()
    {
        _world = AddNode(new Node3D());
        _spawner = new RecordingCardSpawner();
        _containerSpawns.Clear();
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
    public async Task AFullBoxOpenedAtTheStorageDeliveryPointSpawnsEveryItemInsideTheStorageRoomOnItsOwnSpot()
    {
        AddFloor();
        var box = Make(BoxScene);
        box.GlobalPosition = StorageDelivery;

        // Opens the real box, every pack and every booster. Each level lands on the floor before the next is
        // opened, as in the shop where the player opens an item only after it has come to rest.
        await OpenEverything(box, SettleFrames);

        var positions = _containerSpawns.Concat(_spawner.Spawned.Select(spawned => spawned.Transform.Origin)).ToList();
        var itemsPerContainer = CardContainerLayout.ItemsPerContainer;
        AssertThat(positions.Count).IsEqual(itemsPerContainer + itemsPerContainer * itemsPerContainer
                                            + CardContainerLayout.CardsPerBox);
        var outside = positions
            .Where(p => !StorageRoom.HasPoint(new Vector2(p.X, p.Z)) || p.Y >= StorageWallHeight)
            .ToList();
        AssertThat(string.Join(", ", outside)).IsEmpty();
        var shared = positions.GroupBy(p => (Mathf.RoundToInt(p.X / SamePositionTolerance),
                Mathf.RoundToInt(p.Y / SamePositionTolerance), Mathf.RoundToInt(p.Z / SamePositionTolerance)))
            .Where(group => group.Count() > 1)
            .Select(group => group.First())
            .ToList();
        AssertThat(string.Join(", ", shared)).IsEmpty();
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

    private void AddFloor()
    {
        var floor = AddNode(new StaticBody3D());
        floor.AddChild(new CollisionShape3D
        {
            Shape = new BoxShape3D { Size = new Vector3(40f, 1f, 40f) },
            Position = new Vector3(StorageDelivery.X, -0.5f, StorageDelivery.Z)
        });
    }

    /// <summary>Opens the box, then every pack and booster, optionally letting each level settle first.</summary>
    private async Task OpenEverything(CardContainer box, int settleFrames = 0)
    {
        box.Open();
        var expected = CardContainerLayout.ItemsPerContainer;
        foreach (var kind in new[] { CardContainerKind.Pack, CardContainerKind.Booster })
        {
            await Frames(OpenFrames);
            await PhysicsFrames(settleFrames);
            var next = Containers(kind);
            AssertThat(next.Length).IsEqual(expected);
            _containerSpawns.AddRange(next.Select(container => container.GlobalPosition));
            foreach (var container in next)
                container.Open();
            expected *= CardContainerLayout.ItemsPerContainer;
        }

        var budget = GenerousFrameBudget;
        while (_spawner.Spawned.Count < CardContainerLayout.CardsPerBox && budget-- > 0)
            await Frames(1);
    }

    private static async Task PhysicsFrames(int count)
    {
        for (var i = 0; i < count; i++)
            await ISceneRunner.SyncPhysicsFrame;
    }

    private static async Task Frames(int count)
    {
        for (var i = 0; i < count; i++)
            await ISceneRunner.SyncProcessFrame;
    }
}
