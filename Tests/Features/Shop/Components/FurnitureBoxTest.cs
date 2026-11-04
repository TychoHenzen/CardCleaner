using System.Collections.Generic;
using CardCleaner.Scripts.Features.Shop.Components;
using CardCleaner.Scripts.Features.Shop.Services;
using CardCleaner.Tests.TestUtilities;
using Godot;

namespace CardCleaner.Tests.Features.Shop.Components;

[TestSuite]
[RequireGodotRuntime]
public class FurnitureBoxTest
{
    private const string ShelfBoxPath = "res://Scenes/Shop/Items/ShelfBox.tscn";
    private const string BareBoxPath = "res://Scenes/Shop/Items/FurnitureBox.tscn";
    private const uint InteractableLayerBit = 4;

    private FakePlacement _placement = null!;

    [BeforeTest]
    public void Setup()
    {
        TestServices.Reset();
        _placement = new FakePlacement();
        TestServices.Container.RegisterSingleton<IFurniturePlacement>(_placement);
    }

    [AfterTest]
    public static void TearDown() => TestServices.Reset();

    [TestCase]
    [TestCategory("Unit")]
    public void Interact_StartsPlacementWithTheBox()
    {
        var box = BuildBox(ShelfBoxPath);

        box.Interact();

        AssertInt(_placement.Boxes.Count).IsEqual(1);
        AssertBool(ReferenceEquals(_placement.Boxes[0], box)).IsTrue();
        AssertString(box.Item!.Id).IsEqual("shelf_unit");
    }

    [TestCase]
    [TestCategory("Unit")]
    public void CanInteract_IsFalseWhilePlacementIsActive()
    {
        var box = BuildBox(ShelfBoxPath);
        _placement.IsActive = true;

        AssertBool(box.CanInteract).IsFalse();
        box.Interact();

        AssertInt(_placement.Boxes.Count).IsEqual(0);
    }

    [TestCase]
    [TestCategory("Unit")]
    public static void CanInteract_IsFalseWithoutAnItem()
    {
        var box = BuildBox(BareBoxPath);

        AssertBool(box.Item == null).IsTrue();
        AssertBool(box.CanInteract).IsFalse();
    }

    [TestCase]
    [TestCategory("Unit")]
    public static void BoxJoinsTheFurnitureBoxesGroupAndTheInteractableLayer()
    {
        var box = BuildBox(ShelfBoxPath);

        AssertBool(box.IsInGroup(FurnitureBox.GroupName)).IsTrue();
        AssertBool((box.CollisionLayer & InteractableLayerBit) == InteractableLayerBit).IsTrue();
    }

    private static FurnitureBox BuildBox(string scenePath)
    {
        var box = ResourceLoader.Load<PackedScene>(scenePath).Instantiate<FurnitureBox>();
        TestServices.Inject(box);
        AddNode(box);
        TestServices.ExecutePendingCallbacks();
        return box;
    }

    private sealed class FakePlacement : IFurniturePlacement
    {
        public bool IsActive { get; set; }

        public List<FurnitureBox> Boxes { get; } = new();

        public bool Begin(FurnitureBox box)
        {
            if (IsActive)
                return false;
            Boxes.Add(box);
            return true;
        }
    }
}
