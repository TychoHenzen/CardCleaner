using Godot;

namespace CardCleaner.Scripts.Features.Shop.Components;

/// <summary>What the player's camera aims at, read from a viewport.</summary>
internal static class PlacementAim
{
    /// <summary>The placed furniture under the camera's aim, or null.</summary>
    public static PlacedFurniture? Furniture(Viewport viewport)
    {
        var camera = viewport.GetCamera3D();
        return camera == null ? null : FurnitureProbe.Aimed(viewport.World3D, camera);
    }

    /// <summary>The floor point under the camera aim; null with no camera or when the aim misses the floor.</summary>
    public static (float X, float Z)? FloorPoint(Viewport viewport)
    {
        var camera = viewport.GetCamera3D();
        if (camera == null || !FurnitureAim.TryFloorPoint(
                camera.GlobalPosition, -camera.GlobalBasis.Z, out var x, out var z))
        {
            return null;
        }

        return (x, z);
    }
}
