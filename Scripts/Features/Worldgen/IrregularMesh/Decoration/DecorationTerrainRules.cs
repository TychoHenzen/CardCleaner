using System.Linq;

namespace CardCleaner.Scripts.Features.Worldgen.IrregularMesh.Decoration;

/// <summary>
/// Decides which decoration types may stand on a quad given the terrain at its corners.
/// </summary>
internal static class DecorationTerrainRules
{
    internal static bool IsCompatible(IrregularMesh mesh, MeshQuad quad, DecorationType type)
    {
        var terrainTypes = quad.VertexIds
            .Select(vid => mesh.Vertices[vid].TerrainType)
            .ToHashSet();

        return type switch
        {
            // Trees need solid ground (terrainType > 0) at all corners
            DecorationType.Tree => terrainTypes.All(t => t > 0),

            // Rocks can be anywhere except water
            DecorationType.Rock => terrainTypes.Any(t => t > 0),

            // Bushes need solid ground
            DecorationType.Bush => terrainTypes.All(t => t > 0),

            // Water lilies need water (terrainType == 0)
            DecorationType.WaterLily => terrainTypes.Any(t => t == 0),

            // Grass overlays work on any ground
            DecorationType.Grass => terrainTypes.Any(t => t > 0),

            // Props are flexible
            _ => true
        };
    }
}
