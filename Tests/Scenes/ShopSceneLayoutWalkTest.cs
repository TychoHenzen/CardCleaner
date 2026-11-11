using Godot;

namespace CardCleaner.Tests.Scenes;

/// <summary>
/// Where the player can and cannot walk in the graybox shop: the storage room, the right arm, the road door and the
/// locked expansion lot, checked by sweeping the player capsule through the level.
/// </summary>
[TestSuite]
[RequireGodotRuntime]
public class ShopSceneLayoutWalkTest : ShopSceneContractTestBase
{
    [TestCase]
    [TestCategory("Unit")]
    public void StorageAndBackofficeAreOneRoom()
    {
        AssertThat(_shop.GetNodeOrNull("World/Walls/BackRoomDivider")).IsNull();
        foreach (var z in new[] { -5f, -3f, -1f })
        {
            AssertBool(_probe.ProbeSweep(new Vector3(4f, 1f, z), new Vector3(12f, 1f, z))).IsTrue();
        }

        AssertThat(_shop.GetNode<Node3D>("World/BackofficeZone").Position).IsEqual(new Vector3(12f, 2f, -4f));
        AssertThat(MarkerPosition("PcLocation")).IsEqual(new Vector3(10f, 0f, -6.9f));
        AssertThat(MarkerPosition("SeamLocation")).IsEqual(new Vector3(12f, 1.5f, -7.8f));
        AssertThat(_shop.GetNode<Node3D>("World/Backoffice/Floor").GlobalPosition)
            .IsEqual(new Vector3(12f, -0.1f, -4f));
    }

    [TestCase]
    [TestCategory("Unit")]
    public void RightArmWrapsTheStorefront()
    {
        AssertBool(IsInsideArea("RightStorage", new Vector3(23f, 0f, -7f))).IsTrue();
        AssertBool(IsInsideArea("RightStorage", new Vector3(17f, 0f, 11f))).IsTrue();
        AssertThat(FloorShape("RightStorage")!.GlobalPosition).IsEqual(new Vector3(20f, -0.1f, 2f));

        // Opening in the old wall at x = 16, z -8..0.
        AssertBool(_probe.ProbeSweep(new Vector3(12f, 1f, -4f), new Vector3(20f, 1f, -4f))).IsTrue();
        // The room continues along the storefront's right side.
        AssertBool(_probe.ProbeSweep(new Vector3(20f, 1f, -4f), new Vector3(20f, 1f, 6f))).IsTrue();
        // The wall at x = 16 for z 0..12 stays.
        AssertBool(_probe.ProbeSweep(new Vector3(14f, 1f, 6f), new Vector3(15f, 1f, 6f))).IsTrue();
        AssertBool(_probe.ProbeSweep(new Vector3(14f, 1f, 6f), new Vector3(18f, 1f, 6f))).IsFalse();
        // The outer right wall at x = 24.
        AssertBool(_probe.ProbeSweep(new Vector3(22f, 1f, 6f), new Vector3(23.6f, 1f, 6f))).IsFalse();
        // The outer back wall at z = -8.
        AssertBool(_probe.ProbeSweep(new Vector3(20f, 1f, -6f), new Vector3(20f, 1f, -7.6f))).IsFalse();
    }

    [TestCase]
    [TestCategory("Unit")]
    public void RoadDoorJoinsStreetAndRightArm()
    {
        var street = _shop.GetNode<CollisionShape3D>("World/Street/Floor/CollisionShape3D");
        AssertThat(((BoxShape3D)street.Shape).Size).IsEqual(new Vector3(40f, 0.2f, 4f));
        AssertThat(street.GlobalPosition).IsEqual(new Vector3(4f, -0.1f, 14f));

        // Street through the road door, then to the delivery point.
        AssertBool(_probe.ProbeSweep(new Vector3(RoadDoorX, 1f, 14f), new Vector3(RoadDoorX, 1f, 9f))).IsTrue();
        AssertBool(_probe.ProbeSweep(new Vector3(RoadDoorX, 1f, 9f), new Vector3(20f, 1f, 9f))).IsTrue();
        // Solid wall beside the door.
        AssertBool(_probe.ProbeSweep(new Vector3(21f, 1f, 14f), new Vector3(21f, 1f, 9f))).IsFalse();
        // The front door still joins the street and the storefront.
        AssertBool(_probe.ProbeSweep(new Vector3(6.05f, 1f, 14f), new Vector3(6.05f, 1f, 9f))).IsTrue();
        // The street runs along the whole front.
        AssertBool(_probe.ProbeSweep(new Vector3(-15f, 1f, 14f), new Vector3(23f, 1f, 14f))).IsTrue();
        // The street is walled on its building side where no building stands.
        AssertBool(_probe.ProbeSweep(new Vector3(-12f, 1f, 14f), new Vector3(-12f, 1f, 12.4f))).IsFalse();

        var delivery = MarkerPosition("DeliveryPoint");
        AssertThat(delivery).IsEqual(new Vector3(20f, 0f, 9f));
        AssertBool(IsInsideArea("RightStorage", delivery)).IsTrue();
        AssertBool(delivery.Z is > 8f and < 12f).IsTrue();
        AssertBool(Mathf.Abs(delivery.X - RoadDoorX) < 2f).IsTrue();
    }

    [TestCase]
    [TestCategory("Unit")]
    public void ExpansionLotIsLockedLeftOfTheStorefront()
    {
        // Storefront side, stopped by LeftWall at x = 0.
        AssertBool(_probe.ProbeSweep(new Vector3(2f, 1f, 6f), new Vector3(-4f, 1f, 6f))).IsFalse();
        // Storage side, stopped by LeftWall.
        AssertBool(_probe.ProbeSweep(new Vector3(2f, 1f, -4f), new Vector3(-4f, 1f, -4f))).IsFalse();
        // Lot to street, stopped by the lot's street-side wall.
        AssertBool(_probe.ProbeSweep(new Vector3(-4f, 1f, 6f), new Vector3(-4f, 1f, 14f))).IsFalse();
        // Storefront part to storage part, stopped by the wall at z = 0.
        AssertBool(_probe.ProbeSweep(new Vector3(-4f, 1f, 6f), new Vector3(-4f, 1f, -4f))).IsFalse();
        // Lot back wall at z = -8.
        AssertBool(_probe.ProbeSweep(new Vector3(-4f, 1f, -4f), new Vector3(-4f, 1f, -7.6f))).IsFalse();
        // Lot outer left wall at x = -8.
        AssertBool(_probe.ProbeSweep(new Vector3(-4f, 1f, 6f), new Vector3(-7.6f, 1f, 6f))).IsFalse();

        AssertThat(_shop.GetNodeOrNull<StaticBody3D>("World/Walls/LeftWall")).IsNotNull();
        AssertThat(MarkerPosition("ExpansionLot1")).IsEqual(new Vector3(-4f, 0f, 6f));
        AssertBool(IsInsideArea("ExpansionLot", MarkerPosition("ExpansionLot1"))).IsTrue();
        AssertThat(FloorShape("ExpansionLot")!.GlobalPosition).IsEqual(new Vector3(-4f, -0.1f, 2f));
        AssertBool(FloorShape("ExpansionLot")!.GlobalPosition.X < FloorShape("Storefront")!.GlobalPosition.X).IsTrue();
    }
}
