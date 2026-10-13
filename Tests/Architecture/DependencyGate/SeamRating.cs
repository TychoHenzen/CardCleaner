using System.Collections.Generic;

namespace CardCleaner.Tests.Architecture.DependencyGate;

/// <summary>
/// The rating of one module seam: its kind, the share of the top three target classes, the concrete target classes
/// (heaviest first) and whether the reverse direction crosses a concrete class.
/// </summary>
internal sealed record SeamRating(EdgeKey Modules, SeamKind Rating, double TopThreeShare, List<string> Concrete,
    bool ConcreteBack);
