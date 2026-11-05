using CardCleaner.Scripts.Features.Shop.Models;
using Godot;

namespace CardCleaner.Scripts.Features.Shop.Components;

/// <summary>
///     The piece one placement session carries: a new piece from a delivered box, or a placed piece being moved.
///     It knows how to put that piece on the floor or back where it was.
/// </summary>
internal sealed class PlacementSession
{
    private readonly FurnitureBox? _box;
    private readonly CarriedFurniture? _carried;

    private PlacementSession(FurnitureItem item, FurnitureBox? box, CarriedFurniture? carried)
    {
        Item = item;
        _box = box;
        _carried = carried;
    }

    public FurnitureItem Item { get; }

    /// <summary>Starts a session that places the item of a delivered box.</summary>
    public static PlacementSession ForBox(FurnitureItem item, FurnitureBox box) => new(item, box, null);

    /// <summary>Starts a session that moves a placed piece, lifted out of the shop until the session ends.</summary>
    public static PlacementSession ForMove(FurnitureItem item, PlacedFurniture furniture) =>
        new(item, null, new CarriedFurniture(furniture));

    /// <summary>Where a moved piece stood, else <paramref name="newPiecePose" />.</summary>
    public FurniturePose StartPose(FurniturePose newPiecePose) => _carried?.OldPose ?? newPiecePose;

    /// <summary>Puts a carried piece back where it was; false when none is carried or a load freed it.</summary>
    public bool PutBack() => _carried?.PutBack() == true;

    public CommitOutcome Commit(FurniturePose pose, Node3D? root)
    {
        if (_carried != null)
            return _carried.PutDownAt(pose) ? CommitOutcome.Placed : CommitOutcome.Gone;

        var placed = root == null ? null : PlacedFurniture.Create(Item, pose);
        if (placed == null)
            return CommitOutcome.Kept;

        placed.StandUnder(root!);
        _box?.QueueFree();
        return CommitOutcome.Placed;
    }
}
