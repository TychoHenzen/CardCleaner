using System.Linq;
using CardCleaner.Scripts.Features.Deckbuilder.Services;
using CardCleaner.Tests.TestUtilities.Fixtures;
using Godot;

namespace CardCleaner.Tests.Features.Deckbuilder.Services;

/// <summary>
/// GetCellsInRect treats the rectangle's far edge as exclusive.
/// </summary>
[TestSuite]
[RequireGodotRuntime]
public class RegularGridMapDataRectTest
{
    private RegularGridMapData _gridData = null!;
    private float _tile;

    [BeforeTest]
    public void Setup()
    {
        _gridData = OpenFloorMap.Create(4, 4).GridData;
        _tile = _gridData.TileSize;
    }

    [TestCase]
    public void RectEndingOnTileBoundaryExcludesTheNextCell()
    {
        var cells = _gridData.GetCellsInRect(new Rect2(0, 0, _tile, _tile)).ToList();

        AssertThat(cells).IsEqual(new System.Collections.Generic.List<int> { _gridData.GetCellId(new Vector2I(0, 0)) });
    }

    [TestCase]
    public void RectEndingInsideATileIncludesThatTile()
    {
        var cells = _gridData.GetCellsInRect(new Rect2(0, 0, _tile * 1.5f, _tile * 0.5f)).ToList();

        AssertThat(cells.Count).IsEqual(2);
    }

    [TestCase]
    public void ZeroSizeRectStillReturnsTheContainingCell()
    {
        var cells = _gridData.GetCellsInRect(new Rect2(_tile * 1.5f, _tile * 1.5f, 0, 0)).ToList();

        AssertThat(cells).IsEqual(new System.Collections.Generic.List<int> { _gridData.GetCellId(new Vector2I(1, 1)) });
    }
}
