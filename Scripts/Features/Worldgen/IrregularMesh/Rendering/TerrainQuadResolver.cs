using CardCleaner.Scripts.Features.Worldgen.AutoTiling;
using Godot;

namespace CardCleaner.Scripts.Features.Worldgen.IrregularMesh.Rendering;

/// <summary>
/// Picks the compiled-atlas tile for a quad from its background tile and corner foreground tiles.
/// </summary>
internal sealed class TerrainQuadResolver
{
    private const int AllCornersMask = 15;

    private readonly CompiledTransitionResolver _transitionResolver;

    internal TerrainQuadResolver(CompiledTransitionResolver transitionResolver)
    {
        _transitionResolver = transitionResolver;
    }

    /// <summary>
    /// Returns the atlas tile for the quad, or null when the quad cannot be rendered.
    /// </summary>
    internal TerrainTileChoice? Resolve(MeshQuad quad)
    {
        var backgroundTileId = quad.BackgroundTileId;
        if (string.IsNullOrEmpty(backgroundTileId))
            return null;

        var sortedCorners = quad.GetSortedCorners();
        if (sortedCorners.Length != 4)
            return null;

        var foregroundTileId = FindForegroundTileId(sortedCorners);
        var bitmask = ComputeBitmask(sortedCorners, foregroundTileId);
        var atlasCoords = ResolveAtlasCoords(quad, foregroundTileId, backgroundTileId, bitmask);
        if (!atlasCoords.HasValue)
            return null;

        return new TerrainTileChoice(atlasCoords.Value, foregroundTileId ?? backgroundTileId);
    }

    /// <summary>
    /// Finds the unique foreground tile among the corners (the gap constraint allows at most one type).
    /// </summary>
    private static string? FindForegroundTileId(MeshVertex[] sortedCorners)
    {
        foreach (var corner in sortedCorners)
        {
            if (!string.IsNullOrEmpty(corner.ForegroundTileId))
                return corner.ForegroundTileId;
        }

        return null;
    }

    /// <summary>
    /// Corners are sorted by angle: [0]=SW, [1]=SE, [2]=NE, [3]=NW.
    /// </summary>
    private static int ComputeBitmask(MeshVertex[] sortedCorners, string? foregroundTileId)
    {
        if (string.IsNullOrEmpty(foregroundTileId))
            return 0;

        int bitmask = 0;
        if (sortedCorners[0].ForegroundTileId == foregroundTileId) bitmask |= MeshQuad.SW;
        if (sortedCorners[1].ForegroundTileId == foregroundTileId) bitmask |= MeshQuad.SE;
        if (sortedCorners[2].ForegroundTileId == foregroundTileId) bitmask |= MeshQuad.NE;
        if (sortedCorners[3].ForegroundTileId == foregroundTileId) bitmask |= MeshQuad.NW;
        return bitmask;
    }

    private Vector2I? ResolveAtlasCoords(MeshQuad quad, string? foregroundTileId, string backgroundTileId, int bitmask)
    {
        if (string.IsNullOrEmpty(foregroundTileId) || bitmask == 0)
            return ResolveBackground(backgroundTileId);

        if (bitmask == AllCornersMask)
            return _transitionResolver.ResolveSolidFill(foregroundTileId);

        return ResolveTransition(quad, foregroundTileId, backgroundTileId, bitmask);
    }

    private Vector2I? ResolveBackground(string backgroundTileId)
    {
        return _transitionResolver.ResolveSolidFill(backgroundTileId)
               ?? _transitionResolver.ResolveAsOuterTerrain(backgroundTileId, 0);
    }

    private Vector2I? ResolveTransition(MeshQuad quad, string foregroundTileId, string backgroundTileId, int bitmask)
    {
        int positionSeed = TerrainPositionHash.Compute(quad.Centroid);
        return _transitionResolver.ResolveTransitionWithVariant(
                   foregroundTileId, backgroundTileId, bitmask, positionSeed, positionSeed)
               ?? _transitionResolver.ResolveAnyVariant(foregroundTileId, bitmask);
    }
}
