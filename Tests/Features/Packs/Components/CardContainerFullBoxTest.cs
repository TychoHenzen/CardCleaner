using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using CardCleaner.Scripts.Features.Card.Models;
using CardCleaner.Scripts.Features.Packs.Components;
using CardCleaner.Scripts.Features.Packs.Models;
using CardCleaner.Scripts.Features.Packs.Services;
using CardCleaner.Scripts.Core.Randomness;
using Godot;
using static CardCleaner.Tests.Features.Packs.Components.ContainerOpeningWait;

namespace CardCleaner.Tests.Features.Packs.Components;

/// <summary>
///     Opens a whole booster box through its boosters down to the 64 cards. A cardboard box is storage and never opens
///     whole, so the chain starts one level down.
/// </summary>
[TestSuite]
[RequireGodotRuntime]
public class CardContainerFullBoxTest
{
    private const string PackScene = "res://Scenes/Shop/Items/Pack.tscn";
    private const int CardsPerPack = CardContainerLayout.ItemsPerContainer * CardContainerLayout.ItemsPerContainer;
    private const float StorageWallHeight = 3.75f;
    private const ulong Seed = 777;
    private const int SettleFrames = 180;
    private const float WallThickness = 0.2f;

    // Two items of one generation (packs, boosters or cards) spawning closer than this share a spot.
    private const float SamePositionTolerance = 0.001f;

    // A synthetic 8 x 8 m storage room and a delivery marker inside it, modelled on the shop's storage block; it is not
    // read from ShopScene.
    private static readonly Rect2 StorageRoom = new(0f, -8f, 8f, 8f);
    private static readonly Vector3 StorageDelivery = new(4f, 0f, -5.5f);

    private readonly List<Vector3> _boosterSpawns = [];
    private Node3D _world = null!;
    private RecordingCardSpawner _spawner = null!;

    [BeforeTest]
    public void Setup()
    {
        _world = AddNode(new Node3D());
        _spawner = new RecordingCardSpawner();
        _boosterSpawns.Clear();
    }

    [TestCase]
    [TestCategory("Unit")]
    public async Task AWholeBoosterBoxOpensToSixtyFourCards()
    {
        await OpenEverything(Make(PackScene));

        AssertThat(_spawner.Spawned.Count).IsEqual(CardsPerPack);
    }

