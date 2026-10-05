using System.Threading.Tasks;
using CardCleaner.Scripts.Core.DependencyInjection;
using CardCleaner.Scripts.Features.Card.Models;
using CardCleaner.Scripts.Features.Portal.Components;
using CardCleaner.Scripts.Features.Portal.Models;
using Godot;

namespace CardCleaner.Tests.Scenes;

/// <summary>
/// Odd states of the backoffice wall seam in the shop scene: one crossing only, dropping the card closes
/// the seam, a player pressed against the wall still crosses, and a blocked landing is refused.
/// </summary>
[TestSuite]
[RequireGodotRuntime]
public class ShopSeamReliabilitySceneTest
{

    private ShopSeamRig _rig = null!;

    [BeforeTest]
    public async Task Setup()
    {
        _rig = await ShopSeamRig.Create();
    }

    [AfterTest]
    public static void Teardown()
    {
        ServiceLocator.ResetForTesting();
    }

    [TestCase]
    [TestCategory("Unit")]
    public async Task CrossingOnceDoesNotTeleportAgainAndTheSeamCloses()
    {
        await _rig.MovePlayer(ShopSeamRig.BackofficeMiddle);
        await _rig.Hold(ShopSeamRig.Special());
        await _rig.MovePlayer(ShopSeamRig.InTheDoorway);
        await ShopSeamRig.Settle();
        await ShopSeamRig.Settle();

        AssertThat(_rig.Seam.CrossingCount).IsEqual(1);
        AssertThat(_rig.Seam.Phase).IsEqual(SeamPhase.Hidden);
        AssertBool(_rig.Seam.HumRequested).IsFalse();
    }

    [TestCase]
    [TestCategory("Unit")]
    public async Task DroppingTheCardWhileTheSeamIsOpenClosesIt()
    {
        await _rig.MovePlayer(ShopSeamRig.BackofficeMiddle);
        var card = await _rig.Hold(ShopSeamRig.Special());
        await _rig.MovePlayer(ShopSeamRig.NearTheSeam);
        AssertThat(_rig.Seam.Phase).IsEqual(SeamPhase.Open);

        await _rig.Drop(card);

        AssertThat(_rig.Seam.Phase).IsEqual(SeamPhase.Hidden);
        AssertBool(_rig.Seam.HumRequested).IsFalse();
    }

    [TestCase]
    [TestCategory("Unit")]
    public async Task PlayerPressedAgainstTheWallStillCountsAsCrossing()
    {
        await _rig.MovePlayer(ShopSeamRig.BackofficeMiddle);
        await _rig.Hold(ShopSeamRig.Special());
        // The wall face is at z=-7.85 and the player capsule radius is 0.5, so this is as close as it gets.
        await _rig.MovePlayer(new Vector3(12f, 0.95f, -7.34f));

        AssertThat(_rig.Seam.CrossingCount).IsEqual(1);
    }

    [TestCase]
    [TestCategory("Unit")]
    public async Task DroppingTheSpecialCardOfAMixedHandClosesTheSeam()
    {
        await _rig.MovePlayer(ShopSeamRig.BackofficeMiddle);
        var special = await _rig.Hold(ShopSeamRig.Special());
        await _rig.Hold(new CardSignature());
        AssertThat(_rig.Seam.Phase).IsEqual(SeamPhase.Glowing);

        await _rig.Drop(special);

        AssertThat(_rig.Seam.Phase).IsEqual(SeamPhase.Hidden);
    }

    [TestCase]
    [TestCategory("Unit")]
    public async Task BlockedLandingRefusesTheCrossingAndKeepsThePlayerInTheShop()
    {
        var blocker = new StaticBody3D { Position = _rig.WorkshopEntry.GlobalPosition };
        blocker.AddChild(new CollisionShape3D { Shape = new BoxShape3D { Size = new Vector3(4f, 6f, 4f) } });
        _rig.Shop.AddChild(blocker);
        await _rig.MovePlayer(ShopSeamRig.BackofficeMiddle);
        await _rig.Hold(ShopSeamRig.Special());
        await _rig.MovePlayer(ShopSeamRig.InTheDoorway);

        AssertThat(_rig.Seam.CrossingCount).IsEqual(0);
        AssertBool(_rig.Player.GlobalPosition.X < 20f).IsTrue();
    }
}
