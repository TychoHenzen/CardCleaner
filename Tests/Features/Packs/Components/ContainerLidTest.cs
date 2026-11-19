using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using CardCleaner.Scripts.Features.Card.Components;
using CardCleaner.Scripts.Features.Packs.Components;
using CardCleaner.Scripts.Features.Packs.Models;
using CardCleaner.Scripts.Features.Shop.Components;
using Godot;
using NSubstitute;
using static CardCleaner.Tests.Features.Packs.Components.ContainerOpeningWait;

namespace CardCleaner.Tests.Features.Packs.Components;

/// <summary>
///     A cardboard box opened in the hand: its flaps swing open and shut without spilling anything, and each
///     <see cref="CardContainer.TakeOneInto" /> moves the next item into a shelf slot with the child seed of its place
///     in the box. A cardboard box is storage: emptied, it stays, and it never opens whole. A booster box and a booster
///     pack have no lid: one empties into the hand, the other opens into the hand of cards.
/// </summary>
[TestSuite]
[RequireGodotRuntime]
public class ContainerLidTest
{
    private const string BoxScene = "res://Scenes/Shop/Items/CardboardBox.tscn";
    private const string PackScene = "res://Scenes/Shop/Items/Pack.tscn";
    private const string BoosterScene = "res://Scenes/Shop/Items/Booster.tscn";
    private const string ProductShelfScene = "res://Scenes/Shop/Items/ProductShelf.tscn";
    private const ulong Seed = 0x1D5EEDUL;

    private Node3D _world = null!;
    private RecordingCardSpawner _spawner = null!;

    [BeforeTest]
    public async Task Setup()
    {
        _world = AddNode(new Node3D());
        _spawner = new RecordingCardSpawner();
        await Frames(1);
    }

    [TestCase(PackScene)]
    [TestCase(BoosterScene)]
    [TestCategory("Unit")]
    public void ABoosterBoxAndABoosterPackHaveNoLid(string scene)
    {
        var container = MakeContainer(scene);

        AssertBool(container.HasLid).IsFalse();
        AssertBool(container.SetLidOpen(true)).IsFalse();
        AssertBool(container.IsLidOpen).IsFalse();
    }

    [TestCase]
    [TestCategory("Unit")]
    public async Task TheLidSwingsOpenToTheSamePoseAsAnInstantOpen()
    {
        var swung = MakeContainer(BoxScene);
        var instant = MakeContainer(BoxScene);
        instant.SetLidOpen(true, instantly: true);

        AssertBool(swung.SetLidOpen(true)).IsTrue();
        AssertBool(swung.IsProcessing()).IsTrue();
        await Until(() => !swung.IsProcessing(), "the lid swinging open");

        AssertPosesMatch(swung, instant);
        AssertThat(swung.Remaining).IsEqual(CardContainerLayout.ItemsPerContainer);
        AssertThat(_world.GetChildren().OfType<CardContainer>().Count()).IsEqual(2);
    }

    // A pick-up, a drop or a slot reparents the container, which stops its processing; the swing must still finish.
    [TestCase]
    [TestCategory("Unit")]
    public async Task ALidStillSwingingFinishesAfterAReparent()
    {
        var box = MakeContainer(BoxScene);
        var instant = MakeContainer(BoxScene);
        instant.SetLidOpen(true, instantly: true);
        var hand = new Node3D { Name = "Hand" };
        _world.AddChild(hand);

        box.SetLidOpen(true);
        await Frames(1);
        box.Reparent(hand);

        AssertBool(box.IsProcessing()).IsTrue();
        await Until(() => !box.IsProcessing(), "the lid swinging open after the reparent");
        AssertPosesMatch(box, instant);
    }

    [TestCase]
    [TestCategory("Unit")]
    public async Task AClosedLidSwingsBackToItsShutPose()
    {
        var box = MakeContainer(BoxScene);
        var shut = MakeContainer(BoxScene);
        box.SetLidOpen(true, instantly: true);

        AssertBool(box.SetLidOpen(false)).IsTrue();
        await Until(() => !box.IsProcessing(), "the lid swinging shut");

        AssertBool(box.IsLidOpen).IsFalse();
        AssertPosesMatch(box, shut);
    }

    [TestCase]
    [TestCategory("Unit")]
    public void NothingIsTakenOutOfAShutBox()
    {
        var shelf = AddShelf();
        var box = MakeContainer(BoxScene);
        var slot = shelf.Slots.First();

        AssertBool(box.TakeOneInto(slot)).IsFalse();

        AssertBool(slot.HasCards).IsFalse();
        AssertThat(box.Remaining).IsEqual(CardContainerLayout.ItemsPerContainer);
    }

    [TestCase]
    [TestCategory("Unit")]
    public void EachTakeLocksTheNextPackWithTheSeedOpeningWouldGiveIt()
    {
        var shelf = AddShelf();
        var box = MakeContainer(BoxScene);
        box.SetLidOpen(true, instantly: true);
        var slots = shelf.Slots.Take(2).ToArray();

        AssertBool(box.TakeOneInto(slots[0])).IsTrue();
        AssertBool(box.TakeOneInto(slots[1])).IsTrue();

        var first = (CardContainer)slots[0].Cards.Single();
        var second = (CardContainer)slots[1].Cards.Single();
        AssertThat(first.Kind).IsEqual(CardContainerKind.Pack);
        AssertBool(first.ContentSeed == ContainerSeeds.ChildSeed(Seed, 0)).IsTrue();
        AssertBool(second.ContentSeed == ContainerSeeds.ChildSeed(Seed, 1)).IsTrue();
        AssertBool(first.Freeze).IsTrue();
        AssertThat(box.Remaining).IsEqual(CardContainerLayout.ItemsPerContainer - 2);
    }

