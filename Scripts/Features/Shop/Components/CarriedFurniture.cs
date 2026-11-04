using CardCleaner.Scripts.Features.Shop.Models;
using Godot;

namespace CardCleaner.Scripts.Features.Shop.Components;

/// <summary>
///     A placed piece that a placement session carries. It stays in the tree, hidden and off the collision layer, so a
///     save or a quit during the move still finds it at the pose it was picked up from.
/// </summary>
internal sealed class CarriedFurniture
{
    private readonly PlacedFurniture _piece;

    public CarriedFurniture(PlacedFurniture piece)
    {
        _piece = piece;
        OldPose = piece.Pose;
        piece.SetCarried(true);
    }

    /// <summary>The pose the piece was picked up from.</summary>
    public FurniturePose OldPose { get; }

    /// <summary>Puts the piece down at <paramref name="pose" />. False when a load freed it meanwhile.</summary>
    public bool PutDownAt(FurniturePose pose)
    {
        if (!GodotObject.IsInstanceValid(_piece))
            return false;
        _piece.Place(pose);
        _piece.SetCarried(false);
        return true;
    }

    /// <summary>Puts the piece back at the pose it was picked up from. False when a load freed it meanwhile.</summary>
    public bool PutBack() => PutDownAt(OldPose);
}
