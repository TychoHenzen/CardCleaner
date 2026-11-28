using System.Linq;
using System.Threading.Tasks;
using CardCleaner.Scripts.Features.Card.Components;
using CardCleaner.Scripts.Features.Packs.Components;
using CardCleaner.Scripts.Features.Shop.Shelves;
using CardCleaner.Tests.TestUtilities.Helpers;
using Godot;

namespace CardCleaner.Tests.Features.Card.Components;

[TestSuite]
[RequireGodotRuntime]
public class DeckSlotContainerTest
{
    private const string SlotScene = "res://Scenes/UI/CardSlot.tscn";
    private const string BoosterScene = "res://Scenes/Shop/Items/Booster.tscn";
    private const string BoxScene = "res://Scenes/Shop/Items/CardboardBox.tscn";

    // CardSlot.tscn's trigger box is 0.33 m wide, centred 0.25 m up and 0.22 m forward of the slot, so two slots
    // 0.2 m apart share a 0.13 m wide strip of trigger, and a booster halfway between them is inside both.
    private const float OverlappingSlotSpacing = 0.2f;
    private const float BoosterDropHeight = 0.2f;
    private const float TriggerDepthCentre = 0.22f;
    private const int MaxSlotChanges = 6;
    private const int SettleFrames = 10;

    [TestCase]
    public static void ProductShelfSlotLocksABoosterAndReportsIt()
    {
        var slot = MakeSlot(ShelfKind.ProductShelf);
        var booster = MakeContainer(BoosterScene);

        AssertBool(slot.TryLock(booster)).IsTrue();

        AssertBool(booster.Freeze).IsTrue();
        AssertThat(booster.GetParent()).IsEqual(slot);
        AssertBool(slot.HasCards).IsTrue();
    }

    [TestCase]
    public static void ProductShelfSlotRefusesABox()
    {
        var slot = MakeSlot(ShelfKind.ProductShelf);
        var box = MakeContainer(BoxScene);

        AssertBool(slot.TryLock(box)).IsFalse();

        AssertBool(box.Freeze).IsFalse();
        AssertThat(box.GetParent()).IsNotEqual(slot);
        AssertBool(slot.HasCards).IsFalse();
    }

    [TestCase]
    public static void FullSlotRefusesASecondBooster()
    {
        var slot = MakeSlot(ShelfKind.ProductShelf);
        AssertBool(slot.TryLock(MakeContainer(BoosterScene))).IsTrue();

        AssertBool(slot.TryLock(MakeContainer(BoosterScene))).IsFalse();
    }

    [TestCase]
    public static void ReparentingALockedContainerAwayFreesTheSlot()
    {
        var slot = MakeSlot(ShelfKind.ProductShelf);
        var booster = MakeContainer(BoosterScene);
        AssertBool(slot.TryLock(booster)).IsTrue();
        var changes = 0;
        slot.CardsChanged += () => changes++;

        booster.Reparent(AddNode(new Node3D()));

        AssertBool(slot.HasCards).IsFalse();
        AssertThat(changes).IsEqual(1);
    }

    [TestCase]
    public static void RemovingALockedContainerFreesTheSlotForTheNextOne()
    {
        var slot = MakeSlot(ShelfKind.ProductShelf);
        var first = MakeContainer(BoosterScene);
        AssertBool(slot.TryLock(first)).IsTrue();

        slot.RemoveChild(first);

        AssertBool(slot.HasCards).IsFalse();
        AssertBool(slot.TryLock(MakeContainer(BoosterScene))).IsTrue();
    }

    [TestCase]
    public static void CardShelfSlotRefusesABooster()
    {
        var slot = MakeSlot(ShelfKind.CardShelf);
        var booster = MakeContainer(BoosterScene);

        AssertBool(slot.TryLock(booster)).IsFalse();

        AssertBool(booster.Freeze).IsFalse();
        AssertBool(slot.HasCards).IsFalse();
    }

