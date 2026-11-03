using CardCleaner.Scripts.Features.Card.Components;
using CardCleaner.Scripts.Features.Packs.Components;
using CardCleaner.Scripts.Features.Shop.Models;
using Godot;

namespace CardCleaner.Tests.Features.Card.Components;

[TestSuite]
[RequireGodotRuntime]
public class DeckSlotContainerTest
{
    private const string SlotScene = "res://Scenes/UI/CardSlot.tscn";
    private const string BoosterScene = "res://Scenes/Shop/Items/Booster.tscn";
    private const string BoxScene = "res://Scenes/Shop/Items/CardboardBox.tscn";

    [TestCase]
    [TestCategory("Unit")]
    public void ProductShelfSlotLocksABoosterAndReportsIt()
    {
        var slot = MakeSlot(ShelfKind.ProductShelf);
        var booster = MakeContainer(BoosterScene);

        AssertBool(slot.TryLock(booster)).IsTrue();

        AssertBool(booster.Freeze).IsTrue();
        AssertThat(booster.GetParent()).IsEqual(slot);
        AssertBool(slot.HasCards).IsTrue();
    }

    [TestCase]
    [TestCategory("Unit")]
    public void ProductShelfSlotRefusesABox()
    {
        var slot = MakeSlot(ShelfKind.ProductShelf);
        var box = MakeContainer(BoxScene);

        AssertBool(slot.TryLock(box)).IsFalse();

        AssertBool(box.Freeze).IsFalse();
        AssertThat(box.GetParent()).IsNotEqual(slot);
        AssertBool(slot.HasCards).IsFalse();
    }

    [TestCase]
    [TestCategory("Unit")]
    public void FullSlotRefusesASecondBooster()
    {
        var slot = MakeSlot(ShelfKind.ProductShelf);
        AssertBool(slot.TryLock(MakeContainer(BoosterScene))).IsTrue();

        AssertBool(slot.TryLock(MakeContainer(BoosterScene))).IsFalse();
    }

    [TestCase]
    [TestCategory("Unit")]
    public void ReparentingALockedContainerAwayFreesTheSlot()
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
    [TestCategory("Unit")]
    public void RemovingALockedContainerFreesTheSlotForTheNextOne()
    {
        var slot = MakeSlot(ShelfKind.ProductShelf);
        var first = MakeContainer(BoosterScene);
        AssertBool(slot.TryLock(first)).IsTrue();

        slot.RemoveChild(first);

        AssertBool(slot.HasCards).IsFalse();
        AssertBool(slot.TryLock(MakeContainer(BoosterScene))).IsTrue();
    }

    [TestCase]
    [TestCategory("Unit")]
    public void CardShelfSlotRefusesABooster()
    {
        var slot = MakeSlot(ShelfKind.CardShelf);
        var booster = MakeContainer(BoosterScene);

        AssertBool(slot.TryLock(booster)).IsFalse();

        AssertBool(booster.Freeze).IsFalse();
        AssertBool(slot.HasCards).IsFalse();
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
