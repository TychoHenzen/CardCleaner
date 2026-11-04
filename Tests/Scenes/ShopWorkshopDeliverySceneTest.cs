using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using CardCleaner.Scripts.Features.Shop.Services;
using CardCleaner.Tests.TestUtilities;
using Godot;

namespace CardCleaner.Tests.Scenes;

/// <summary>
/// Geometry of the workshop terminal's deliveries inside the shop scene: they never overlap workshop geometry and
/// use a single ground layer, so tall items never reach the ceiling.
/// </summary>
[TestSuite]
[RequireGodotRuntime]
public class ShopWorkshopDeliverySceneTest
{
    private const float WidestItem = 1.3f;
    private const float TallestItem = 2.24f;
    private const float ProbeInset = 0.02f;
    private const int SettleFrames = 300;
    private const float UprightDot = 0.95f;
    private const float PadReach = 3.5f;
    private const float RestTolerance = 0.1f;
    private const float ClearanceBottom = 0.02f;
    private const float ClearanceTop = 2.2f;

    // A cabinet with its holders spans two columns, so the second goes to the first free cell, the far column.
    private static readonly int[] CabinetSlots = [0, 2];

    private WorkshopDeliveryRig _rig = null!;

    [BeforeTest]
    public async Task Setup()
    {
        _rig = new WorkshopDeliveryRig();
        AddNode(_rig.Shop);
        await WorkshopDeliveryRig.SettleAsync();
    }

    [AfterTest]
    public static void Teardown()
    {
        TestServices.Reset();
    }

    [TestCase]
    [TestCategory("Unit")]
    public void TerminalDeliversToItsOwnMarkerNotTheShopDefault()
    {
        AssertBool(ReferenceEquals(_rig.Terminal.DeliveryPoint, _rig.Delivery)).IsTrue();
        AssertBool(_rig.Delivery.GlobalPosition.DistanceTo(_rig.ShopDelivery.GlobalPosition) > 20f).IsTrue();
    }

    [TestCase]
    [TestCategory("Unit")]
    public void EveryDeliverySlotIsFreeOfWorkshopGeometryForTheTallestItem()
    {
        var space = _rig.Shop.GetWorld3D().DirectSpaceState;
        var box = new BoxShape3D { Size = new Vector3(WidestItem, TallestItem - ProbeInset * 2f, WidestItem) };

        for (var slot = 0; slot < WorkshopDeliveryRig.Capacity; slot++)
        {
            var feet = _rig.Delivery.GlobalPosition + OrderingService.SlotOffset(slot);
            var query = new PhysicsShapeQueryParameters3D
            {
                Shape = box,
                Transform = new Transform3D(Basis.Identity, feet + Vector3.Up * (TallestItem / 2f)),
                CollideWithBodies = true,
                CollideWithAreas = false
            };

            AssertThat(space.IntersectShape(query, 1).Count).IsEqual(0);
        }
    }

    [TestCase]
    [TestCategory("Unit")]
    public void DeliveryClearanceIsFreeOfWorkshopGeometry()
    {
        var clearance = _rig.Delivery.FloorClearance;
        AssertBool(clearance.HasArea())
            .OverrideFailureMessage($"the delivery point declares no floor clearance: {clearance}")
            .IsTrue();

        var space = _rig.Shop.GetWorld3D().DirectSpaceState;
        var height = ClearanceTop - ClearanceBottom;
        var centre = clearance.GetCenter();
        // The walls stand on the clearance's edges, so a box exactly that size only touches them. Inset it on X and Z.
        var size = new Vector3(clearance.Size.X - 2f * ProbeInset, height, clearance.Size.Y - 2f * ProbeInset);
        var query = new PhysicsShapeQueryParameters3D
        {
            Shape = new BoxShape3D { Size = size },
            Transform = new Transform3D(Basis.Identity,
                _rig.Delivery.GlobalPosition + new Vector3(centre.X, ClearanceBottom + height / 2f, centre.Y)),
            CollideWithBodies = true,
            CollideWithAreas = false
        };

        var hits = space.IntersectShape(query, 64);
        var colliders = string.Join(", ",
            hits.Select(hit => hit["collider"].Obj is Node node ? node.GetPath().ToString() : "?"));
        AssertThat(hits.Count)
            .OverrideFailureMessage($"bodies inside the delivery clearance: {colliders}")
            .IsEqual(0);
    }

    [TestCase]
    [TestCategory("Unit")]
    public void TallestItemInTheSingleLayerClearsTheCeiling()
    {
        var ceiling = _rig.Workshop.GetNode<StaticBody3D>("WorkshopGrid/Ceiling");
        var shape = (BoxShape3D)ceiling.GetNode<CollisionShape3D>("CollisionShape3D").Shape;
        var underside = ceiling.GlobalPosition.Y - shape.Size.Y / 2f;

        AssertBool(_rig.Delivery.GlobalPosition.Y + TallestItem < underside).IsTrue();
    }

