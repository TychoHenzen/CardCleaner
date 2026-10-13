using CardCleaner.Scripts.Core.Interfaces;

namespace CardCleaner.Scripts.Features.Worldgen.IrregularMesh;

/// <summary>
/// The irregular map's generator. The composition root registers it; the cabinet resolves it for the irregular kind
/// and hands it to the irregular screen. Its maps are <see cref="IMeshGeneratedMap"/>s.
/// </summary>
public interface IMeshMapGenerator : IMapGenerator
{
}
