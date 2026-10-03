using CardCleaner.Scripts.Features.Deckbuilder.Services;
using Godot;

namespace CardCleaner.Tests.TestUtilities.Fixtures;

/// <summary>
///     A fully passable rectangular map of "floor" tiles together with its regular grid view.
///     Deconstructs into (MapData, GridData).
/// </summary>
public sealed record OpenFloorMap(SimpleMapData MapData, RegularGridMapData GridData)
{
    public const string FloorTileId = "floor";

    public static OpenFloorMap Create(int width, int height) =>
        Create(width, height, Vector2I.Zero);

    public static OpenFloorMap Create(int width, int height, Vector2I playerStart)
    {
        var mapData = new SimpleMapData
        {
            TileIds = new string[height, width],
            Size = new Vector2I(width, height),
            PlayerStart = playerStart
        };

        for (var y = 0; y < height; y++)
        for (var x = 0; x < width; x++)
        {
            mapData.TileIds[y, x] = FloorTileId;
            mapData.PassableTiles.Add(new Vector2I(x, y));
        }

        return new OpenFloorMap(mapData, new RegularGridMapData(mapData));
    }
}
