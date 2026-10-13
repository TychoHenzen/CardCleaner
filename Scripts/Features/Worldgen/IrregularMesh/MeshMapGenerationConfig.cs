using CardCleaner.Scripts.Core.Interfaces;

namespace CardCleaner.Scripts.Features.Worldgen.IrregularMesh;

/// <summary>
/// A generation config that also names the mesh's ring count. <see cref="MeshMapGenerator"/> reads it when it is
/// given one and uses <see cref="MeshMapGenerator.DefaultRings"/> for a plain config.
/// </summary>
public class MeshMapGenerationConfig : MapGenerationConfig
{
    /// <summary>Rings of hexes around the mesh centre; more rings make a larger map.</summary>
    public int Rings { get; init; } = MeshMapGenerator.DefaultRings;
}
