using System.Linq;
using CardCleaner.Scripts.Features.Shop.Components;
using Godot;

namespace CardCleaner.Tests.Features.Shop.Components;

/// <summary>
/// The delivery footprint exemption: a solid shape under a node in <c>DeliveryMarker.NoFootprintGroup</c> does not
/// raise a stack, so a price tag on a shelf never lifts the layer that rests on the shelf.
/// </summary>
[TestSuite]
[RequireGodotRuntime]
public class DeliveryFootprintExemptionTest
{
    private const float Tolerance = 0.001f;
    private const float BoxTop = 1f;
    private const float TagTop = 1.05f;
    private const string ShelfScene = "res://Scenes/Shop/Items/Shelf.tscn";
    private const string ProductShelfScene = "res://Scenes/Shop/Items/ProductShelf.tscn";
    private const string StorageRackScene = "res://Scenes/Shop/Items/StorageRack.tscn";
    private const string PriceTagScene = "res://Scenes/Shop/Items/PriceTag.tscn";
    private static readonly Vector2 Cell = new(3f, 3f);

    private Node3D _world = null!;

    [BeforeTest]
    public void Setup()
    {
        _world = AddNode(new Node3D());
    }

    [TestCase]
    [TestCategory("Unit")]
    public void ATaggedItemInTheExemptGroupDoesNotRaiseTheStack()
    {
        var marker = NewMarker();
        var lift = LiftOnTopOf(marker, NewItem(true, groups: DeliveryMarker.NoFootprintGroup));

        AssertBool(Mathf.Abs(lift - (BoxTop + DeliveryMarker.StackGap)) < Tolerance).IsTrue();
    }

    [TestCase]
    [TestCategory("Unit")]
    public void AnUntaggedGroupItemStillCountsItsSolidTag()
    {
        var marker = NewMarker();
        var lift = LiftOnTopOf(marker, NewItem(true));

        AssertBool(Mathf.Abs(lift - (TagTop + DeliveryMarker.StackGap)) < Tolerance).IsTrue();
    }

    [TestCase]
    [TestCategory("Unit")]
    public void AnExemptShapeInsideAnExemptNodeIsAlsoIgnored()
    {
        var marker = NewMarker();
        var lift = LiftOnTopOf(marker, NewItem(true, groupOnRoot: true, groups: DeliveryMarker.NoFootprintGroup));

        AssertBool(Mathf.Abs(lift - (BoxTop + DeliveryMarker.StackGap)) < Tolerance).IsTrue();
    }

    [TestCase]
    [TestCategory("Unit")]
    public void TheExemptionBeatsTheFootprintGroup()
    {
        var marker = NewMarker();
        var lift = LiftOnTopOf(
            marker,
            NewItem(true, areaTag: true, groups: [DeliveryMarker.NoFootprintGroup, DeliveryMarker.FootprintGroup]));

        AssertBool(Mathf.Abs(lift - (BoxTop + DeliveryMarker.StackGap)) < Tolerance).IsTrue();
    }

    [TestCase]
    [TestCategory("Unit")]
    public void AFootprintGroupTriggerWithoutTheExemptionStillCounts()
    {
        var marker = NewMarker();
        var lift = LiftOnTopOf(marker, NewItem(true, areaTag: true, groups: DeliveryMarker.FootprintGroup));

        AssertBool(Mathf.Abs(lift - (TagTop + DeliveryMarker.StackGap)) < Tolerance).IsTrue();
    }

    [TestCase]
    [TestCategory("Unit")]
    public void PriceTagSceneJoinsTheExemptGroup()
    {
        var tag = GD.Load<PackedScene>(PriceTagScene).Instantiate<Node>();

        AssertBool(tag.IsInGroup(DeliveryMarker.NoFootprintGroup)).IsTrue();
        tag.Free();
    }

    [TestCase]
    [TestCategory("Unit")]
    public void AShelfWithTagsHasTheFootprintOfAShelfWithout()
    {
        foreach (var path in new[] { ShelfScene, ProductShelfScene, StorageRackScene })
        {
            var shelf = GD.Load<PackedScene>(path).Instantiate<CardShelf>();
            _world.AddChild(shelf);

            var withTags = LiftOnTopOf(NewMarker(), shelf);
            var pricings = shelf.Pricings.ToArray();
            AssertBool(pricings.Length > 0).IsTrue();
            foreach (var pricing in pricings)
            {
                shelf.RemoveChild(pricing);
                pricing.Free();
            }

            AssertBool(shelf.Pricings.Any()).IsFalse();
            var withoutTags = LiftOnTopOf(NewMarker(), shelf);

            AssertBool(Mathf.Abs(withTags - withoutTags) < Tolerance).IsTrue();
            AssertBool(withTags >= DeliveryMarker.StackGap).IsTrue();
        }
    }

    private DeliveryMarker NewMarker()
    {
        var marker = new DeliveryMarker();
        _world.AddChild(marker);
        return marker;
    }

    /// <summary>
    /// Delivers <paramref name="item" /> onto the marker's single slot, then returns the lift a plain probe gets on top
    /// of it. The item joins the world only when it is not in the tree yet.
    /// </summary>
    private float LiftOnTopOf(DeliveryMarker marker, Node3D item)
    {
        if (!item.IsInsideTree())
            _world.AddChild(item);
        marker.Track(item);

        var probe = NewItem(false);
        var placement = marker.FindPlacement(1, _ => Vector3.Zero, Cell, probe);
        probe.Free();
        return placement!.Value.Lift;
    }

    /// <summary>
    /// A delivered item: a solid box of one metre, and optionally a price-tag-sized shape at its top. The tag is
    /// solid unless <paramref name="areaTag" /> makes it a trigger. <paramref name="groupOnRoot" /> puts the tag's
    /// shape under a plain node, and <paramref name="groups" /> go on the tag body, or on that node when it is set.
    /// </summary>
    private static Node3D NewItem(bool tagged, bool areaTag = false, bool groupOnRoot = false, params string[] groups)
    {
        var item = new Node3D();
        var box = new StaticBody3D { Name = "Box", Position = new Vector3(0f, 0.5f, 0f) };
        box.AddChild(new CollisionShape3D { Shape = new BoxShape3D { Size = Vector3.One } });
        item.AddChild(box);
        if (!tagged)
            return item;

        Node3D tagParent = item;
        if (groupOnRoot)
        {
            tagParent = new Node3D { Name = "TagRoot" };
            item.AddChild(tagParent);
        }

        Node3D tagBody = areaTag ? new Area3D() : new StaticBody3D();
        tagBody.Name = "Tag";
        tagBody.Position = new Vector3(0f, 1f, 0f);
        tagBody.AddChild(new CollisionShape3D { Shape = new BoxShape3D { Size = new Vector3(0.3f, 0.1f, 0.04f) } });
        tagParent.AddChild(tagBody);

        foreach (var group in groups)
            (groupOnRoot ? tagParent : tagBody).AddToGroup(group);
        return item;
    }
}
