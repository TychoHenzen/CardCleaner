using System;
using System.Collections.Generic;

namespace CardCleaner.Tests.Architecture.DependencyGate;

/// <summary>
/// The four numbers the dependency gate ratchets (#240 requirement 9). Leaky and missing seams are one number, the
/// sum of both counts: Louvain membership changes can relabel a seam between missing and leaky with no dependency
/// change, so the label is not a stable ratchet target (#278, #239).
/// </summary>
internal sealed record GatedNumbers(int UpEdges, int BiggestLoop, int SeamsLeakyOrMissing, int RefsFromNonUiToUi)
{
    /// <summary>
    /// The one table of the numbers: the name each has in the baseline file and the report. Its order is both the file
    /// order and the ratchet's priority order (#299): the first number in this order that differs from the baseline
    /// decides the gate, so reordering the table changes what the gate accepts. FromValues takes the values in this
    /// order.
    /// </summary>
    public static readonly IReadOnlyList<(string Name, Func<GatedNumbers, int> Value)> Fields =
    [
        ("up_edges", n => n.UpEdges),
        ("biggest_loop", n => n.BiggestLoop),
        ("seams_leaky_or_missing", n => n.SeamsLeakyOrMissing),
        ("refs_from_non_ui_to_ui", n => n.RefsFromNonUiToUi),
    ];

    public static GatedNumbers FromValues(IReadOnlyList<int> values) =>
        new(values[0], values[1], values[2], values[3]);
}
