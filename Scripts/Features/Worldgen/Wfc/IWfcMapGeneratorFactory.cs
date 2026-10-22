using System.Collections.Generic;

namespace CardCleaner.Scripts.Features.Worldgen.Wfc;

/// <summary>
/// Creates a WFC map generator over the given transition pairs. Each call returns a new generator over new
/// adjacency rules, because the generator constructor adds gap tiles to its rules.
/// </summary>
public interface IWfcMapGeneratorFactory
{
    IWfcMapGenerator Create(
        IReadOnlyList<(string tileA, string tileB)> transitionPairs,
        IWfcTileCatalog? tileCatalog);
}
