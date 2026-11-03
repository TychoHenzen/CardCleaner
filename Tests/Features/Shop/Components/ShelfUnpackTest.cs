using System.Linq;
using System.Threading.Tasks;
using CardCleaner.Scripts.Features.Card.Components;
using CardCleaner.Scripts.Features.Packs.Components;
using CardCleaner.Scripts.Features.Packs.Models;
using CardCleaner.Scripts.Features.Shop.Components;
using CardCleaner.Tests.Features.Packs;
using Godot;

namespace CardCleaner.Tests.Features.Shop.Components;

[TestSuite]
[RequireGodotRuntime]
public class ShelfUnpackTest
{
    private const string ProductShelfScene = "res://Scenes/Shop/Items/ProductShelf.tscn";
    private const string StorageRackScene = "res://Scenes/Shop/Items/StorageRack.tscn";
    private const string CardShelfScene = "res://Scenes/Shop/Items/Shelf.tscn";
    private const string BoxScene = "res://Scenes/Shop/Items/CardboardBox.tscn";
    private const string PackScene = "res://Scenes/Shop/Items/Pack.tscn";
    private const string BoosterScene = "res://Scenes/Shop/Items/Booster.tscn";
    private const ulong Seed = 777;
    private const int OpenFrames = 12;

    private Node3D _world = null!;
    private RecordingCardSpawner _spawner = null!;

    [BeforeTest]
    public async Task Setup()
    {
        _world = AddNode(new Node3D());
        _spawner = new RecordingCardSpawner();
        await Frames(1);
    }

    [TestCase]
    [TestCategory("Unit")]
    public async Task OpenOntoLocksTheFirstThreeBoxPacksIntoTheShelfAndSpillsTheRest()
    {
        var shelf = AddShelf();
        var box = MakeContainer(BoxScene, Seed);

        AssertBool(box.OpenOnto(FreeSlots(shelf))).IsTrue();
        AssertThat(box.PlacedCount).IsEqual(3);
        AssertThat(box.PendingCount).IsEqual(5);
        foreach (var slot in shelf.Slots)
        {
            AssertThat(slot.Cards.Count).IsEqual(1);
            AssertThat(((CardContainer)slot.Cards[0]).Kind).IsEqual(CardContainerKind.Pack);
        }

        await Frames(OpenFrames);

        AssertThat(shelf.StockedCount).IsEqual(3);
        AssertThat(Containers(_world, CardContainerKind.Pack).Length).IsEqual(5);
    }

    [TestCase]
    [TestCategory("Unit")]
    public async Task ALoosePackFromTheSameSeedMatchesTheFirstLockedPack()
    {
        var shelf = AddShelf();
        var unpacked = MakeContainer(BoxScene, Seed);
        var looseWorld = AddNode(new Node3D());
        var loose = MakeContainer(BoxScene, Seed, looseWorld);

        AssertBool(unpacked.OpenOnto(FreeSlots(shelf))).IsTrue();
        AssertBool(loose.Open()).IsTrue();
        await Frames(OpenFrames);

        var lockedFirst = (CardContainer)shelf.Slots.First().Cards[0];
        var looseFirst = Containers(looseWorld, CardContainerKind.Pack).OrderBy(c => c.GlobalPosition.Y).First();
        AssertBool(lockedFirst.ContentSeed != null).IsTrue();
        AssertBool(lockedFirst.ContentSeed == looseFirst.ContentSeed).IsTrue();
    }

    [TestCase]
    [TestCategory("Unit")]
    public async Task APackOntoAShelfYieldsBoostersInTheThreeSlots()
    {
        var shelf = AddShelf();
        var pack = MakeContainer(PackScene, Seed);

        AssertBool(pack.OpenOnto(FreeSlots(shelf))).IsTrue();
        AssertThat(pack.PlacedCount).IsEqual(3);
        AssertThat(pack.PendingCount).IsEqual(5);

        await Frames(OpenFrames);

        foreach (var slot in shelf.Slots)
            AssertThat(((CardContainer)slot.Cards[0]).Kind).IsEqual(CardContainerKind.Booster);
        AssertThat(Containers(_world, CardContainerKind.Booster).Length).IsEqual(5);
    }

