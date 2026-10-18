namespace CardCleaner.Scripts.Features.Worldgen.IrregularMesh.Rendering;

/// <summary>
/// Picks the compiled-atlas tile for a mesh quad during terrain rendering.
/// </summary>
internal interface ITerrainQuadResolver
{
    /// <summary>
    /// Returns the atlas tile for the quad, or null when the quad cannot be rendered.
    /// </summary>
    TerrainTileChoice? Resolve(IMeshQuad quad);
}
