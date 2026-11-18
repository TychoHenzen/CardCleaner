using Godot;
using IrregularMeshNs = CardCleaner.Scripts.Features.Worldgen.IrregularMesh;

namespace CardCleaner.Tests.Features.Worldgen.IrregularMesh;

/// <summary>
/// Tests for IrregularMeshMovementController.
/// Verifies position tracking, movement, and event handling.
/// </summary>
[TestSuite]
[RequireGodotRuntime]
public class IrregularMeshMovementControllerTest
{
    private IrregularMeshNs.IrregularMesh _testMesh = null!;
    private IrregularMeshNs.IrregularMeshMapData _mapData = null!;
    private IrregularMeshNs.IrregularMeshMovementController _controller = null!;

    [BeforeTest]
    public void Setup()
    {
        _testMesh = CreateSimpleTestMesh();
        _mapData = new IrregularMeshNs.IrregularMeshMapData(_testMesh);
        _controller = new IrregularMeshNs.IrregularMeshMovementController();
    }

    [AfterTest]
    public void Teardown()
    {
        // The controller never enters the tree, so QueueFree would leave it alive when gdUnit counts orphans.
        _controller.Free();
    }

    [TestCase]
    public void TestControllerInitializes()
    {
        _controller.Initialize(_mapData, 0);

        AssertThat(_controller.CurrentCellId).IsEqual(0);
    }

    [TestCase]
    public void TestInitialPositionMatchesCellCenter()
    {
        _controller.Initialize(_mapData, 0);

        var expectedPos = _mapData.GetCellCenter(0);
        AssertThat(_controller.Position).IsEqual(expectedPos);
    }

    [TestCase]
    public void TestCurrentWorldPositionMatchesCellCenter()
    {
        _controller.Initialize(_mapData, 0);

        var expectedPos = _mapData.GetCellCenter(0);
        AssertThat(_controller.CurrentWorldPosition).IsEqual(expectedPos);
    }

    [TestCase]
    public void TestIsMovingDefaultsFalse()
    {
        _controller.Initialize(_mapData, 0);
        AssertBool(_controller.IsMoving).IsFalse();
    }

    [TestCase]
    public void TestTeleportToCell()
    {
        _controller.Initialize(_mapData, 0);

        _controller.TeleportToCell(1);

        AssertThat(_controller.CurrentCellId).IsEqual(1);
        AssertThat(_controller.Position).IsEqual(_mapData.GetCellCenter(1));
    }

    [TestCase]
    public void TestTeleportToCellRaisesCellEntered()
    {
        _controller.Initialize(_mapData, 0);

        int enteredCellId = -1;
        _controller.CellEntered += (cellId) => enteredCellId = cellId;

        _controller.TeleportToCell(2);

        AssertThat(enteredCellId).IsEqual(2);
    }

    [TestCase]
    public void TestTeleportToSameCellDoesNotRaiseCellEntered()
    {
        _controller.Initialize(_mapData, 0);

        int enteredCount = 0;
        _controller.CellEntered += (_) => enteredCount++;

        _controller.TeleportToCell(0);

        AssertThat(enteredCount).IsEqual(0);
    }

    [TestCase]
    public void TestMoveToValidCellReturnsTrue()
    {
        _controller.Initialize(_mapData, 0);

        // Cell 1 should be adjacent and passable
        var result = _controller.MoveToCell(1);

        AssertBool(result).IsTrue();
    }

    [TestCase]
    public void TestMoveToInvalidCellReturnsFalse()
    {
        _controller.Initialize(_mapData, 0);

        var result = _controller.MoveToCell(-1);

        AssertBool(result).IsFalse();
    }

    [TestCase]
    public void TestMoveToNonAdjacentCellReturnsFalse()
    {
        _controller.Initialize(_mapData, 0);

        // Cell 3 only touches cell 0 at a corner, so it is not an adjacent cell
        var result = _controller.MoveToCell(3);

        AssertBool(result).IsFalse();
        AssertBool(_controller.IsMoving).IsFalse();
    }

