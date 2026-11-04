using System;
using CardCleaner.Scripts.Features.Shop.Components;
using Godot;

namespace CardCleaner.Tests.Features.Shop.Components;

[TestSuite]
[RequireGodotRuntime]
public class FurnitureAimTest
{
    private const float Tolerance = 0.001f;

    [TestCase]
    [TestCategory("Unit")]
    public static void StraightDownHitsTheOriginColumn()
    {
        bool hit = FurnitureAim.TryFloorPoint(new Vector3(8f, 2f, 2f), new Vector3(0f, -1f, 0f), out float x,
            out float z);

        AssertBool(hit).IsTrue();
        AssertBool(MathF.Abs(x - 8f) <= Tolerance).IsTrue();
        AssertBool(MathF.Abs(z - 2f) <= Tolerance).IsTrue();
    }

    [TestCase]
    [TestCategory("Unit")]
    public static void DiagonalDownHitsTheSlantedFloorPoint()
    {
        var direction = new Vector3(0f, -1f, -1f).Normalized();

        bool hit = FurnitureAim.TryFloorPoint(new Vector3(8f, 2f, 2f), direction, out float x, out float z);

        AssertBool(hit).IsTrue();
        AssertBool(MathF.Abs(x - 8f) <= Tolerance).IsTrue();
        AssertBool(MathF.Abs(z) <= Tolerance).IsTrue();
    }

    [TestCase]
    [TestCategory("Unit")]
    public static void LevelRayMeetsNoFloor()
    {
        bool hit = FurnitureAim.TryFloorPoint(new Vector3(8f, 2f, 2f), new Vector3(0f, 0f, -1f), out _, out _);

        AssertBool(hit).IsFalse();
    }

    [TestCase]
    [TestCategory("Unit")]
    public static void FloorBeyondMaxReachIsRefused()
    {
        var direction = new Vector3(0f, -0.1f, -1f).Normalized();

        bool hit = FurnitureAim.TryFloorPoint(new Vector3(0f, 2f, 0f), direction, out _, out _);

        AssertBool(hit).IsFalse();
    }

    [TestCase]
    [TestCategory("Unit")]
    public static void UpwardRayMeetsNoFloor()
    {
        bool hit = FurnitureAim.TryFloorPoint(new Vector3(8f, 2f, 2f), new Vector3(0f, 1f, 0f), out _, out _);

        AssertBool(hit).IsFalse();
    }
}
