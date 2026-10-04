using Godot;

namespace CardCleaner.Scripts.Features.Worldgen.AutoTiling;

/// <summary>
/// Resolves terrain transition atlas coordinates at runtime.
/// Used by map generators to look up the correct composite tile
/// for a given inner/outer terrain pair and neighbor bitmask.
/// </summary>
public interface ITransitionResolver
{
    /// <summary>Resolves atlas coordinates for a terrain transition, or null if unavailable.</summary>
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

    /// <summary>Checks whether a transition exists for a terrain pair.</summary>
    bool HasTransition(string innerTerrainId, string outerTerrainId);

    /// <summary>
    /// Gets the source ID for the compiled atlas.
    /// Transitions are rendered from this atlas source.
    /// </summary>
    int CompiledAtlasSourceId { get; }

    /// <summary>Finds solid-fill coordinates for a terrain, or null if unavailable.</summary>
    Vector2I? ResolveSolidFill(string terrainId);
}
