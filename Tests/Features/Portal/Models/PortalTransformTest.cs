using CardCleaner.Scripts.Features.Portal.Models;
using Godot;

namespace CardCleaner.Tests.Features.Portal.Models;

[TestSuite]
[RequireGodotRuntime]
public class PortalTransformTest
{
    private const float Tolerance = 0.001f;

    private static Transform3D At(Vector3 origin, float yawDegrees)
    {
        return new Transform3D(Basis.FromEuler(new Vector3(0f, Mathf.DegToRad(yawDegrees), 0f)), origin);
    }

    private static void AssertNear(Vector3 actual, Vector3 expected)
    {
        AssertBool(actual.DistanceTo(expected) < Tolerance).IsTrue();
    }

    [TestCase]
    [TestCategory("Unit")]
    public static void PlayerAtTheEntryLandsAtTheExit()
    {
        var entry = At(new Vector3(12f, 1.5f, -7.8f), 0f);
        var exit = At(new Vector3(60f, 1.5f, -4f), 0f);

        var result = PortalTransform.Map(entry, entry, exit);

        AssertNear(result.Origin, exit.Origin);
    }

    [TestCase]
    [TestCategory("Unit")]
    public static void OffsetFromTheDoorwayIsPreservedWhenMarkersShareAFacing()
    {
        var entry = At(new Vector3(12f, 1.5f, -7.8f), 0f);
        var exit = At(new Vector3(60f, 1.5f, -4f), 0f);
        var player = At(new Vector3(12.3f, 0.9f, -7.2f), 25f);

        var result = PortalTransform.Map(player, entry, exit);

        AssertNear(result.Origin, new Vector3(60.3f, 0.9f, -3.4f));
        AssertNear(-result.Basis.Z, -player.Basis.Z);
    }

    [TestCase]
    [TestCategory("Unit")]
    public static void ExitTurnedNinetyDegreesTurnsOffsetAndFacingByTheSameAmount()
    {
        var entry = At(new Vector3(0f, 1.5f, 0f), 0f);
        var exit = At(new Vector3(10f, 1.5f, 10f), 90f);
        var player = At(new Vector3(0f, 1.5f, 1f), 0f);

        var result = PortalTransform.Map(player, entry, exit);

        // One metre in front of the entry (+Z) is one metre in front of the exit, which now faces -X.
        AssertNear(result.Origin, new Vector3(11f, 1.5f, 10f));
        AssertNear(-result.Basis.Z, new Vector3(-1f, 0f, 0f));
    }

    [TestCase]
    [TestCategory("Unit")]
    public static void PitchAndHeightOffsetAreNotChanged()
    {
        var entry = At(new Vector3(0f, 1.5f, 0f), 0f);
        var exit = At(new Vector3(5f, 3f, 5f), 180f);
        var player = At(new Vector3(0f, 0.9f, 0f), 0f);

        var result = PortalTransform.Map(player, entry, exit);

        AssertThat(Mathf.IsEqualApprox(result.Origin.Y, 2.4f)).IsTrue();
        AssertNear(result.Basis.Y, Vector3.Up);
    }

    [TestCase]
    [TestCategory("Unit")]
    public static void VelocityTurnsWithTheBody()
    {
        var entry = At(Vector3.Zero, 0f);
        var exit = At(new Vector3(5f, 0f, 5f), 90f);

        var result = PortalTransform.MapVelocity(new Vector3(0f, -2f, -3f), entry, exit);

        AssertNear(result, new Vector3(-3f, -2f, 0f));
    }

    [TestCase]
    [TestCategory("Unit")]
    public static void SteppingIntoTheDoorwayCrossesButStandingBackOrBesideDoesNot()
    {
        var doorway = At(new Vector3(12f, 1.5f, -7.8f), 0f);

        AssertThat(PortalTransform.HasCrossed(doorway, new Vector3(12.2f, 0.9f, -7.4f), 0.8f, 0.6f)).IsTrue();
        AssertThat(PortalTransform.HasCrossed(doorway, new Vector3(12f, 0.9f, -5f), 0.8f, 0.6f)).IsFalse();
        AssertThat(PortalTransform.HasCrossed(doorway, new Vector3(13.5f, 0.9f, -7.4f), 0.8f, 0.6f)).IsFalse();
    }

    [TestCase]
    [TestCategory("Unit")]
    public static void CrossingFollowsTheDoorwayFacing()
    {
        var doorway = At(Vector3.Zero, 90f);

        // Yawed 90 degrees, the room side (local +Z) points along world +X.
        AssertThat(PortalTransform.HasCrossed(doorway, new Vector3(0.3f, 0f, 0f), 0.8f, 0.6f)).IsTrue();
        AssertThat(PortalTransform.HasCrossed(doorway, new Vector3(0f, 0f, 5f), 0.8f, 0.6f)).IsFalse();
    }

    [TestCase]
    [TestCategory("Unit")]
    public static void CandidatesRiseInStepsFromTheMappedSpot()
    {
        var landing = new Vector3(60f, 0.9f, -4f);

        AssertNear(PortalTransform.Candidate(landing, 0, 0.25f), landing);
        AssertNear(PortalTransform.Candidate(landing, 3, 0.25f), landing + new Vector3(0f, 0.75f, 0f));
    }
}
