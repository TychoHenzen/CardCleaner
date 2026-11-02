using Godot;

namespace CardCleaner.Tests.Scenes;

/// <summary>Ground-plane footprints (x, z) the neighbourhood scenery must stay out of.</summary>
public static class NeighbourhoodBounds
{
    public static readonly Rect2 ShopAndStreet = new(-16f, -8f, 40f, 24f);
    public static readonly Rect2 Workshop = new(40f, -24f, 40f, 40f);
}