    [TestCase]
    [TestCategory("Unit")]
    public void OpenOntoRefusesABoosterAndLeavesItClosed()
    {
        var shelf = AddShelf();
        var booster = MakeContainer(BoosterScene, Seed);

        AssertBool(booster.OpenOnto(FreeSlots(shelf))).IsFalse();
        AssertBool(booster.IsOpened).IsFalse();
        AssertThat(shelf.StockedCount).IsEqual(0);
    }

    [TestCase]
    [TestCategory("Unit")]
    public async Task TryUnpackOntoAProductShelfFillsItsThreeFreeSlots()
    {
        var shelf = AddShelf();
        var box = MakeContainer(BoxScene, Seed);

        AssertBool(shelf.TryUnpack(box)).IsTrue();
        AssertThat(box.PlacedCount).IsEqual(3);
        AssertThat(box.PendingCount).IsEqual(5);

        await Frames(OpenFrames);

        AssertThat(shelf.StockedCount).IsEqual(3);
    }

    [TestCase]
    [TestCategory("Unit")]
    public async Task TryUnpackWithAllSlotsStockedSpillsEveryItem()
    {
        var shelf = AddShelf();
        AssertBool(MakeContainer(BoxScene, Seed).OpenOnto(FreeSlots(shelf))).IsTrue();
        var box = MakeContainer(BoxScene, Seed + 1);

        AssertBool(shelf.TryUnpack(box)).IsTrue();
        AssertThat(box.PlacedCount).IsEqual(0);
        AssertThat(box.PendingCount).IsEqual(8);

        await Frames(OpenFrames);

        AssertThat(shelf.StockedCount).IsEqual(3);
    }

    [TestCase]
    [TestCategory("Unit")]
    public void TryUnpackBoxOntoACardShelfReturnsFalseAndLeavesItClosed()
    {
        var shelf = AddShelf(CardShelfScene);
        var box = MakeContainer(BoxScene, Seed);

        AssertBool(shelf.TryUnpack(box)).IsFalse();
        AssertBool(box.IsOpened).IsFalse();
        AssertThat(shelf.StockedCount).IsEqual(0);
    }

    [TestCase]
    [TestCategory("Unit")]
    public async Task TryUnpackBoxOntoAStorageRackFillsItsFourSlots()
    {
        var rack = AddShelf(StorageRackScene);
        var box = MakeContainer(BoxScene, Seed);

        AssertBool(rack.TryUnpack(box)).IsTrue();
        AssertThat(box.PlacedCount).IsEqual(4);
        AssertThat(box.PendingCount).IsEqual(4);

        await Frames(OpenFrames);

        AssertThat(rack.StockedCount).IsEqual(4);
    }

    private CardShelf AddShelf(string scenePath = ProductShelfScene)
    {
        var shelf = AddNode(GD.Load<PackedScene>(scenePath).Instantiate<CardShelf>());
        return shelf;
    }

    private static DeckSlot[] FreeSlots(CardShelf shelf) => shelf.Slots.Where(slot => !slot.HasCards).ToArray();

    private CardContainer MakeContainer(string scenePath, ulong seed, Node3D? parent = null)
    {
        var container = GD.Load<PackedScene>(scenePath).Instantiate<CardContainer>();
        container.ContentSeed = seed;
        container.Spawning = _spawner;
        (parent ?? _world).AddChild(container);
        return container;
    }

    private static CardContainer[] Containers(Node parent, CardContainerKind kind) =>
        parent.GetChildren().OfType<CardContainer>().Where(c => c.Kind == kind && !c.IsOpened).ToArray();

    private static async Task Frames(int count)
    {
        for (var i = 0; i < count; i++)
            await ISceneRunner.SyncProcessFrame;
    }
}
