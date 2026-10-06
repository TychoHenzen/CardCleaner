using System.Threading.Tasks;
using CardCleaner.Scripts.Core.DependencyInjection;
using CardCleaner.Scripts.Features.Portal.Components;
using CardCleaner.Scripts.Features.Portal.Models;
using Godot;

namespace CardCleaner.Tests.Scenes;

/// <summary>
/// The way back from the workshop: a doorway on the cabinet room's south wall that is always open, needs no
/// special card, and lands the player in the backoffice walking away from the seam, so the seam never sends
/// them straight back.
/// </summary>
[TestSuite]
[RequireGodotRuntime]
public class ShopReturnSceneTest
{
    private const int SettleFrames = 10;

    // Workshop-local spots: in front of the return doorway, and inside its crossing depth.
    private static readonly Vector3 NearTheReturn = new(-3.8f, 0.95f, 3.0f);
    private static readonly Vector3 InTheReturnDoorway = new(-3.6f, 0.95f, 5.4f);

    private ShopSeamRig _rig = null!;
    private WallSeam _return = null!;
    private Node3D _workshop = null!;

    [BeforeTest]
    public async Task Setup()
    {
        _rig = await ShopSeamRig.Create();
        _workshop = _rig.Shop.GetNode<Node3D>("World/Workshop");
        _return = _workshop.GetNode<WallSeam>("ReturnSeam");
    }

    [AfterTest]
    public static void Teardown()
    {
        ServiceLocator.ResetForTesting();
    }

    [TestCase]
    [TestCategory("Unit")]
    public async Task ReturnDoorwayOpensWithoutASpecialCard()
    {
        await _rig.MovePlayer(_workshop.GlobalPosition + NearTheReturn);

        AssertThat(_return.Phase).IsEqual(SeamPhase.Open);
        AssertBool(_return.Door!.Visible).IsTrue();
    }

    [TestCase]
    [TestCategory("Unit")]
    public async Task SteppingIntoTheReturnLandsInTheBackofficeWalkingAwayFromTheSeam()
    {
        await _rig.MovePlayer(_workshop.GlobalPosition + NearTheReturn);
        _rig.Player.RotationDegrees = new Vector3(0f, 180f, 0f);
        await _rig.MovePlayer(_workshop.GlobalPosition + InTheReturnDoorway);

        AssertThat(_return.CrossingCount).IsEqual(1);
        AssertBool(_rig.Shop.GetNode<Area3D>("World/BackofficeZone").OverlapsBody(_rig.Player)).IsTrue();
        // Facing south (yaw 180) means walking on along +Z, away from the seam on the backoffice's north wall.
        AssertBool(Mathf.IsEqualApprox(Mathf.Abs(_rig.Player.GlobalRotationDegrees.Y), 180f)).IsTrue();
        AssertBool(_rig.Player.GlobalPosition.Z > _rig.Seam.GlobalPosition.Z + 1f).IsTrue();
    }

    [TestCase]
    [TestCategory("Unit")]
    public async Task ArrivingWithASpecialCardDoesNotSendThePlayerStraightBack()
    {
        await _rig.Hold(ShopSeamRig.Special());
        await _rig.MovePlayer(_workshop.GlobalPosition + NearTheReturn);
        await _rig.MovePlayer(_workshop.GlobalPosition + InTheReturnDoorway);
        for (var i = 0; i < SettleFrames; i++)
            await ISceneRunner.SyncPhysicsFrame;

        AssertThat(_return.CrossingCount).IsEqual(1);
        AssertThat(_rig.Seam.CrossingCount).IsEqual(0);
        AssertBool(_rig.Shop.GetNode<Area3D>("World/BackofficeZone").OverlapsBody(_rig.Player)).IsTrue();
    }

    [TestCase]
    [TestCategory("Unit")]
    public async Task ArrivingInTheWorkshopDoesNotSendThePlayerStraightBack()
    {
        await _rig.MovePlayer(ShopSeamRig.BackofficeMiddle);
        await _rig.Hold(ShopSeamRig.Special());
        await _rig.MovePlayer(ShopSeamRig.InTheDoorway);
        for (var i = 0; i < SettleFrames; i++)
            await ISceneRunner.SyncPhysicsFrame;

        AssertThat(_rig.Seam.CrossingCount).IsEqual(1);
        AssertThat(_return.CrossingCount).IsEqual(0);
        AssertBool(_rig.Player.GlobalPosition.X > _workshop.GlobalPosition.X - 20f).IsTrue();
    }
}
