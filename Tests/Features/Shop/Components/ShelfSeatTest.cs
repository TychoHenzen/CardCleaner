using System;
using System.Collections.Generic;
using System.Linq;
using CardCleaner.Scripts.Features.Shop.Components;
using Godot;

namespace CardCleaner.Tests.Features.Shop.Components;

/// <summary>
///     Where stock sits once a shelf slot locks it: on a surface of the shelf's own model, centred on the slot, inside
///     the shelf's colliders, all of it turning with the shelf. The surfaces are read from the art mesh, so a slot
///     that floats above a board or sinks into one fails here. Each shelf stands turned a quarter turn, as the shop's
///     product shelf does, so an offset applied in world space instead of the slot's frame fails too.
/// </summary>
[TestSuite]
[RequireGodotRuntime]
public class ShelfSeatTest
{
    private const string ShelfScene = "res://Scenes/Shop/Items/Shelf.tscn";
    private const string ProductShelfScene = "res://Scenes/Shop/Items/ProductShelf.tscn";
    private const string RackScene = "res://Scenes/Shop/Items/StorageRack.tscn";
    private const string CardMeshPath = "res://Data/Card/CardMesh.tres";
    private const string CardScenePath = "res://Scenes/Components/CardShader.tscn";
    private const string CardFaceScenePath = Scripts.Features.Card.Components.CardFace.ScenePath;
    private const string CardFaceNode = "Face";
    private const string CardMeshNode = "OuterBox_Baked";
    private const float Tolerance = 0.015f;

    // The player's eye: the camera's head sits 0.9 m above the centre of a 1.8 m capsule standing on the floor.
    private const float EyeHeight = 1.8f;

    // How far in front of a shelf a player stands to look at its stock.
    private const float EyeDistance = 1.2f;

    // A face whose normal is within this of straight up is a surface stock can stand on.
    private const float UpFacing = 0.9f;

    [TestCase(ProductShelfScene, "res://Scenes/Shop/Items/Booster.tscn")]
    [TestCase(ProductShelfScene, "res://Scenes/Shop/Items/Pack.tscn")]
    [TestCase(RackScene, "res://Scenes/Shop/Items/Booster.tscn")]
    [TestCase(RackScene, "res://Scenes/Shop/Items/CardboardBox.tscn")]
    public static void StockRestsOnABoardOfTheShelfModel(string shelfScene, string stock)
    {
        var shelf = TurnedShelf(shelfScene);
        var surfaces = UpFaces(shelf);
        var footprint = ColliderBounds(shelf);

        foreach (var slot in shelf.Slots)
        {
            var body = AddNode(GD.Load<PackedScene>(stock).Instantiate<RigidBody3D>());
            AssertBool(slot.TryLock(body)).OverrideFailureMessage($"{slot.Name} refused {stock}").IsTrue();

            var onSlot = BodyShapesIn(slot, body);
            AssertBool(Near(onSlot.GetCenter().X, 0))
                .OverrideFailureMessage($"{slot.Name}: {stock} centred at {onSlot.GetCenter()}, not on the slot")
                .IsTrue();

            var onShelf = BodyShapesIn(shelf, body);
            var bottom = new Vector3(onShelf.GetCenter().X, onShelf.Position.Y, onShelf.GetCenter().Z);
            var surface = SurfaceBelow(surfaces, bottom);
            AssertBool(surface is { } y && Near(bottom.Y, y))
                .OverrideFailureMessage($"{slot.Name}: {stock} bottom at {bottom}, the model's surface under it at "
                                        + $"{surface?.ToString() ?? "none"}")
                .IsTrue();
            AssertInside(footprint, onShelf, $"{slot.Name}: {stock}");
        }
    }

    // A box is 0.669 m tall since it holds four stacks of 128 cards; it must not reach into the board above its seat.
    [TestCase]
    public static void ABoxOnEveryRackSeatClearsTheBoardAboveIt()
    {
        var shelf = TurnedShelf(RackScene);
        var boards = ShelfBoxes(shelf).Where(box => box.Owner == shelf).ToList();

        foreach (var slot in shelf.Slots)
        {
            var box = AddNode(GD.Load<PackedScene>("res://Scenes/Shop/Items/CardboardBox.tscn")
                .Instantiate<RigidBody3D>());
            AssertBool(slot.TryLock(box)).OverrideFailureMessage($"{slot.Name} refused a cardboard box").IsTrue();

            var onShelf = BodyShapesIn(shelf, box);
            var hit = boards.FirstOrDefault(board => board.Overlaps(onShelf));
            AssertThat(hit.Owner).OverrideFailureMessage(
                    $"{slot.Name}: a box from {onShelf.Position.Y:0.000} to {onShelf.End.Y:0.000} m reaches into "
                    + $"{hit.Name} ({hit.Box.Position.Y:0.000} to {hit.Box.End.Y:0.000} m)")
                .IsNull();
        }
    }