    // The emptied box is kept, to be thrown in a trash can, and can still be shut.
    [TestCase]
    [TestCategory("Unit")]
    public void TakingTheLastPackLeavesTheEmptyBox()
    {
        var shelf = AddShelf();
        var box = MakeContainer(BoxScene);
        box.SetLidOpen(true, instantly: true);

        foreach (var slot in shelf.Slots.Take(CardContainerLayout.ItemsPerContainer))
            AssertBool(box.TakeOneInto(slot)).IsTrue();

        AssertThat(shelf.StockedCount).IsEqual(CardContainerLayout.ItemsPerContainer);
        AssertThat(box.Remaining).IsEqual(0);
        AssertBool(box.IsOpened).IsFalse();
        AssertBool(box.IsQueuedForDeletion()).IsFalse();
        AssertBool(box.TakeOneInto(shelf.Slots.Last())).IsFalse();
        AssertBool(box.SetLidOpen(false)).IsTrue();
    }

    // The item is spawned into the slot before the slot is asked to lock it, so a refusal must free it again.
    [TestCase]
    [TestCategory("Unit")]
    public void ATakeIntoAFullSlotTakesNothingAndLeavesNoStrayItem()
    {
        var shelf = AddShelf();
        var slot = shelf.Slots.First();
        AssertBool(slot.TryLock(MakeContainer(PackScene))).IsTrue();
        var box = MakeContainer(BoxScene);
        box.SetLidOpen(true, instantly: true);

        AssertBool(box.TakeOneInto(slot)).IsFalse();

        AssertThat(slot.GetChildren().OfType<CardContainer>().Count()).IsEqual(1);
        AssertThat(box.Remaining).IsEqual(CardContainerLayout.ItemsPerContainer);
    }

    [TestCase]
    [TestCategory("Unit")]
    public void AHeldCardboardBoxNeitherEmptiesIntoTheHandNorOpensIntoIt()
    {
        var box = MakeContainer(BoxScene);
        var hand = InHandOpening.AddHand(_world);
        InHandOpening.Hold(box, hand);

        AssertThat(box.EmptyIntoHand().Count).IsEqual(0);
        AssertBool(box.OpenInHand(Substitute.For<ICardHolder>())).IsFalse();

        AssertBool(box.IsOpened).IsFalse();
        AssertThat(hand.GetChildren().OfType<CardContainer>().Count()).IsEqual(1);
        AssertThat(box.Remaining).IsEqual(CardContainerLayout.ItemsPerContainer);
    }

    // Unpacking moves what is left straight into the slots, with the seeds the takes would have given, and keeps the
    // emptied box.
    [TestCase]
    [TestCategory("Unit")]
    public void ABoxUnpackedAfterThreeTakesPlacesTheOtherFive()
    {
        var shelf = AddShelf();
        var box = MakeContainer(BoxScene);
        box.SetLidOpen(true, instantly: true);
        foreach (var slot in shelf.Slots.Take(3))
            AssertBool(box.TakeOneInto(slot)).IsTrue();

        AssertBool(shelf.TryUnpack(box)).IsTrue();

        AssertThat(shelf.StockedCount).IsEqual(CardContainerLayout.ItemsPerContainer);
        AssertThat(Seeds(shelf.StockedCards.OfType<CardContainer>())).IsEqual(ExpectedSeeds(0, 8));
        AssertThat(box.Remaining).IsEqual(0);
        AssertBool(box.IsQueuedForDeletion()).IsFalse();
    }

    private static void AssertPosesMatch(CardContainer actual, CardContainer expected)
    {
        var actualParts = OpeningPart.Under(actual.GetNode("Visual"));
        var expectedParts = OpeningPart.Under(expected.GetNode("Visual"));
        AssertThat(actualParts.Count).IsEqual(expectedParts.Count);
        AssertThat(actualParts.Count).IsGreater(0);
        for (var i = 0; i < actualParts.Count; i++)
        {
            AssertBool(actualParts[i].Transform.IsEqualApprox(expectedParts[i].Transform))
                .OverrideFailureMessage(
                    $"{actualParts[i].Name} is at {actualParts[i].Transform}, expected {expectedParts[i].Transform}")
                .IsTrue();
        }
    }

    private static ulong[] Seeds(IEnumerable<CardContainer> containers) =>
        containers.Select(c => c.ContentSeed!.Value).Order().ToArray();

    private static ulong[] ExpectedSeeds(int from, int to) =>
        Enumerable.Range(from, to - from).Select(i => ContainerSeeds.ChildSeed(Seed, i)).Order().ToArray();

    // Beside the containers, which stand at the origin, so no slot's trigger reaches one and locks it.
    private static CardShelf AddShelf()
    {
        var shelf = AddNode(GD.Load<PackedScene>(ProductShelfScene).Instantiate<CardShelf>());
        shelf.Position = new Vector3(5f, 0f, 0f);
        return shelf;
    }

    private CardContainer MakeContainer(string scenePath)
    {
        var container = GD.Load<PackedScene>(scenePath).Instantiate<CardContainer>();
        container.ContentSeed = Seed;
        container.Spawning = _spawner;
        _world.AddChild(container);
        return container;
    }
}