    [TestCase]
    public static void CanTakeAnswersWhatTryLockWouldDo()
    {
        var slot = MakeSlot(ShelfKind.ProductShelf);
        var booster = MakeContainer(BoosterScene);
        var box = MakeContainer(BoxScene);

        AssertBool(slot.HasRoom).IsTrue();
        AssertBool(slot.CanTake(booster)).IsTrue();
        AssertBool(slot.CanTake(box)).IsFalse();

        AssertBool(slot.TryLock(booster)).IsTrue();

        AssertBool(slot.HasRoom).IsFalse();
        AssertBool(slot.CanTake(booster)).IsFalse();
        AssertBool(slot.CanTake(MakeContainer(BoosterScene))).IsFalse();
    }

    [TestCase]
    public static void TurnedSlotSeatsTheBoosterOnItsOwnTray()
    {
        // The product shelf stands turned a quarter turn; its slot offset must turn with it.
        var shelf = AddNode(new Node3D { RotationDegrees = new Vector3(0, -90, 0), Position = new Vector3(1, 0, 4) });
        var slot = GD.Load<PackedScene>(SlotScene).Instantiate<DeckSlot>();
        slot.Capacity = 1;
        slot.Kind = ShelfKind.ProductShelf;
        shelf.AddChild(slot);
        var booster = MakeContainer(BoosterScene);

        AssertBool(slot.TryLock(booster)).IsTrue();

        var expected = slot.ToGlobal(slot.PositionOffset);
        AssertBool(booster.GlobalPosition.IsEqualApprox(expected)).IsTrue();
        AssertBool(booster.GlobalPosition.IsEqualApprox(slot.GlobalPosition + slot.PositionOffset)).IsFalse();
    }

    [TestCase]
    public static async Task ABoosterInsideTwoSlotTriggersIsLockedByOneSlotOnly()
    {
        // Two empty slots whose trigger boxes overlap, and a loose booster falling through both. Both see it enter on
        // the same physics step, and reparenting it into one slot makes Godot report it entering the other's trigger
        // again at once. Before the fix the two slots took it from each other without end (CI run 43798843821 crashed
        // in RestockerCarryTest that way), so this test breaks such a loop itself after MaxSlotChanges changes.
        var first = MakeSlot(ShelfKind.ProductShelf);
        var second = MakeSlot(ShelfKind.ProductShelf);
        second.Position = new Vector3(OverlappingSlotSpacing, 0f, 0f);
        var booster = MakeContainer(BoosterScene);
        booster.Position = new Vector3(OverlappingSlotSpacing / 2f, BoosterDropHeight, TriggerDepthCentre);
        var changes = 0;
        void Count()
        {
            if (++changes > MaxSlotChanges)
                booster.CollisionLayer = 0;
        }

        first.CardsChanged += Count;
        second.CardsChanged += Count;

        AssertThat(await BoundedWait.Until(() => first.HasCards || second.HasCards, "a slot locking the booster"))
            .IsNull();
        AssertThat(await BoundedWait.PhysicsFrames(SettleFrames)).IsNull();

        AssertThat(changes).IsEqual(1);
        AssertThat(first.Cards.Count + second.Cards.Count).IsEqual(1);
        var holder = first.HasCards ? first : second;
        AssertThat(booster.GetParent()).IsEqual(holder);
    }

    [TestCase]
    public static void ASlotRefusesABoosterAnotherSlotHolds()
    {
        var holder = MakeSlot(ShelfKind.ProductShelf);
        var other = MakeSlot(ShelfKind.ProductShelf);
        var booster = MakeContainer(BoosterScene);
        AssertBool(holder.TryLock(booster)).IsTrue();

        AssertBool(other.CanTake(booster)).IsFalse();
        AssertBool(other.TryLock(booster)).IsFalse();

        AssertThat(booster.GetParent()).IsEqual(holder);
        AssertBool(holder.Cards.Contains(booster)).IsTrue();
        AssertBool(other.HasCards).IsFalse();
    }

    private static DeckSlot MakeSlot(ShelfKind kind)
    {
        var slot = GD.Load<PackedScene>(SlotScene).Instantiate<DeckSlot>();
        slot.Capacity = 1;
        slot.Kind = kind;
        AddNode(slot);
        return slot;
    }

    private static CardContainer MakeContainer(string scenePath)
    {
        return AddNode(GD.Load<PackedScene>(scenePath).Instantiate<CardContainer>());
    }
}