    [TestCase]
    public static void EveryCardStandsOnItsStandOnABoardFacingTheFront()
    {
        var shelf = TurnedShelf(ShelfScene);
        var surfaces = UpFaces(shelf);
        var footprint = ColliderBounds(shelf);
        var colliders = ShelfBoxes(shelf).Where(box => box.Owner == shelf).ToList();

        AssertThat(shelf.SlotCount).IsEqual(8);
        foreach (var slot in shelf.Slots)
        {
            var standBase = slot.GetNode<MeshInstance3D>("Upright/Base");
            var baseBox = MeshIn(shelf, standBase);
            var baseBottom = new Vector3(baseBox.GetCenter().X, baseBox.Position.Y, baseBox.GetCenter().Z);
            var surface = SurfaceBelow(surfaces, baseBottom);
            AssertBool(surface is { } y && Near(baseBottom.Y, y))
                .OverrideFailureMessage($"{slot.Name}: stand base at {baseBottom}, the board under it at "
                                        + $"{surface?.ToString() ?? "none"}")
                .IsTrue();
            AssertBool(Mathf.Abs(baseBox.Size.Y - BaseThickness(standBase)) < 0.001f)
                .OverrideFailureMessage($"{slot.Name}: the stand base is not level ({baseBox.Size})").IsTrue();

            var card = AddNode(ShopTestCards.Create(null));
            AssertBool(slot.TryLock(card)).OverrideFailureMessage($"{slot.Name} refused a card").IsTrue();

            var onShelf = BodyShapesIn(shelf, card);
            AssertBool(onShelf.Position.Y >= baseBox.End.Y - 0.002f && onShelf.Position.Y <= baseBox.End.Y + 0.01f)
                .OverrideFailureMessage(
                    $"{slot.Name}: card bottom at {onShelf.Position.Y}, stand base top {baseBox.End.Y}")
                .IsTrue();
            // A card on top of the shelf stands above its colliders, so only its sides are held to the footprint.
            AssertInside(footprint, onShelf, $"{slot.Name}: card", capHeight: false);
            var cut = colliders.FirstOrDefault(box => box.Overlaps(onShelf));
            AssertObject(cut.Owner).OverrideFailureMessage($"{slot.Name}: the card cuts into {cut.Name}").IsNull();

            // Up is where the card's texture has its top edge, read from the card mesh, not an axis picked here: the
            // top edge is above the centre and leans back, and the face looks out of the front (-Z) and up.
            var (top, centre, face) = CardPoints(shelf, card);
            AssertBool(top.Y - centre.Y > 0.02f && top.Z >= centre.Z && face.Z < -0.1f && face.Y > 0.3f)
                .OverrideFailureMessage($"{slot.Name}: card top at {top}, centre {centre}, face {face}: not upright "
                                        + "facing the front").IsTrue();
        }
    }

    // A player standing in front of the card shelf sees every card's art: the line from a standing eye to the card's
    // centre and to the middle of its top edge passes no part of the shelf, its price tags included.
    [TestCase]
    public static void AStandingPlayerSeesEveryCardsArt()
    {
        var shelf = TurnedShelf(ShelfScene);
        var boxes = ShelfBoxes(shelf);
        var front = ColliderBounds(shelf).Position.Z;

        foreach (var slot in shelf.Slots)
        {
            var card = AddNode(ShopTestCards.Create(null));
            AssertBool(slot.TryLock(card)).OverrideFailureMessage($"{slot.Name} refused a card").IsTrue();
            var (top, centre, _) = CardPoints(shelf, card);

            foreach (var point in new[] { top, centre })
            {
                var eye = new Vector3(point.X, EyeHeight, front - EyeDistance);
                var blocker = boxes.FirstOrDefault(box => box.Blocks(eye, point));
                AssertObject(blocker.Owner)
                    .OverrideFailureMessage(
                        $"{slot.Name}: {blocker.Name} hides the card at {point} from an eye at {eye}")
                    .IsNull();
            }
        }
    }

