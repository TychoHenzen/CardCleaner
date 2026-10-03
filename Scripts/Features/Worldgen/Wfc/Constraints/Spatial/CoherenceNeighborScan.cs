namespace CardCleaner.Scripts.Features.Worldgen.Wfc.Constraints;

/// <summary>
/// What the collapsed neighbors of a cell say about a candidate tile.
/// </summary>
internal readonly record struct CoherenceNeighborScan(int LargestMatchingRegion, bool HasAnyCollapsedNeighbor);
