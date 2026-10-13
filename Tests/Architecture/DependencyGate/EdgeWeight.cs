namespace CardCleaner.Tests.Architecture.DependencyGate;

/// <summary>The summed strength of every reference from one class to another.</summary>
internal sealed class EdgeWeight
{
    /// <summary>Sum of kind weight x count: the module-level weight.</summary>
    public long Weight { get; set; }

    /// <summary>Sum of kind weight x (1 + log2 count): the clustering weight.</summary>
    public double ClusterWeight { get; set; }
}
