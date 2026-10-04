using System.Threading.Tasks;
using CardCleaner.Scripts.Core.Services;
using CardCleaner.Scripts.Features.Deckbuilder.Services;
using CardCleaner.Tests.TestUtilities.Fixtures;
using Godot;

namespace CardCleaner.Tests.Core.Services;

/// <summary>
/// An opaque destination cell stays visible, while opaque cells in front of it still block.
/// Both opaque cells share one body, matching how terrain colliders are generated.
/// </summary>
[TestSuite]
[RequireGodotRuntime]
public class RaycastVisibilityOpaqueTargetTest
{
    private const float TileSize = 16f;

    private RaycastVisibilityChecker _checker = null!;
    private RegularGridMapData _gridData = null!;

    [BeforeTest]
    public async Task Setup()
    {
        var root = new Node2D();
        var viewport = new SubViewport { World2D = new World2D() };
        var body = new StaticBody2D { CollisionLayer = RaycastVisibilityChecker.OpaqueTerrainCollisionLayer };
        body.AddChild(CreateTileShape(new Vector2(TileSize * 1.5f, TileSize * 0.5f)));
        body.AddChild(CreateTileShape(new Vector2(TileSize * 3.5f, TileSize * 0.5f)));
        viewport.AddChild(body);
        root.AddChild(viewport);
        AddNode(root);

        await ISceneRunner.SyncPhysicsFrame;
        await ISceneRunner.SyncPhysicsFrame;

        _checker = new RaycastVisibilityChecker(viewport.World2D.DirectSpaceState);
        _gridData = OpenFloorMap.Create(5, 1).GridData;
    }

    [TestCase]
    public void CanSeeOpaqueDestinationWhenNothingIsInFront()
    {
        var from = _gridData.GetCellId(new Vector2I(2, 0));
        var opaqueTarget = _gridData.GetCellId(new Vector2I(3, 0));

        AssertBool(_checker.CanSee(from, opaqueTarget, _gridData)).IsTrue();
    }

    [TestCase]
    public void CannotSeePastAnOpaqueCellInFront()
    {
        var from = _gridData.GetCellId(new Vector2I(0, 0));
        var behindOpaque = _gridData.GetCellId(new Vector2I(4, 0));

        AssertBool(_checker.CanSee(from, behindOpaque, _gridData)).IsFalse();
    }

    [TestCase]
    public void CanSeeNearestOpaqueCellFromOpenGround()
    {
        var from = _gridData.GetCellId(new Vector2I(0, 0));
        var opaqueTarget = _gridData.GetCellId(new Vector2I(1, 0));

        AssertBool(_checker.CanSee(from, opaqueTarget, _gridData)).IsTrue();
    }

    private static CollisionShape2D CreateTileShape(Vector2 center) => new()
    {
        Position = center,
        Shape = new RectangleShape2D { Size = new Vector2(TileSize, TileSize) }
    };
}