    [TestCase]
    public void TestMoveToImpassableCellReturnsFalse()
    {
        // Make cell 1 impassable by setting vertex terrain to 0
        foreach (var vertexId in _testMesh.Quads[1].VertexIds)
        {
            _testMesh.Vertices[vertexId].TerrainType = 0;
        }

        _controller.Initialize(_mapData, 0);

        var result = _controller.MoveToCell(1);

        AssertBool(result).IsFalse();
    }

    [TestCase]
    public void TestMoveWhileMovingReturnsFalse()
    {
        _controller.Initialize(_mapData, 0);

        // Start first move
        _controller.MoveToCell(1);
        AssertBool(_controller.IsMoving).IsTrue();

        // Try second move while first is in progress
        var result = _controller.MoveToCell(2);

        AssertBool(result).IsFalse();
    }

    [TestCase]
    public void TestMovementStartedEventRaised()
    {
        _controller.Initialize(_mapData, 0);

        int fromCellId = -1;
        int toCellId = -1;
        _controller.MovementStarted += (from, to, fromPos, toPos) =>
        {
            fromCellId = from;
            toCellId = to;
        };

        _controller.MoveToCell(1);

        AssertThat(fromCellId).IsEqual(0);
        AssertThat(toCellId).IsEqual(1);
    }

    [TestCase]
    public void TestCancelMovement()
    {
        _controller.Initialize(_mapData, 0);

        _controller.MoveToCell(1);
        AssertBool(_controller.IsMoving).IsTrue();

        _controller.CancelMovement();

        AssertBool(_controller.IsMoving).IsFalse();
    }

    [TestCase]
    public void TestMoveDurationDefault()
    {
        AssertThat(_controller.MoveDuration).IsEqual(0.15f);
    }

    [TestCase]
    public void TestMoveDurationCanBeChanged()
    {
        _controller.MoveDuration = 0.5f;
        AssertThat(_controller.MoveDuration).IsEqual(0.5f);
    }

    [TestCase]
    public void TestTransitionTypeDefault()
    {
        AssertThat(_controller.TransitionType).IsEqual(Tween.TransitionType.Sine);
    }

    [TestCase]
    public void TestEaseTypeDefault()
    {
        AssertThat(_controller.EaseType).IsEqual(Tween.EaseType.InOut);
    }

    [TestCase]
    public void TestGetCellWorldPosition()
    {
        _controller.Initialize(_mapData, 0);

        var pos = _controller.GetCellWorldPosition(2);
        var expected = _mapData.GetCellCenter(2);

        AssertThat(pos).IsEqual(expected);
    }

    [TestCase]
    public void TestGetCellAtPosition()
    {
        _controller.Initialize(_mapData, 0);

        var center = _mapData.GetCellCenter(1);
        var cellId = _controller.GetCellAtPosition(center);

        AssertThat(cellId).IsEqual(1);
    }

    /// <summary>
    /// Create a simple 2x2 quad test mesh with 9 vertices.
    /// </summary>
    private static IrregularMeshNs.IrregularMesh CreateSimpleTestMesh()
    {
        var mesh = new IrregularMeshNs.IrregularMesh();

        // Create 3x3 grid of vertices
        var positions = new[]
        {
            new Vector2(0, 0), new Vector2(16, 0), new Vector2(32, 0),
            new Vector2(0, 16), new Vector2(16, 16), new Vector2(32, 16),
            new Vector2(0, 32), new Vector2(16, 32), new Vector2(32, 32)
        };

        foreach (var pos in positions)
        {
            var id = mesh.AddVertex(pos);
            mesh.Vertices[id].TerrainType = 1; // Solid ground
        }

        // Add 4 quads
        mesh.AddQuad(new[] { 0, 1, 4, 3 });
        mesh.AddQuad(new[] { 1, 2, 5, 4 });
        mesh.AddQuad(new[] { 3, 4, 7, 6 });
        mesh.AddQuad(new[] { 4, 5, 8, 7 });

        mesh.UpdateAllCachedProperties();
        mesh.BuildAdjacency();

        return mesh;
    }
}