    // The colliders leave the space between two boards open, so a delivered shelf stacks as one box over the whole
    // shelf, on no layer and colliding with nothing. It is a trigger that a delivery counts as footprint, never a
    // body, which the navigation bake would parse even while the shelf is carried.
    [TestCase(ShelfScene)]
    [TestCase(ProductShelfScene)]
    [TestCase(RackScene)]
    public static void TheFootprintVolumeCoversTheWholeShelf(string shelfScene)
    {
        var shelf = TurnedShelf(shelfScene);
        var footprint = shelf.FootprintVolume;
        AssertObject(footprint).IsNotNull();

        AssertThat(footprint!.CollisionLayer).IsEqual(0u);
        AssertThat(footprint.CollisionMask).IsEqual(0u);
        AssertBool(footprint.IsInGroup(DeliveryMarker.FootprintGroup)).IsTrue();
        var box = ShapesIn(shelf, footprint.GetChildren().OfType<CollisionShape3D>().Single());
        var bounds = ColliderBounds(shelf);
        AssertBool(box.Position.IsEqualApprox(bounds.Position) && box.Size.IsEqualApprox(bounds.Size))
            .OverrideFailureMessage($"footprint box {box}, shelf colliders {bounds}").IsTrue();
    }

    private static CardShelf TurnedShelf(string scene)
    {
        var shelf = AddNode(GD.Load<PackedScene>(scene).Instantiate<CardShelf>());
        shelf.GlobalTransform = new Transform3D(new Basis(Vector3.Up, Mathf.Pi / 2), new Vector3(3, 0, -2));
        return shelf;
    }

    private static bool Near(float value, float expected) => Mathf.Abs(value - expected) <= Tolerance;

    private static void AssertInside(Aabb footprint, Aabb box, string what, bool capHeight = true)
    {
        AssertBool(box.Position.X >= footprint.Position.X - Tolerance
                   && box.End.X <= footprint.End.X + Tolerance
                   && box.Position.Z >= footprint.Position.Z - Tolerance
                   && box.End.Z <= footprint.End.Z + Tolerance
                   && (!capHeight || box.End.Y <= footprint.End.Y + Tolerance))
            .OverrideFailureMessage($"{what} sticks out of the shelf ({box} in {footprint})")
            .IsTrue();
    }

    // The middle of the card's top edge, its centre and its face direction, in the shelf's frame. The top edge is
    // where the card texture starts (the smallest UV v of the card mesh), so a card shown upside down fails.
    private static (Vector3 Top, Vector3 Centre, Vector3 Face) CardPoints(Node3D frame, Node3D card)
    {
        var toFrame = frame.GlobalTransform.AffineInverse() * card.GlobalTransform;
        var (top, centre, face) = CardFace.Value;
        return (toFrame * top, toFrame * centre, (toFrame.Basis * face).Normalized());
    }

    // The same three in the card body's frame, read from the real card: its mesh resource and the transform of the
    // mesh node in the card scene. A test card carries no mesh, so its body stands in for the real one.
    private static readonly Lazy<(Vector3 Top, Vector3 Centre, Vector3 Face)> CardFace = new(() =>
    {
        var mesh = GD.Load<ArrayMesh>(CardMeshPath);
        var arrays = mesh.SurfaceGetArrays(0);
        var vertices = arrays[(int)Mesh.ArrayType.Vertex].AsVector3Array();
        var uvs = arrays[(int)Mesh.ArrayType.TexUV].AsVector2Array();
        var topV = uvs.Min(uv => uv.Y);
        var edge = vertices.Where((_, i) => uvs[i].Y <= topV + 0.0001f).ToList();
        var top = edge.Aggregate(Vector3.Zero, (sum, v) => sum + v) / edge.Count;

        var onBody = CardMeshNodeTransform();
        return (onBody * top, onBody * mesh.GetAabb().GetCenter(), onBody.Basis * Vector3.Up);
    });

    // The mesh lies in the card's face scene, which the card scene instances as its Face child.
    private static Transform3D CardMeshNodeTransform() =>
        StoredTransform(CardScenePath, CardFaceNode) * StoredTransform(CardFaceScenePath, CardMeshNode);

    private static Transform3D StoredTransform(string scene, string nodeName)
    {
        var state = GD.Load<PackedScene>(scene).GetState();
        for (var node = 0; node < state.GetNodeCount(); node++)
        {
            if (state.GetNodeName(node) != nodeName)
                continue;
            for (var property = 0; property < state.GetNodePropertyCount(node); property++)
            {
                if (state.GetNodePropertyName(node, property) == Node3D.PropertyName.Transform)
                    return state.GetNodePropertyValue(node, property).AsTransform3D();
            }

            return Transform3D.Identity;
        }

        throw new InvalidOperationException($"{scene} has no {nodeName}");
    }

    // The shelf's own collision boxes and its price tags' boxes, in the shelf's frame: what blocks a view or a card.
    private static List<ShelfBox> ShelfBoxes(CardShelf shelf)
    {
        var own = shelf.GetChildren().OfType<CollisionShape3D>()
            .Select(shape => new ShelfBox(shelf, shape.Name, ShapesIn(shelf, shape)));
        var tags = shelf.Pricings.SelectMany(tag => tag.GetChildren().OfType<CollisionShape3D>()
            .Select(shape => new ShelfBox(tag, $"{tag.Name}/{shape.Name}", ShapesIn(shelf, shape))));
        return own.Concat(tags).ToList();
    }

