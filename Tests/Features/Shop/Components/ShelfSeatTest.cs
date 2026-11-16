using System.Linq;
using CardCleaner.Scripts.Features.Shop.Components;
using Godot;

namespace CardCleaner.Tests.Features.Shop.Components;

/// <summary>
///     Where stock sits once a shelf slot locks it: flat on the slot's tray, centred on it, inside the shelf's own
///     footprint, all of it turning with the shelf. Each shelf stands turned a quarter turn, as the shop's product
///     shelf does, so an offset applied in world space instead of the slot's frame fails here.
/// </summary>
[TestSuite]
[RequireGodotRuntime]
public class ShelfSeatTest
{
    private const string ShelfScene = "res://Scenes/Shop/Items/Shelf.tscn";
    private const string ProductShelfScene = "res://Scenes/Shop/Items/ProductShelf.tscn";
    private const string RackScene = "res://Scenes/Shop/Items/StorageRack.tscn";
    private const string CardStock = "card";

    // The slot's tray (Assets/Models/SingleCard.fbx) is 0.03 m thick, and its centre lies 0.2 m behind the slot origin.
    private const float TrayTop = 0.03f;
    private const float TrayCentreZ = 0.2f;
    private const float Tolerance = 0.015f;

    [TestCase(ShelfScene, CardStock)]
    [TestCase(ProductShelfScene, "res://Scenes/Shop/Items/Booster.tscn")]
    [TestCase(ProductShelfScene, "res://Scenes/Shop/Items/Pack.tscn")]
    [TestCase(RackScene, "res://Scenes/Shop/Items/Booster.tscn")]
    [TestCase(RackScene, "res://Scenes/Shop/Items/CardboardBox.tscn")]
    [TestCategory("Unit")]
    public static void StockRestsOnTheTrayInsideATurnedShelf(string shelfScene, string stock)
    {
        var shelf = AddNode(GD.Load<PackedScene>(shelfScene).Instantiate<CardShelf>());
        shelf.GlobalTransform = new Transform3D(new Basis(Vector3.Up, Mathf.Pi / 2), new Vector3(3, 0, -2));
        var footprint = ShapesIn(shelf, shelf.GetNode<CollisionShape3D>("CollisionShape3D"));

        foreach (var slot in shelf.Slots)
        {
            var body = Make(stock);
            AssertBool(slot.TryLock(body)).OverrideFailureMessage($"{slot.Name} refused {stock}").IsTrue();

            var onSlot = BodyShapesIn(slot, body);
            var centre = onSlot.GetCenter();
            AssertBool(Near(centre.X, 0) && Near(centre.Z, TrayCentreZ))
                .OverrideFailureMessage($"{slot.Name}: {stock} centred at {centre}, not on the tray centre").IsTrue();
            AssertBool(Near(onSlot.Position.Y, TrayTop))
                .OverrideFailureMessage($"{slot.Name}: {stock} bottom at {onSlot.Position.Y}, tray top {TrayTop}")
                .IsTrue();

            var onShelf = BodyShapesIn(shelf, body);
            AssertBool(onShelf.Position.X >= footprint.Position.X - Tolerance
                       && onShelf.End.X <= footprint.End.X + Tolerance
                       && onShelf.Position.Z >= footprint.Position.Z - Tolerance
                       && onShelf.End.Z <= footprint.End.Z + Tolerance)
                .OverrideFailureMessage($"{slot.Name}: {stock} sticks out of the shelf ({onShelf} in {footprint})")
                .IsTrue();
        }
    }

    private static bool Near(float value, float expected) => Mathf.Abs(value - expected) <= Tolerance;

    private static RigidBody3D Make(string stock)
    {
        RigidBody3D body = stock == CardStock
            ? ShopTestCards.Create(null)
            : GD.Load<PackedScene>(stock).Instantiate<RigidBody3D>();
        return AddNode(body);
    }

    // The body's own collision shapes, the part that physically rests on the tray.
    private static Aabb BodyShapesIn(Node3D frame, RigidBody3D body)
    {
        var shapes = body.GetChildren().OfType<CollisionShape3D>().Where(shape => !shape.Disabled).ToList();
        var box = ShapesIn(frame, shapes[0]);
        foreach (var shape in shapes.Skip(1))
            box = box.Merge(ShapesIn(frame, shape));
        return box;
    }

    private static Aabb ShapesIn(Node3D frame, CollisionShape3D shape)
    {
        var toFrame = frame.GlobalTransform.AffineInverse() * shape.GlobalTransform;
        return toFrame * shape.Shape.GetDebugMesh().GetAabb();
    }
}
