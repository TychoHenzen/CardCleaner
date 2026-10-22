using System.Collections.Generic;

namespace CardCleaner.Scripts.Features.Worldgen.Wfc;

/// <summary>
/// The production IWfcMapGeneratorFactory. It delegates to the Wfc front door, so the adjacency rules are still built
/// in one place.
/// </summary>
public sealed class WfcMapGeneratorFactory : IWfcMapGeneratorFactory
{
    /// <inheritdoc />
    public IWfcMapGenerator Create(
        IReadOnlyList<(string tileA, string tileB)> transitionPairs,
        IWfcTileCatalog? tileCatalog) =>
        Wfc.CreateMapGenerator(transitionPairs, tileCatalog);
}
