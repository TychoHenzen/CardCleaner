using Godot;

namespace CardCleaner.Scripts.Features.Worldgen.AutoTiling;

/// <summary>
/// Result of resolving a terrain transition.
/// Contains the atlas coordinates and source ID for rendering.
/// </summary>
public record TransitionResolveResult(int SourceId, Vector2I AtlasCoords);

/// <summary>
/// Resolves terrain transition atlas coordinates at runtime.
/// Used by map generators to look up the correct composite tile
/// for a given inner/outer terrain pair and neighbor bitmask.
/// </summary>
public interface ITransitionResolver
{
    /// <summary>
    /// Resolves the atlas coordinates for a terrain transition.
    /// </summary>
    /// <param name="innerTerrainId">The dominant terrain (border terrain).</param>
    /// <param name="outerTerrainId">The background terrain.</param>
    /// <param name="bitmask">The neighbor bitmask for variant selection.</param>
    /// <returns>Atlas coordinates if a transition exists, null otherwise.</returns>
    Vector2I? ResolveTransition(string innerTerrainId, string outerTerrainId, int bitmask);

    /// <summary>
    /// Resolves a transition with full render info (sourceId + atlasCoords).
    /// Includes fallback to the inner terrain's auto-tile variants if no compiled transition exists.
    /// </summary>
    /// <param name="innerTerrainId">The dominant terrain (border terrain).</param>
    /// <param name="outerTerrainId">The background terrain.</param>
    /// <param name="bitmask">The neighbor bitmask for variant selection.</param>
    /// <param name="fallbackSourceId">Source ID to use if falling back to tile auto-tiles.</param>
    /// <param name="fallbackCoords">Atlas coords to use if no transition or auto-tile found.</param>
    /// <returns>Resolved transition with source ID and atlas coordinates.</returns>
    TransitionResolveResult ResolveWithFallback(
        string innerTerrainId,
        string outerTerrainId,
        int bitmask,
        int fallbackSourceId,
        Vector2I fallbackCoords);

    /// <summary>
    /// Checks if a transition exists for the given terrain pair.
    /// </summary>
    /// <param name="innerTerrainId">The dominant terrain (border terrain).</param>
    /// <param name="outerTerrainId">The background terrain.</param>
    /// <returns>True if a transition is defined for this pair.</returns>
    bool HasTransition(string innerTerrainId, string outerTerrainId);

    /// <summary>
    /// Gets the source ID for the compiled atlas.
    /// Transitions are rendered from this atlas source.
    /// </summary>
    int CompiledAtlasSourceId { get; }

    /// <summary>
    /// Finds the solid fill (bitmask 15) coordinates for a terrain by searching
    /// any transition that uses it. Used for uniform terrain areas where
    /// self-transitions don't exist.
    /// </summary>
    /// <param name="terrainId">The terrain tile ID to find the solid fill for.</param>
    /// <returns>Atlas coordinates of the solid fill, or null if not found.</returns>
    Vector2I? ResolveSolidFill(string terrainId);
}