    [TestCase]
    [TestCategory("Unit")]
    public async Task SameSeedPutsSpecialCardsInTheSamePositionsAndCommonCardsAreAllZero()
    {
        await OpenEverything(Make(PackScene));
        var first = _spawner.Spawned.Select(s => s.Signature).ToArray();

        _spawner = new RecordingCardSpawner();
        await OpenEverything(Make(PackScene));
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
    public async Task SpawnedCardSignaturesMatchTheGeneratorOutput()
    {
        await OpenEverything(Make(PackScene));
        var expected = new CardPackGenerator(RandomSources.Create(Seed)).OpenBox().Take(CardsPerPack);
        var spawned = _spawner.Spawned.Select(s => s.Signature).ToArray();

        // Spawn order is not generation order, so compare the multiset of all 64 signatures.
        AssertThat(spawned.Select(SignatureKey).OrderBy(k => k).ToArray())
            .IsEqual(expected.Select(SignatureKey).OrderBy(k => k).ToArray());
    }

    private static string SignatureKey(CardSignature signature) => string.Join(" ", signature.Elements);

    [TestCase]
    [TestCategory("Unit")]
    public async Task AFullBoosterBoxOpenedAtTheStorageDeliveryPointSpawnsEveryItemInsideTheRoomOnItsOwnSpot()
    {
        AddStorageRoom();
        var pack = Make(PackScene);
        pack.GlobalPosition = StorageDelivery;

        // Opens the real booster box and every booster. Each level lands on the floor before the next is opened, as
        // in the shop where the player opens an item only after it has come to rest. The test checks where each
        // opened container puts its items: inside the room, and no two of one generation on one spot.
        await OpenEverything(pack, SettleFrames);

        var cards = _spawner.Spawned.Select(spawned => spawned.Transform.Origin).ToList();
        AssertThat(_boosterSpawns.Count).IsEqual(CardContainerLayout.ItemsPerContainer);
        AssertThat(cards.Count).IsEqual(CardsPerPack);

        var outside = _boosterSpawns.Concat(cards)
            .Where(p => !StorageRoom.HasPoint(new Vector2(p.X, p.Z)) || p.Y >= StorageWallHeight)
            .ToList();
        AssertThat(string.Join(", ", outside)).IsEmpty();

        // Boosters and cards never exist at the same time, so only items of one generation can overlap.
        AssertThat(string.Join(", ", SharedSpots(_boosterSpawns))).IsEmpty();
        AssertThat(string.Join(", ", SharedSpots(cards))).IsEmpty();
    }

    [TestCase]
    [TestCategory("Unit")]
    public static void APackAndABoosterAtTheSameSpotAreNotASharedSpawnButTwoPacksAre()
    {
        var spot = new Vector3(4f, 0.5f, -5.5f);
        List<Vector3> packs = [spot];
        List<Vector3> boosters = [spot];

        AssertThat(SharedSpots(packs).Count).IsEqual(0);
        AssertThat(SharedSpots(boosters).Count).IsEqual(0);
        AssertThat(SharedSpots(packs.Concat(boosters)).Count).IsEqual(1);
        AssertThat(SharedSpots([spot, spot]).Count).IsEqual(1);
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

    // Where the items of this container spawn: the spot it settles on when it is opened plus the per-item offset.
    private static void RecordSpawns(CardContainer container, List<Vector3> into)
    {
        var settled = ContainerOpening.SettledPose(container, container.CollisionMask).Origin;
        for (var i = 0; i < CardContainerLayout.ItemsPerContainer; i++)
            into.Add(settled + CardContainer.SpawnOffset(i, container.Kind));
    }

    private static List<Vector3> SharedSpots(IEnumerable<Vector3> positions) =>
        positions.GroupBy(p => (Mathf.RoundToInt(p.X / SamePositionTolerance),
                Mathf.RoundToInt(p.Y / SamePositionTolerance), Mathf.RoundToInt(p.Z / SamePositionTolerance)))
            .Where(group => group.Count() > 1)
            .Select(group => group.First())
            .ToList();

    // The storage room as the shop has it, a floor and four walls, so a pack that rolls on landing stays in the
    // room as it does in the shop; without walls where it settles is up to the physics engine run by run.
    private static void AddStorageRoom()
    {
        var room = AddNode(new StaticBody3D());
        var centre = StorageRoom.GetCenter();
        var halfWall = WallThickness / 2f;
        var width = StorageRoom.Size.X + 2f * WallThickness;
        var depth = StorageRoom.Size.Y + 2f * WallThickness;
        var wallY = StorageWallHeight / 2f;

        AddBox(room, new Vector3(centre.X, -0.5f, centre.Y), new Vector3(width, 1f, depth));
        AddBox(room, new Vector3(StorageRoom.Position.X - halfWall, wallY, centre.Y),
            new Vector3(WallThickness, StorageWallHeight, depth));
        AddBox(room, new Vector3(StorageRoom.End.X + halfWall, wallY, centre.Y),
            new Vector3(WallThickness, StorageWallHeight, depth));
        AddBox(room, new Vector3(centre.X, wallY, StorageRoom.Position.Y - halfWall),
            new Vector3(width, StorageWallHeight, WallThickness));
        AddBox(room, new Vector3(centre.X, wallY, StorageRoom.End.Y + halfWall),
            new Vector3(width, StorageWallHeight, WallThickness));
    }

    private static void AddBox(StaticBody3D body, Vector3 centre, Vector3 size)
    {
        body.AddChild(new CollisionShape3D { Shape = new BoxShape3D { Size = size }, Position = centre });
    }

    /// <summary>
    ///     Opens the booster box, then every booster, each level all at once, optionally letting each level settle
    ///     before the next is opened.
    /// </summary>
    private async Task OpenEverything(CardContainer pack, int settleFrames = 0)
    {
        var expected = 1;
        foreach (var kind in new[] { CardContainerKind.Pack, CardContainerKind.Booster })
        {
            CardContainer[] level = kind == CardContainerKind.Pack ? [pack] : Containers(kind);
            AssertThat(level.Length).IsEqual(expected);
            foreach (var container in level)
            {
                if (kind == CardContainerKind.Pack)
                    RecordSpawns(container, _boosterSpawns);
                container.Open();
            }

            await Until(() => level.All(c => !GodotObject.IsInstanceValid(c) || c.IsQueuedForDeletion()),
                $"opening every {kind}");
            await PhysicsFrames(settleFrames);
            expected *= CardContainerLayout.ItemsPerContainer;
        }
    }

    private static async Task PhysicsFrames(int count)
    {
        for (var i = 0; i < count; i++)
            await ISceneRunner.SyncPhysicsFrame;
    }
}
