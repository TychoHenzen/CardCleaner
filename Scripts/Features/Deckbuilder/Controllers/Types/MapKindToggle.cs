namespace CardCleaner.Scripts.Features.Deckbuilder.Controllers;

/// <summary>
/// Rules of the cabinet's map kind toggle: which kind a press switches to, when the toggle may be pressed, and the
/// text that names the picked kind on the map screen.
/// </summary>
public static class MapKindToggle
{
    /// <summary>
    /// The toggle works only while the cabinet is idle: not while a session runs, and not while a finished map
    /// waits for its reset press, so the kind never changes under a live map.
    /// </summary>
    public static bool CanToggle(bool sessionRunning, bool awaitingReset) => !sessionRunning && !awaitingReset;

    /// <summary>The kind a toggle press switches to.</summary>
    public static MapGenerationType Next(MapGenerationType kind) =>
        kind == MapGenerationType.RegularGrid ? MapGenerationType.IrregularMesh : MapGenerationType.RegularGrid;

    /// <summary>The status text that names the picked kind.</summary>
    public static string Describe(MapGenerationType kind) =>
        kind == MapGenerationType.RegularGrid ? "Map: grid" : "Map: irregular";
}