    [TestCase]
    [TestCategory("Unit")]
    public void OrderedCabinetsFillTheGridTwoAtATimeAndNeverReachEachOthersHolders()
    {
        _rig.Money.Add(WorkshopDeliveryRig.TopUp);
        _rig.Terminal.Interact();

        var cabinets = _rig.Presses.OrderUntilFull(_rig.Terminal, _rig.IndexOf("arcade_cabinet"));

        AssertThat(string.Join(", ", DeliveredFootprints.OverlapsBetween(cabinets))).IsEmpty();
        var offsets = string.Join(", ", cabinets.Select(cabinet => cabinet.GlobalPosition - _rig.Delivery.GlobalPosition));
        AssertThat(cabinets.Count)
            .OverrideFailureMessage($"delivered {cabinets.Count} cabinets at offsets {offsets}")
            .IsEqual(CabinetSlots.Length);
        for (var i = 0; i < cabinets.Count; i++)
        {
            var expected = _rig.Delivery.GlobalPosition + OrderingService.SlotOffset(CabinetSlots[i]);
            AssertBool(cabinets[i].GlobalPosition.IsEqualApprox(expected))
                .OverrideFailureMessage(
                    $"cabinet {i} is at {cabinets[i].GlobalPosition}, expected slot {CabinetSlots[i]}")
                .IsTrue();
        }

        foreach (var cabinet in cabinets)
            AssertThat(string.Join(", ", ProbeAgainstWorkshop(cabinet))).IsEmpty();
    }

    [TestCase]
    [TestCategory("Unit")]
    public async Task OrderedCabinetSettlesUprightOnThePadWithItsHolders()
    {
        _rig.Money.Add(WorkshopDeliveryRig.TopUp);
        _rig.Terminal.Interact();
        var cabinet = (RigidBody3D)_rig.Presses.PressForDelivery(_rig.Terminal, _rig.IndexOf("arcade_cabinet"));
        _rig.Terminal.Close();
        for (var i = 0; i < SettleFrames; i++)
            await ISceneRunner.SyncPhysicsFrame;

        var offset = cabinet.GlobalPosition - _rig.Delivery.GlobalPosition;
        AssertBool(cabinet.GlobalBasis.Y.Dot(Vector3.Up) > UprightDot)
            .OverrideFailureMessage($"cabinet tipped: up {cabinet.GlobalBasis.Y}")
            .IsTrue();
        AssertBool(new Vector2(offset.X, offset.Z).Length() < PadReach && Mathf.Abs(offset.Y) < RestTolerance)
            .OverrideFailureMessage($"cabinet came to rest at {offset} from the pad")
            .IsTrue();
        AssertThat(cabinet.GetNodeOrNull("Assembly/DeckSlot")).IsNotNull();
        AssertThat(cabinet.GetNodeOrNull("Assembly/CardSlot")).IsNotNull();
    }

    /// <summary>
    /// Probes every enabled collision shape of a delivered item in its place against the workshop's bodies, triggers
    /// included, since the item carries its holders. The item's own bodies are excluded, and each shape is lifted by
    /// the probe inset so the floor it stands on is not reported. A report means a shape of the item is inside
    /// workshop geometry.
    /// </summary>
    private List<string> ProbeAgainstWorkshop(Node3D item)
    {
        var space = _rig.Shop.GetWorld3D().DirectSpaceState;
        var own = new Godot.Collections.Array<Rid>();
        if (item is CollisionObject3D root)
            own.Add(root.GetRid());
        foreach (var body in item.FindChildren("*", nameof(CollisionObject3D), true, false).OfType<CollisionObject3D>())
            own.Add(body.GetRid());

        var hits = new List<string>();
        foreach (var collider in item.FindChildren("*", nameof(CollisionShape3D), true, false)
                     .OfType<CollisionShape3D>()
                     .Where(c => c is { Disabled: false, Shape: not null }))
        {
            var query = new PhysicsShapeQueryParameters3D
            {
                Shape = collider.Shape,
                Transform = new Transform3D(collider.GlobalBasis, collider.GlobalPosition + Vector3.Up * ProbeInset),
                CollideWithBodies = true,
                CollideWithAreas = false,
                Exclude = own
            };
            if (space.IntersectShape(query, 1).Count > 0)
                hits.Add($"{item.GetPathTo(collider)} is inside workshop geometry");
        }

        return hits;
    }
}
