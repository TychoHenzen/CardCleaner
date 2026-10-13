using System.Collections.Generic;

namespace CardCleaner.Tests.Architecture.DependencyGate;

/// <summary>
/// The rating of one module seam: "defined", "leaky" or "missing", the share of the top three target classes, the
/// concrete target classes (heaviest first) and whether the reverse direction crosses a concrete class.
/// </summary>
internal sealed record SeamRating(EdgeKey Modules, string Rating, double TopThreeShare, List<string> Concrete,
    bool ConcreteBack)
{
    public const string Defined = "defined";
    public const string Leaky = "leaky";
    public const string Missing = "missing";
}
