using System;
using System.Collections.Generic;

namespace CardCleaner.Tests.Architecture.DependencyGate;

/// <summary>The five numbers the dependency gate ratchets (#240 requirement 9).</summary>
internal sealed record GatedNumbers(int UpEdges, int BiggestLoop, int SeamsLeaky, int SeamsMissing,
    int RefsFromNonUiToUi)
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
        ("seams_leaky", n => n.SeamsLeaky),
        ("seams_missing", n => n.SeamsMissing),
        ("refs_from_non_ui_to_ui", n => n.RefsFromNonUiToUi),
    ];

    public static GatedNumbers FromValues(IReadOnlyList<int> values) =>
        new(values[0], values[1], values[2], values[3], values[4]);
}
