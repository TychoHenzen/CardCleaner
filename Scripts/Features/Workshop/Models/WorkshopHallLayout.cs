using System.Collections.Generic;
using System.Linq;
using Godot;

namespace CardCleaner.Scripts.Features.Workshop.Models;

/// <summary>
///     The workshop as one square warehouse divided into rooms joined by hallways, written down in metres and
///     expanded into 0.2 m wall cells. The cabinet room sits at the centre; a hallway or hall leaves each of its
///     four sides, and the four quadrants hold the ordering rooms (west) and build rooms (east). Every wall run and
///     doorway edge lies on a multiple of the cell size, so a wall is always exactly two cells thick.
///     Coordinates are workshop-local: x east, z south, the cabinet room centred on the origin.
/// </summary>
public sealed class WorkshopHallLayout
{
    public const int WallItem = 1;
    public const int WallLayer = 0;
    public const int CellsPerMetre = 5;

    /// <summary>Half the side of the square warehouse, outer wall faces included.</summary>
    public const float HalfSize = 20f;

    /// <summary>Every doorway is this wide, so the player capsule and a conveyor cart fit through.</summary>
    public const float DoorwayWidth = 3.2f;

    public const float WallThickness = 0.4f;

    // A spoke hallway's walls stand on these lines, leaving DoorwayWidth of clear floor between them.
    private const float SpokeWall = 1.8f;
    private const float CabinetWall = 6f;
    private const float QuadrantSplit = 13f;

    /// <summary>
    ///     Every interior wall as a straight run along one centre line, with the doorway centres cut into it.
    ///     The outer wall has no doorways and is added separately.
    /// </summary>
    private static readonly WallRun[] InteriorWalls =
    [
        // The two long east-west walls that bound the cabinet room and the side halls.
        new(false, -CabinetWall, -HalfSize, HalfSize, -13f, 0f, 13f),
        new(false, CabinetWall, -HalfSize, HalfSize, -13f, 0f, 13f),

        // The cabinet room's west and east walls, opening onto the west and east halls.
        new(true, -CabinetWall, -CabinetWall, CabinetWall, 0f),
        new(true, CabinetWall, -CabinetWall, CabinetWall, 0f),

        // The north and south spoke hallways.
        new(true, -SpokeWall, -HalfSize, -CabinetWall, -16.4f),
        new(true, SpokeWall, -HalfSize, -CabinetWall, -16.4f),
        new(true, -SpokeWall, CabinetWall, HalfSize, 13f),
        new(true, SpokeWall, CabinetWall, HalfSize, 13f),

        // Each northern quadrant is split into two rooms joined by a door.
        new(false, -QuadrantSplit, -HalfSize, -SpokeWall, -10f),
        new(false, -QuadrantSplit, SpokeWall, HalfSize, 10f)
    ];

    /// <summary>The rooms and hallways, each with the spot a player must be able to reach, west to east.</summary>
    public static readonly IReadOnlyList<HallSection> Sections =
    [
        new("OrderOffice", new Vector2(-11f, -16.4f)),
        new("DeliveryBay", new Vector2(-11f, -9.6f)),
        new("WestHall", new Vector2(-13f, 0f)),
        new("Storeroom", new Vector2(-11f, 13f)),
        new("NorthHallway", new Vector2(0f, -13f)),
        new("CabinetRoom", new Vector2(3f, 3f)),
        new("SouthHallway", new Vector2(0f, 13f)),
        new("BuildRoomA", new Vector2(11f, -16.4f)),
        new("BuildRoomB", new Vector2(11f, -9.6f)),
        new("EastHall", new Vector2(13f, 0f)),
        new("BuildHallC", new Vector2(11f, 13f))
    ];

    private WorkshopHallLayout(HashSet<Vector2I> walls)
    {
        Walls = walls;
    }

    /// <summary>Cells (x, z) that carry wall.</summary>
    public IReadOnlySet<Vector2I> Walls { get; }

    /// <summary>The one layout the workshop ships with.</summary>
    public static WorkshopHallLayout Default { get; } = Build();

    /// <summary>Every cell (x, z) inside the outer wall faces.</summary>
    public static IEnumerable<Vector2I> AllCells => CellsIn(new Rect2(-HalfSize, -HalfSize, 2 * HalfSize, 2 * HalfSize));

    /// <summary>The cell (x, z) containing a workshop-local position in metres.</summary>
    public static Vector2I CellOf(Vector2 metres)
    {
        return new Vector2I(Mathf.FloorToInt(metres.X * CellsPerMetre), Mathf.FloorToInt(metres.Y * CellsPerMetre));
    }

    /// <summary>The centre, in metres, of the cell (x, z).</summary>
    public static Vector2 CentreOf(Vector2I cell)
    {
        return new Vector2((cell.X + 0.5f) / CellsPerMetre, (cell.Y + 0.5f) / CellsPerMetre);
    }

    private static WorkshopHallLayout Build()
    {
        var walls = new HashSet<Vector2I>();
        foreach (var run in OuterWalls().Concat(InteriorWalls))
        {
            walls.UnionWith(CellsIn(run.Body));
            foreach (var doorway in run.Doorways)
                walls.ExceptWith(CellsIn(doorway));
        }

        return new WorkshopHallLayout(walls);
    }

    private static IEnumerable<WallRun> OuterWalls()
    {
        var edge = HalfSize - WallThickness / 2f;
        yield return new WallRun(false, -edge, -HalfSize, HalfSize);
        yield return new WallRun(false, edge, -HalfSize, HalfSize);
        yield return new WallRun(true, -edge, -HalfSize, HalfSize);
        yield return new WallRun(true, edge, -HalfSize, HalfSize);
    }

    // Cells covered by a rectangle whose edges lie on cell boundaries. Rounding, not flooring, so an edge that
    // float arithmetic puts a hair below a boundary still lands on it.
    private static IEnumerable<Vector2I> CellsIn(Rect2 metres)
    {
        var minX = Mathf.RoundToInt(metres.Position.X * CellsPerMetre);
        var minZ = Mathf.RoundToInt(metres.Position.Y * CellsPerMetre);
        var maxX = Mathf.RoundToInt(metres.End.X * CellsPerMetre);
        var maxZ = Mathf.RoundToInt(metres.End.Y * CellsPerMetre);
        for (var x = minX; x < maxX; x++)
        for (var z = minZ; z < maxZ; z++)
            yield return new Vector2I(x, z);
    }

    /// <summary>
    ///     A straight wall on centre line <paramref name="Line" /> (x for a vertical run, z for a horizontal one),
    ///     from <paramref name="From" /> to <paramref name="To" /> along the other axis, with a doorway of
    ///     <see cref="DoorwayWidth" /> cut at each of <paramref name="DoorCentres" />.
    /// </summary>
    private sealed record WallRun(bool Vertical, float Line, float From, float To, params float[] DoorCentres)
    {
        public Rect2 Body => Span(From, To, WallThickness);

        public IEnumerable<Rect2> Doorways =>
            DoorCentres.Select(centre => Span(centre - DoorwayWidth / 2f, centre + DoorwayWidth / 2f, WallThickness));

        private Rect2 Span(float start, float end, float thickness)
        {
            var across = Line - thickness / 2f;
            return Vertical
                ? new Rect2(across, start, thickness, end - start)
                : new Rect2(start, across, end - start, thickness);
        }
    }
}
