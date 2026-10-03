namespace CardCleaner.Scripts.Features.Worldgen.IrregularMesh;

/// <summary>
/// Number of cells in each fog state.
/// </summary>
internal readonly record struct FogStatistics(int Hidden, int Revealed, int Visible);
