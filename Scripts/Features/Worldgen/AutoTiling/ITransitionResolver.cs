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

    /// <summary>
    /// Resolves coordinates for a transition with a position-based variant choice.
    /// Returns null when no transition exists for the pair.
    /// </summary>
    /// <param name="innerTerrainId">The dominant terrain (border terrain).</param>
    /// <param name="outerTerrainId">The background terrain.</param>
    /// <param name="bitmask">The Corner16 neighbor bitmask.</param>
    /// <param name="x">Position seed x, used to pick a variant deterministically.</param>
    /// <param name="y">Position seed y, used to pick a variant deterministically.</param>
    Vector2I? ResolveTransitionWithVariant(string innerTerrainId, string outerTerrainId, int bitmask, int x, int y);

    /// <summary>
    /// Finds coordinates by looking up the terrain as the outer terrain in any transition.
    /// Returns null when the terrain is never used as an outer terrain for that bitmask.
    /// </summary>
    /// <param name="terrainId">The terrain used as the background.</param>
    /// <param name="bitmask">The bitmask variant (0 to 15).</param>
    Vector2I? ResolveAsOuterTerrain(string terrainId, int bitmask);

    /// <summary>
    /// Finds coordinates for a bitmask variant by looking up any transition with the terrain as its inner terrain.
    /// Returns null when no transition has that variant.
    /// </summary>
    /// <param name="terrainId">The terrain used as the border (inner) terrain.</param>
    /// <param name="bitmask">The bitmask variant (0 to 15).</param>
    Vector2I? ResolveAnyVariant(string terrainId, int bitmask);
}
