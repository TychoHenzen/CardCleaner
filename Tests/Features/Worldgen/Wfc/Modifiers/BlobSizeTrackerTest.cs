using CardCleaner.Scripts.Features.Worldgen.Wfc;
using CardCleaner.Scripts.Features.Worldgen.Wfc.Modifiers;
using GdUnit4;
using Godot;
using static GdUnit4.Assertions;

namespace CardCleaner.Tests.Features.Worldgen.Wfc.Modifiers;

[TestSuite]
[RequireGodotRuntime]
public class BlobSizeTrackerTest
{
    private BlobSizeTracker _tracker = null!;
    private WfcGrid _grid = null!;

    [BeforeTest]
    public void Setup()
    {
        _tracker = new BlobSizeTracker();
        _grid = new WfcGrid(5, 5, new[] { "grass", "water", "sand" });
    }

    [TestCase]
    public void TestSingleTileBlob()
    {
        _tracker.RegisterCollapse(new Vector2I(2, 2), "grass", _grid);

        var size = _tracker.GetBlobSize(new Vector2I(2, 2));

        AssertInt(size).IsEqual(1);
    }

    [TestCase]
    public void TestTwoAdjacentTilesSameType()
    {
        _tracker.RegisterCollapse(new Vector2I(2, 2), "grass", _grid);
        _tracker.RegisterCollapse(new Vector2I(2, 3), "grass", _grid);

        AssertInt(_tracker.GetBlobSize(new Vector2I(2, 2))).IsEqual(2);
        AssertInt(_tracker.GetBlobSize(new Vector2I(2, 3))).IsEqual(2);
    }

    [TestCase]
    public void TestAdjacentTilesDifferentTypes()
    {
        _tracker.RegisterCollapse(new Vector2I(2, 2), "grass", _grid);
        _tracker.RegisterCollapse(new Vector2I(2, 3), "water", _grid);

        AssertInt(_tracker.GetBlobSize(new Vector2I(2, 2))).IsEqual(1);
        AssertInt(_tracker.GetBlobSize(new Vector2I(2, 3))).IsEqual(1);
    }

    [TestCase]
    public void TestDiagonalTilesNotConnected()
    {
        _tracker.RegisterCollapse(new Vector2I(2, 2), "grass", _grid);
        _tracker.RegisterCollapse(new Vector2I(3, 3), "grass", _grid);

        AssertInt(_tracker.GetBlobSize(new Vector2I(2, 2))).IsEqual(1);
        AssertInt(_tracker.GetBlobSize(new Vector2I(3, 3))).IsEqual(1);
    }

    [TestCase]
    public void TestLShapeBlob()
    {
        // L shape:
        // G G
        // G
        _tracker.RegisterCollapse(new Vector2I(0, 0), "grass", _grid);
        _tracker.RegisterCollapse(new Vector2I(1, 0), "grass", _grid);
        _tracker.RegisterCollapse(new Vector2I(0, 1), "grass", _grid);

        AssertInt(_tracker.GetBlobSize(new Vector2I(0, 0))).IsEqual(3);
        AssertInt(_tracker.GetBlobSize(new Vector2I(1, 0))).IsEqual(3);
        AssertInt(_tracker.GetBlobSize(new Vector2I(0, 1))).IsEqual(3);
    }

    [TestCase]
    public void TestMergingTwoBlobs()
    {
        // Two separate blobs that will be connected by middle tile
        // G . G
        //   G
        _tracker.RegisterCollapse(new Vector2I(0, 0), "grass", _grid);
        _tracker.RegisterCollapse(new Vector2I(2, 0), "grass", _grid);
        _tracker.RegisterCollapse(new Vector2I(1, 0), "grass", _grid); // Bridges them

        AssertInt(_tracker.GetBlobSize(new Vector2I(0, 0))).IsEqual(3);
        AssertInt(_tracker.GetBlobSize(new Vector2I(1, 0))).IsEqual(3);
        AssertInt(_tracker.GetBlobSize(new Vector2I(2, 0))).IsEqual(3);
    }

    [TestCase]
    public void TestPotentialBlobSizeWithNoNeighbors()
    {
        var potentialSize = _tracker.GetPotentialBlobSize(new Vector2I(2, 2), "grass", _grid);

        AssertInt(potentialSize).IsEqual(1);
    }

    [TestCase]
    public void TestPotentialBlobSizeWithMatchingNeighbor()
    {
        _tracker.RegisterCollapse(new Vector2I(2, 2), "grass", _grid);

        var potentialSize = _tracker.GetPotentialBlobSize(new Vector2I(2, 3), "grass", _grid);

        AssertInt(potentialSize).IsEqual(2);
    }

    [TestCase]
    public void TestPotentialBlobSizeWouldMergeTwoBlobs()
    {
        // Two blobs of size 2 each
        _tracker.RegisterCollapse(new Vector2I(0, 0), "grass", _grid);
        _tracker.RegisterCollapse(new Vector2I(0, 1), "grass", _grid);
        _tracker.RegisterCollapse(new Vector2I(2, 0), "grass", _grid);
        _tracker.RegisterCollapse(new Vector2I(2, 1), "grass", _grid);

        // Placing at (1, 0) would merge them into size 5
        var potentialSize = _tracker.GetPotentialBlobSize(new Vector2I(1, 0), "grass", _grid);

        AssertInt(potentialSize).IsEqual(5);
    }

    [TestCase]
    public void TestPotentialBlobSizeDifferentType()
    {
        _tracker.RegisterCollapse(new Vector2I(2, 2), "grass", _grid);

        // Water tile shouldn't count grass neighbor
        var potentialSize = _tracker.GetPotentialBlobSize(new Vector2I(2, 3), "water", _grid);

        AssertInt(potentialSize).IsEqual(1);
    }

    [TestCase]
    public void TestUnregisteredPositionReturnsZero()
    {
        var size = _tracker.GetBlobSize(new Vector2I(2, 2));

        AssertInt(size).IsEqual(0);
    }

    [TestCase]
    public void TestClearResetsState()
    {
        _tracker.RegisterCollapse(new Vector2I(2, 2), "grass", _grid);
        _tracker.RegisterCollapse(new Vector2I(2, 3), "grass", _grid);

        _tracker.Clear();

        AssertInt(_tracker.GetBlobSize(new Vector2I(2, 2))).IsEqual(0);
        AssertInt(_tracker.GetBlobSize(new Vector2I(2, 3))).IsEqual(0);
    }

    [TestCase]
    public void TestLargeBlobSize()
    {
        // Create a 3x3 blob
        for (var x = 0; x < 3; x++)
        {
            for (var y = 0; y < 3; y++)
            {
                _tracker.RegisterCollapse(new Vector2I(x, y), "grass", _grid);
            }
        }

        // All 9 tiles should report same blob size
        AssertInt(_tracker.GetBlobSize(new Vector2I(0, 0))).IsEqual(9);
        AssertInt(_tracker.GetBlobSize(new Vector2I(1, 1))).IsEqual(9);
        AssertInt(_tracker.GetBlobSize(new Vector2I(2, 2))).IsEqual(9);
    }
}
