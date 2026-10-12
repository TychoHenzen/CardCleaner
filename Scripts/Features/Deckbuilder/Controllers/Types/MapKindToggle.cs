namespace CardCleaner.Scripts.Features.Deckbuilder.Controllers;

/// <summary>
/// Rules of the cabinet's map kind toggle: which kind a press switches to, and the text that names the picked kind
/// on the map screen. When the toggle may be pressed is the controller's cabinet phase.
/// </summary>
public static class MapKindToggle
{
    /// <summary>The kind a toggle press switches to.</summary>
    public static MapGenerationType Next(MapGenerationType kind) =>
        kind == MapGenerationType.RegularGrid ? MapGenerationType.IrregularMesh : MapGenerationType.RegularGrid;

    /// <summary>The status text that names the picked kind.</summary>
    public static string Describe(MapGenerationType kind) =>
        kind == MapGenerationType.RegularGrid ? "Map: grid" : "Map: irregular";
}