    // One collision box of a shelf, with the node it belongs to. The default value has no owner: nothing found.
    private readonly record struct ShelfBox(Node? Owner, string Name, Aabb Box)
    {
        private const float Clearance = 0.002f;

        public bool Overlaps(Aabb other) => Box.Grow(-Clearance).Intersects(other);

        // Whether the segment from the eye to the point passes through the box. The segment stops just short of the
        // point, so a box the point itself touches (a stand under a card's edge) does not count.
        public bool Blocks(Vector3 eye, Vector3 point)
        {
            var end = point - (point - eye).Normalized() * Clearance;
            var step = end - eye;
            float enter = 0f, leave = 1f;
            for (var axis = 0; axis < 3; axis++)
            {
                if (Mathf.Abs(step[axis]) < 1e-6f)
                {
                    if (eye[axis] < Box.Position[axis] || eye[axis] > Box.End[axis])
                        return false;
                    continue;
                }

                var a = (Box.Position[axis] - eye[axis]) / step[axis];
                var b = (Box.End[axis] - eye[axis]) / step[axis];
                enter = Mathf.Max(enter, Mathf.Min(a, b));
                leave = Mathf.Min(leave, Mathf.Max(a, b));
                if (enter > leave)
                    return false;
            }

            return true;
        }
    }

    private static float BaseThickness(MeshInstance3D standBase) => ((BoxMesh)standBase.Mesh).Size.Y;

    // The up-facing triangles of the shelf's loaded model, in the shelf's frame.
    private static List<Vector3[]> UpFaces(CardShelf shelf)
    {
        var slot = shelf.GetNode<ShopArtSlot>("Art");
        AssertBool(slot.ArtLoaded).OverrideFailureMessage($"{slot.ArtPath} did not load").IsTrue();
        var toShelf = shelf.GlobalTransform.AffineInverse();
        var faces = new List<Vector3[]>();
        var art = slot.GetNode("Art");
        foreach (var mesh in art.FindChildren("*", nameof(MeshInstance3D), true, false).OfType<MeshInstance3D>()
                     .Append(art as MeshInstance3D).OfType<MeshInstance3D>())
        {
            var toFrame = toShelf * mesh.GlobalTransform;
            var points = mesh.Mesh.GetFaces();
            for (var i = 0; i + 2 < points.Length; i += 3)
            {
                var a = toFrame * points[i];
                var b = toFrame * points[i + 1];
                var c = toFrame * points[i + 2];
                if ((c - a).Cross(b - a).Normalized().Y > UpFacing)
                    faces.Add([a, b, c]);
            }
        }

        AssertThat(faces.Count).IsGreater(0);
        return faces;
    }

    // The height of the highest up-facing face under the point (seen from above) that is not above it by more than
    // the tolerance, or null when no face lies under it.
    private static float? SurfaceBelow(List<Vector3[]> faces, Vector3 point)
    {
        float? best = null;
        foreach (var face in faces)
        {
            if (!Covers(face, point))
                continue;
            var y = (face[0].Y + face[1].Y + face[2].Y) / 3f;
            if (y <= point.Y + Tolerance && (best == null || y > best))
                best = y;
        }

        return best;
    }

    private static bool Covers(Vector3[] face, Vector3 point)
    {
        static float Side(Vector3 p, Vector3 a, Vector3 b) => (b.X - a.X) * (p.Z - a.Z) - (b.Z - a.Z) * (p.X - a.X);
        var d1 = Side(point, face[0], face[1]);
        var d2 = Side(point, face[1], face[2]);
        var d3 = Side(point, face[2], face[0]);
        var negative = d1 < 0 || d2 < 0 || d3 < 0;
        var positive = d1 > 0 || d2 > 0 || d3 > 0;
        return !(negative && positive);
    }

    // The shelf's own collision boxes, in its frame.
    private static Aabb ColliderBounds(CardShelf shelf)
    {
        var shapes = shelf.GetChildren().OfType<CollisionShape3D>().ToList();
        var box = ShapesIn(shelf, shapes[0]);
        foreach (var shape in shapes.Skip(1))
            box = box.Merge(ShapesIn(shelf, shape));
        return box;
    }

    // The body's own collision shapes, the part that physically rests on the shelf.
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

    private static Aabb MeshIn(Node3D frame, MeshInstance3D mesh) =>
        frame.GlobalTransform.AffineInverse() * mesh.GlobalTransform * mesh.Mesh.GetAabb();
}
