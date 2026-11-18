using System;
using System.Linq;
using System.Threading.Tasks;
using CardCleaner.Scripts.Features.Card.Components;
using CardCleaner.Scripts.Features.Packs.Components;
using CardCleaner.Scripts.Features.Packs.Models;
using CardCleaner.Scripts.Features.Shop.Components;
using CardCleaner.Tests.Features.Packs;
using Godot;
using static CardCleaner.Tests.Features.Packs.Components.ContainerOpeningWait;

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
    public async Task OpenOntoLocksAllEightBoxPacksIntoTheShelf()
    {
        var shelf = AddShelf();
        var box = MakeContainer(BoxScene, Seed);

        AssertBool(box.OpenOnto(FreeSlots(shelf))).IsTrue();
        AssertThat(box.PlacedCount).IsEqual(8);
        AssertThat(box.PendingCount).IsEqual(0);
        foreach (var slot in shelf.Slots)
        {
            AssertThat(slot.Cards.Count).IsEqual(1);
            AssertThat(((CardContainer)slot.Cards[0]).Kind).IsEqual(CardContainerKind.Pack);
        }

        await UntilOpened(box);

        AssertThat(shelf.StockedCount).IsEqual(8);
        AssertThat(Containers(_world, CardContainerKind.Pack).Length).IsEqual(0);
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
        await UntilOpened(unpacked);
        await UntilOpened(loose);

        // Both took the seed of the box's first pack: the shelf's first slot, and one of the loose packs.
        var firstSeed = ContainerSeeds.ChildSeed(Seed, 0);
        var lockedFirst = (CardContainer)shelf.Slots.First().Cards[0];
        AssertBool(lockedFirst.ContentSeed == firstSeed).IsTrue();
        AssertBool(Containers(looseWorld, CardContainerKind.Pack).Any(c => c.ContentSeed == firstSeed)).IsTrue();
    }

    [TestCase]
    [TestCategory("Unit")]
    public async Task APackUnpackedOntoAProductShelfStacksItsBoostersFourToASlot()
    {
        var shelf = AddShelf();
        var pack = MakeContainer(PackScene, Seed);

        AssertBool(shelf.TryUnpack(pack)).IsTrue();
        AssertThat(pack.PlacedCount).IsEqual(8);
        AssertThat(pack.PendingCount).IsEqual(0);

        await UntilOpened(pack);

        var counts = shelf.Slots.Select(slot => slot.Cards.Count).ToArray();
        AssertThat(string.Join(",", counts)).IsEqual("4,4,0,0,0,0,0,0");
        foreach (var body in shelf.StockedCards)
            AssertThat(((CardContainer)body).Kind).IsEqual(CardContainerKind.Booster);
    }

    [TestCase]
    [TestCategory("Unit")]
    public async Task TryUnpackSkipsASlotThatHoldsAPack()
    {
        var shelf = AddShelf();
        var first = shelf.Slots.First();
        AssertBool(first.TryLock(MakeContainer(PackScene, Seed + 1))).IsTrue();
        var pack = MakeContainer(PackScene, Seed);

        AssertBool(shelf.TryUnpack(pack)).IsTrue();
        await UntilOpened(pack);

        var counts = shelf.Slots.Select(slot => slot.Cards.Count).ToArray();
        AssertThat(string.Join(",", counts)).IsEqual("1,4,4,0,0,0,0,0");
        AssertThat(((CardContainer)first.Cards[0]).Kind).IsEqual(CardContainerKind.Pack);
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
    public async Task TryUnpackABoxOntoAProductShelfFillsItsEightSlots()
    {
        var shelf = AddShelf();
        var box = MakeContainer(BoxScene, Seed);

        AssertBool(shelf.TryUnpack(box)).IsTrue();
        AssertThat(box.PlacedCount).IsEqual(8);
        AssertThat(box.PendingCount).IsEqual(0);

        await UntilOpened(box);

        AssertThat(shelf.StockedCount).IsEqual(8);
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

        await UntilOpened(box);

        AssertThat(shelf.StockedCount).IsEqual(8);
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

        await UntilOpened(box);

        AssertThat(rack.StockedCount).IsEqual(4);
    }

    [TestCase]
    [TestCategory("Unit")]
    public async Task ABoxDroppedOnACardShelfSlotIsNotLocked()
    {
        var shelf = AddShelf(CardShelfScene);
        var slot = shelf.Slots.First();
        var box = MakeContainer(BoxScene, Seed);
        box.Freeze = false;

        slot.Area.EmitSignal(Area3D.SignalName.BodyEntered, box);
        await Frames(2);

        AssertBool(box.Freeze).IsFalse();
        AssertThat(box.GetParent()).IsNotEqual(slot);
        AssertThat(shelf.StockedCount).IsEqual(0);
    }

    [TestCase]
    [TestCategory("Unit")]
    public void OpenOntoAShelfWithZeroFreeSlotsPlacesNothingAndSpillsEight()
    {
        var box = MakeContainer(BoxScene, Seed);

        AssertBool(box.OpenOnto(Array.Empty<DeckSlot>())).IsTrue();
        AssertThat(box.PlacedCount).IsEqual(0);
        AssertThat(box.PendingCount).IsEqual(8);
    }

    [TestCase]
    [TestCategory("Unit")]
    public void OpenOntoAShelfWithThreeFreeSlotsPlacesThreeAndSpillsFive()
    {
        var shelf = AddShelf();
        var box = MakeContainer(BoxScene, Seed);

        AssertBool(box.OpenOnto(FreeSlots(shelf).Take(3).ToArray())).IsTrue();
        AssertThat(box.PlacedCount).IsEqual(3);
        AssertThat(box.PendingCount).IsEqual(5);
    }

    private static CardShelf AddShelf(string scenePath = ProductShelfScene) =>
        AddNode(GD.Load<PackedScene>(scenePath).Instantiate<CardShelf>());

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
}
