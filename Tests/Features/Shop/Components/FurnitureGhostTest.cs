using System;
using CardCleaner.Scripts.Features.Shop.Components;
using CardCleaner.Scripts.Features.Shop.Models;
using Godot;

namespace CardCleaner.Tests.Features.Shop.Components;

[TestSuite]
[RequireGodotRuntime]
public class FurnitureGhostTest
{
    private const float Tolerance = 0.001f;
    private static readonly Color ValidTint = new(0.2f, 0.9f, 0.3f, 0.45f);
    private static readonly Color InvalidTint = new(0.9f, 0.2f, 0.2f, 0.45f);

    private FurnitureGhost _ghost = null!;

    [BeforeTest]
    public void Setup()
    {
        _ghost = new FurnitureGhost();
        _ghost.Configure(new FurnitureFootprint(2.13f, 1.0f), 1.07f);
        AddNode(_ghost);
    }

    [TestCase]
    [TestCategory("Unit")]
    public void ValidPoseShowsTheValidTint()
    {
        _ghost.Show(new FurniturePose(8f, 6f, 0), true);

        AssertBool(_ghost.Tint == ValidTint).IsTrue();
    }

    [TestCase]
    [TestCategory("Unit")]
    public void InvalidPoseShowsTheInvalidTint()
    {
        _ghost.Show(new FurniturePose(8f, 6f, 0), false);

        AssertBool(_ghost.Tint == InvalidTint).IsTrue();
    }

    [TestCase]
    [TestCategory("Unit")]
    public void QuarterTurnOneYawsByAQuarterCircle()
    {
        _ghost.Show(new FurniturePose(8f, 6f, 1), true);

        AssertBool(MathF.Abs(_ghost.Rotation.Y - Mathf.Pi / 2f) <= Tolerance).IsTrue();
    }

    [TestCase]
    [TestCategory("Unit")]
    public void GhostHasNoCollisionAndJoinsNoGroup()
    {
        AssertBool(HasCollisionDescendant(_ghost)).IsFalse();
        AssertBool(_ghost.IsInGroup("sales_shelves")).IsFalse();
        AssertBool(_ghost.IsInGroup("storage_racks")).IsFalse();
        AssertBool(_ghost.IsInGroup("shop_navigation")).IsFalse();
    }

    private static bool HasCollisionDescendant(Node node)
    {
        foreach (var child in node.GetChildren())
        {
            if (child is CollisionObject3D || HasCollisionDescendant(child))
            {
                return true;
            }
        }

        return false;
    }
}
