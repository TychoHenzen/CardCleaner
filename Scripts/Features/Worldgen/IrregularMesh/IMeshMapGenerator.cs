using CardCleaner.Scripts.Core.Interfaces;

namespace CardCleaner.Scripts.Features.Worldgen.IrregularMesh;

/// <summary>
/// The irregular map's generator, and the container's key for it. The lazy [Service] scan builds it; the cabinet
/// resolves it for the irregular kind and the irregular screen hands it to the session. Its maps are
/// <see cref="IMeshGeneratedMap"/>s, which the screen checks when the session's map arrives.
/// </summary>
public interface IMeshMapGenerator : IMapGenerator
{
}
