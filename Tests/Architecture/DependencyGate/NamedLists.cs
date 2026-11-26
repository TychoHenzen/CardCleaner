using System;
using System.Collections.Generic;
using System.Linq;

namespace CardCleaner.Tests.Architecture.DependencyGate;

/// <summary>
/// The class names behind each gated number: the up edge pairs, the classes of the biggest loop, the classes of the
/// defective seams, the references into UI, and the up edge pairs and the classes of the biggest loop inside UI.
/// Diagnostics only, never gated.
/// </summary>
internal sealed record NamedLists(IReadOnlyList<string> UpEdgePairs, IReadOnlyList<string> BiggestLoopClasses,
    IReadOnlyList<string> SeamClasses, IReadOnlyList<string> UiRefPairs, IReadOnlyList<string> UiUpEdgePairs,
    IReadOnlyList<string> UiLoopClasses)
{
    /// <summary>
    /// The one table of the lists: the name each has in the baseline file, the gated number it explains, and the
    /// accessor. Its order is the file order of the lists.
    /// </summary>
    public static readonly IReadOnlyList<(string Name, string Number, Func<NamedLists, IReadOnlyList<string>> Items)>
        Fields =
    [
        ("up_edge_pairs", "up_edges", l => l.UpEdgePairs),
        ("biggest_loop_classes", "biggest_loop", l => l.BiggestLoopClasses),
        ("seam_classes", "seams_leaky_or_missing", l => l.SeamClasses),
        ("ui_ref_pairs", "refs_from_non_ui_to_ui", l => l.UiRefPairs),
        ("ui_up_edge_pairs", "ui_up_edges", l => l.UiUpEdgePairs),
        ("ui_loop_classes", "ui_biggest_loop", l => l.UiLoopClasses),
    ];

    public static NamedLists FromItems(IReadOnlyList<IReadOnlyList<string>> items) =>
        new(items[0], items[1], items[2], items[3], items[4], items[5]);

    /// <summary>
    /// The text that says which entries joined and which left one list: "list joined: A, B; left: C", with "none" for
    /// an empty set, both in ordinal order.
    /// </summary>
    public static string JoinedLeft(string listName, IReadOnlyList<string> baseline, IReadOnlyList<string> measured)
    {
        var joined = Join(measured.Except(baseline, StringComparer.Ordinal));
        var left = Join(baseline.Except(measured, StringComparer.Ordinal));
        return $"{listName} joined: {joined}; left: {left}";
    }

    private static string Join(IEnumerable<string> items)
    {
        var sorted = items.OrderBy(x => x, StringComparer.Ordinal).ToList();
        return sorted.Count == 0 ? "none" : string.Join(", ", sorted);
    }
}
