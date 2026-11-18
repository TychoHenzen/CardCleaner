using System;
using CardCleaner.Scripts.Features.Shop.Models;

namespace CardCleaner.Tests.Features.Shop.Models;

[TestSuite]
public class FigureMotionTest
{
    private const float Tolerance = 1e-5f;

    [TestCase(0f, FigureClip.Idle)]
    [TestCase(0.1f, FigureClip.Idle)]
    [TestCase(FigureMotion.WalkingSpeed, FigureClip.Walk)]
    [TestCase(1.6f, FigureClip.Walk)]
    [TestCategory("Unit")]
    public static void APersonWalksOnlyAtWalkingSpeed(float speed, FigureClip loop)
    {
        AssertThat(FigureMotion.LoopFor(speed)).IsEqual(loop);
    }

    [TestCase(FigureClip.Idle, true)]
    [TestCase(FigureClip.Walk, true)]
    [TestCase(FigureClip.PickUp, false)]
    [TestCase(FigureClip.HandOver, false)]
    [TestCase(FigureClip.Refuse, false)]
    [TestCategory("Unit")]
    public static void OnlyIdleAndWalkLoop(FigureClip clip, bool loops)
    {
        AssertBool(FigureMotion.Loops(clip)).IsEqual(loops);
    }

    // Godot's yaw turns -Z: yaw y sends (0, 0, -1) to (-sin y, 0, -cos y).
    [TestCase(0f, -1f)]
    [TestCase(0f, 1f)]
    [TestCase(1f, 0f)]
    [TestCase(-1f, 0f)]
    [TestCase(0.6f, -0.8f)]
    [TestCategory("Unit")]
    public static void TheYawTowardADirectionTurnsMinusZOntoIt(float x, float z)
    {
        var yaw = FigureMotion.YawToward(x, z);

        AssertFloat(-MathF.Sin(yaw)).IsEqualApprox(x, Tolerance);
        AssertFloat(-MathF.Cos(yaw)).IsEqualApprox(z, Tolerance);
    }

    [TestCase]
    [TestCategory("Unit")]
    public static void ATurnStopsAtItsTarget()
    {
        AssertFloat(FigureMotion.TurnToward(0f, 0.1f, 0.5f)).IsEqualApprox(0.1f, Tolerance);
    }

    [TestCase]
    [TestCategory("Unit")]
    public static void ATurnMovesAtMostOneStep()
    {
        AssertFloat(FigureMotion.TurnToward(0f, 2f, 0.5f)).IsEqualApprox(0.5f, Tolerance);
        AssertFloat(FigureMotion.TurnToward(0f, -2f, 0.5f)).IsEqualApprox(-0.5f, Tolerance);
    }

    [TestCase]
    [TestCategory("Unit")]
    public static void ATurnGoesTheShortWayAcrossTheBackOfTheCircle()
    {
        // From just short of +pi to just past -pi is a small step through pi, not almost a full turn back.
        AssertFloat(FigureMotion.TurnToward(3.0f, -3.0f, 0.1f)).IsEqualApprox(3.1f, Tolerance);
    }

    [TestCase]
    [TestCategory("Unit")]
    public static void ATurnAlwaysEndsInsideMinusPiToPi()
    {
        for (var target = -10f; target <= 10f; target += 0.37f)
        {
            var turned = FigureMotion.TurnToward(5f, target, 100f);
            AssertFloat(turned).IsBetween(-MathF.PI, MathF.PI);
        }
    }

    [TestCase(1.6f, 1.6f, 1f)]
    [TestCase(0.8f, 1.6f, 0.5f)]
    [TestCase(1.6f, 0f, 1f)]
    [TestCategory("Unit")]
    public static void TheWalkClipPlaysAtTheSpeedThatKeepsItsFeetDown(float speed, float clipSpeed, float playback)
    {
        AssertFloat(FigureMotion.WalkPlayback(speed, clipSpeed)).IsEqualApprox(playback, Tolerance);
    }
}
