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

/// <summary>
///     Unpacking onto a shelf: a cardboard box moves the booster boxes it holds straight into the free slots that take
///     them, the last in first, and keeps whatever no slot takes. Nothing else unpacks.
/// </summary>
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
    public void TryUnpackABoxMovesItsEightBoosterBoxesIntoTheShelfAndKeepsTheEmptyBox()
    {
        var shelf = AddShelf();
        var box = MakeContainer(BoxScene, Seed);

        AssertBool(shelf.TryUnpack(box)).IsTrue();

        // The first eight slots, the two middle boards, take one booster box each, at once.
        foreach (var slot in shelf.Slots.Take(8))
        {
            AssertThat(slot.Cards.Count).IsEqual(1);
            AssertThat(((CardContainer)slot.Cards[0]).Kind).IsEqual(CardContainerKind.Pack);
        }

        AssertThat(shelf.StockedCount).IsEqual(8);
        AssertThat(Containers(_world, CardContainerKind.Pack).Length).IsEqual(0);
        AssertBool(GodotObject.IsInstanceValid(box) && !box.IsOpened && !box.IsQueuedForDeletion()).IsTrue();
        AssertThat(box.Remaining).IsEqual(0);
    }

    [TestCase]
    [TestCategory("Unit")]
    public void TheShelfsFirstSlotGetsTheBoosterBoxASameSeedBoxGivesOutFirst()
    {
        var shelf = AddShelf();
        var unpacked = MakeContainer(BoxScene, Seed);
        var looseWorld = AddNode(new Node3D());
        var loose = MakeContainer(BoxScene, Seed, looseWorld);

        AssertBool(shelf.TryUnpack(unpacked)).IsTrue();
        AssertBool(loose.Storage!.TakeOnto(new Transform3D(Basis.Identity, new Vector3(3f, 0f, 0f)))).IsTrue();

        var firstSeed = ContainerSeeds.ChildSeed(Seed, 0);
        var lockedFirst = (CardContainer)shelf.Slots.First().Cards[0];
        AssertBool(lockedFirst.ContentSeed == firstSeed).IsTrue();
        // The taken booster box goes where loose items go (the card spawner's node when one is registered), so it is
        // looked up in the whole tree.
        var looseTaken = ((SceneTree)Engine.GetMainLoop()).GetNodesInGroup("card_containers").OfType<CardContainer>()
            .Where(c => c.Kind == CardContainerKind.Pack && c.GetParent() is not DeckSlot);
        AssertBool(looseTaken.Single().ContentSeed == firstSeed).IsTrue();
    }

    // Only a cardboard box unpacks: a booster box and a booster pack are opened in the hand, never at a shelf.
    [TestCase(PackScene)]
    [TestCase(BoosterScene)]
    [TestCategory("Unit")]
    public void TryUnpackRefusesABoosterBoxOrBoosterPackAndLeavesItSealed(string scene)
    {
        var shelf = AddShelf();
        var container = MakeContainer(scene, Seed);

        AssertBool(shelf.TryUnpack(container)).IsFalse();

        AssertBool(container.IsOpened).IsFalse();
        AssertThat(shelf.StockedCount).IsEqual(0);
        AssertThat(_world.GetChildren().OfType<CardContainer>().Count()).IsEqual(1);
    }

    [TestCase]
    [TestCategory("Unit")]
    public void TryUnpackOntoAFullShelfMovesNothingAndKeepsEveryItemInTheBox()
    {
        var shelf = AddShelf();
        // Two boxes of 8 booster boxes fill the shelf's 16 slots.
        AssertBool(shelf.TryUnpack(MakeContainer(BoxScene, Seed))).IsTrue();
        AssertBool(shelf.TryUnpack(MakeContainer(BoxScene, Seed + 2))).IsTrue();
        var box = MakeContainer(BoxScene, Seed + 1);

        AssertBool(shelf.TryUnpack(box)).IsFalse();

        AssertThat(shelf.StockedCount).IsEqual(shelf.SlotCount);
        AssertThat(box.Remaining).IsEqual(8);
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
    public void TryUnpackBoxOntoAStorageRackFillsItsFourSlotsAndKeepsTheOtherFour()
    {
        var rack = AddShelf(StorageRackScene);
        var box = MakeContainer(BoxScene, Seed);

        AssertBool(rack.TryUnpack(box)).IsTrue();

        AssertThat(rack.StockedCount).IsEqual(4);
        AssertThat(box.Remaining).IsEqual(4);
        AssertThat(Containers(_world, CardContainerKind.Pack).Length).IsEqual(0);
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
    public void MovingIntoNoSlotsMovesNothing()
    {
        var box = MakeContainer(BoxScene, Seed);

        AssertThat(box.Storage!.MoveInto(Array.Empty<DeckSlot>())).IsEqual(0);
        AssertThat(box.Remaining).IsEqual(8);
    }

    [TestCase]
    [TestCategory("Unit")]
    public void MovingIntoThreeFreeSlotsMovesThreeAndKeepsFive()
    {
        var shelf = AddShelf();
        var box = MakeContainer(BoxScene, Seed);

        AssertThat(box.Storage!.MoveInto(FreeSlots(shelf).Take(3).ToArray())).IsEqual(3);

        AssertThat(shelf.StockedCount).IsEqual(3);
        AssertThat(box.Remaining).IsEqual(5);
    }

    // Away from the origin, where the containers are made, so no item lands in a bottom slot's trigger on its own.
    private static CardShelf AddShelf(string scenePath = ProductShelfScene)
    {
        var shelf = GD.Load<PackedScene>(scenePath).Instantiate<CardShelf>();
        shelf.Position = new Vector3(0, 0, -5);
        return AddNode(shelf);
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
}
