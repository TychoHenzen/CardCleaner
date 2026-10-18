using CardCleaner.Scripts.Features.Worldgen.AutoTiling;

namespace CardCleaner.Scripts.Features.Worldgen.IrregularMesh.Rendering;

/// <summary>
/// Creates the terrain quad resolver used by the irregular terrain renderer.
/// </summary>
internal static class TerrainQuads
{
    public static ITerrainQuadResolver Create(ITransitionResolver resolver) => new TerrainQuadResolver(resolver);
}
