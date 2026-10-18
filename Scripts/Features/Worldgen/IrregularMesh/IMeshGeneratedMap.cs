using CardCleaner.Scripts.Core.Interfaces;

namespace CardCleaner.Scripts.Features.Worldgen.IrregularMesh;

/// <summary>
/// A generated irregular map: the mesh for drawing, and its map data with the player start and the enemies placed.
/// </summary>
public interface IMeshGeneratedMap : IGeneratedMap
{
    /// <summary>The mesh, for rendering.</summary>
    IIrregularMesh RawMesh { get; }

    /// <summary>
    /// The map data over the mesh: cells, passability, start and enemies, in the generator's world scale.
    /// </summary>
    IMapData MeshMapData { get; }

    /// <summary>World units per mesh unit of the map data.</summary>
    float WorldScale { get; }
}
