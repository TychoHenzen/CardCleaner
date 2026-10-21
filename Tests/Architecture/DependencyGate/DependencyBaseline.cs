using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Encodings.Web;
using System.Text.Json;

namespace CardCleaner.Tests.Architecture.DependencyGate;

/// <summary>
/// The ratchet over Tests/Architecture/dependency-baseline.json (#240, #299). The measured numbers are compared with
/// the baseline in priority order (GatedNumbers.Fields) and the first number that differs decides: if it went up the
/// gate fails naming it; if it went down the step is accepted, a rise in a lower-priority number with it, and the gate
/// fails until the baseline is re-recorded with the update switch; if no number differs the gate passes. The switch
/// writes the baseline when the deciding number went down, or when no number differs but the class lists are missing
/// or changed (record-only); it never writes after a refused rise. A missing or malformed baseline fails with its
/// path, switch or not. The class-name lists (NamedLists) never fail the gate: they join the failure text of the number
/// they explain and are printed as note lines.
/// </summary>
internal static class DependencyBaseline
{
    public const string SwitchVariable = "CARDCLEANER_UPDATE_DEPENDENCY_BASELINE";

    public const string UpdateCommand = SwitchVariable + "=1 <godot console exe> --headless --path . -s " +
                                        "res://addons/gdUnit4/bin/GdUnitCmdTool.gd -c --ignoreHeadlessMode " +
                                        "-rc 100000 -rd reports/ci -a res://Tests/Architecture/DependencyGateTest.cs";

    private const string NoListsNote = "baseline records no lists; run the update command to record them";

    private static readonly JsonSerializerOptions Indented = new()
    {
        WriteIndented = true,
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
    };

    public static BaselineReadResult Read(string path)
    {
        if (!File.Exists(path)) return new BaselineReadResult(null, $"dependency baseline {path} is missing", null);
        try
        {
            using var document = JsonDocument.Parse(File.ReadAllText(path));
            var values = new int[GatedNumbers.Fields.Count];
            for (var i = 0; i < values.Length; i++)
            {
                var name = GatedNumbers.Fields[i].Name;
                if (!document.RootElement.TryGetProperty(name, out var element) ||
                    !element.TryGetInt32(out values[i]))
                    return new BaselineReadResult(null, $"dependency baseline {path} is malformed: no number {name}",
                        null);
            }

            var (lists, listFailure) = ReadLists(path, document.RootElement);
            if (listFailure is not null) return new BaselineReadResult(null, listFailure, null);
            return new BaselineReadResult(GatedNumbers.FromValues(values), null, lists);
        }
        catch (Exception e) when (e is JsonException or InvalidOperationException or IOException)
        {
            return new BaselineReadResult(null, $"dependency baseline {path} is malformed: {e.Message}", null);
        }
    }

    /// <summary>
    /// The four lists are optional as a group: none present means a numbers-only file (Lists null); some present
    /// without all of them, or one that is not an array of strings, is malformed.
    /// </summary>
    private static (NamedLists? Lists, string? Failure) ReadLists(string path, System.Text.Json.JsonElement root)
    {
        var names = NamedLists.Fields.Select(f => f.Name).ToList();
        if (!names.Any(n => root.TryGetProperty(n, out _))) return (null, null);

        var absent = names.FirstOrDefault(n => !root.TryGetProperty(n, out _));
        if (absent is not null) return (null, $"dependency baseline {path} is malformed: no list {absent}");

        var items = new List<IReadOnlyList<string>>();
        foreach (var name in names)
        {
            var element = root.GetProperty(name);
            if (element.ValueKind != JsonValueKind.Array ||
                element.EnumerateArray().Any(e => e.ValueKind != JsonValueKind.String))
                return (null, $"dependency baseline {path} is malformed: list {name} is not an array of strings");
            items.Add(element.EnumerateArray().Select(e => e.GetString()!).ToList());
        }

        return (NamedLists.FromItems(items), null);
    }

    /// <summary>One diff per number that differs from its baseline, in priority order (GatedNumbers.Fields).</summary>
    public static List<NumberDiff> Compare(GatedNumbers baseline, GatedNumbers measured) =>
        GatedNumbers.Fields
            .Select(f => new NumberDiff(f.Name, f.Value(baseline), f.Value(measured)))
            .Where(d => d.Measured != d.Baseline)
            .ToList();

    /// <summary>
    /// The ratchet. Without the switch: the decision's failures. With it: rewrite the file when the deciding number
    /// went down (and report the accepted rises), or when no number differs but the class lists are missing or changed
    /// (record-only). A rise that is refused never writes, and a missing or malformed file always fails and is never
    /// written, so neither can bypass the ratchet.
    /// </summary>
    public static BaselineResult Apply(string path, GatedNumbers measured, NamedLists lists, bool update)
    {
        var read = Read(path);
        if (read.Numbers is null) return new BaselineResult([read.Failure!], [], []);

        var diffs = Compare(read.Numbers, measured);
        var decision = Decide(diffs, read.Lists, lists);
        if (!update) return decision;
        if (DecidingNumberWentDown(diffs))
        {
            Write(path, measured, lists);
            return decision with { Failures = [] };
        }

        if (diffs.Count == 0 && (read.Lists is null || ListsDiffer(read.Lists, lists)))
            Write(path, measured, lists);
        return decision;
    }

    private static bool ListsDiffer(NamedLists before, NamedLists after) =>
        NamedLists.Fields.Any(row =>
            !row.Items(before).OrderBy(x => x, StringComparer.Ordinal)
                .SequenceEqual(row.Items(after).OrderBy(x => x, StringComparer.Ordinal), StringComparer.Ordinal));

    private static bool IsRefused(List<NumberDiff> diffs) => diffs.Count > 0 && diffs[0].IsHigher;

    private static bool DecidingNumberWentDown(List<NumberDiff> diffs) => diffs.Count > 0 && !diffs[0].IsHigher;

    /// <summary>
    /// The first diff in priority order decides (#299). Up: refused, with the deciding number's message first and
    /// every other rise after it, and no lowering instruction. Down: each lowered number's lowering message and each
    /// rise's accepted-rise message, in priority order, so the author re-records the baseline knowingly. A number whose
    /// class list changed gets the joined and left classes after its message.
    /// </summary>
    private static BaselineResult Decide(List<NumberDiff> diffs, NamedLists? baselineLists, NamedLists measuredLists)
    {
        var notes = ListNotes(baselineLists, measuredLists);
        if (diffs.Count == 0) return new BaselineResult([], [], notes);
        if (IsRefused(diffs))
            return new BaselineResult(
                diffs.Where(d => d.IsHigher).Select(d => d.Message(ListDetail(d, baselineLists, measuredLists)))
                    .ToList(),
                [],
                notes);

        var deciding = diffs[0].Name;
        var rises = diffs.Where(d => d.IsHigher)
            .Select(d => d.AcceptedRiseMessage(deciding, ListDetail(d, baselineLists, measuredLists)))
            .ToList();
        var failures = diffs.Select(d => d.IsHigher
                ? d.AcceptedRiseMessage(deciding, ListDetail(d, baselineLists, measuredLists))
                : d.Message(ListDetail(d, baselineLists, measuredLists)))
            .ToList();
        return new BaselineResult(failures, rises, notes);
    }

    /// <summary>
    /// The joined and left classes of the list that explains this number; null when that list is unchanged.
    /// </summary>
    private static string? ListDetail(NumberDiff diff, NamedLists? baselineLists, NamedLists measuredLists)
    {
        if (baselineLists is null) return null;
        var row = NamedLists.Fields.Single(f => f.Number == diff.Name);
        var before = row.Items(baselineLists);
        var after = row.Items(measuredLists);
        return Differs(before, after) ? NamedLists.JoinedLeft(row.Name, before, after) : null;
    }

    /// <summary>
    /// One note line per list that differs; one line saying the baseline has no lists when it has none.
    /// </summary>
    private static IReadOnlyList<string> ListNotes(NamedLists? baselineLists, NamedLists measuredLists)
    {
        if (baselineLists is null) return [NoListsNote];
        return NamedLists.Fields
            .Where(row => Differs(row.Items(baselineLists), row.Items(measuredLists)))
            .Select(row => NamedLists.JoinedLeft(row.Name, row.Items(baselineLists), row.Items(measuredLists)))
            .ToList();
    }

    private static bool Differs(IReadOnlyList<string> before, IReadOnlyList<string> after) =>
        before.Except(after, StringComparer.Ordinal).Any() || after.Except(before, StringComparer.Ordinal).Any();

    /// <summary>
    /// Writes the numbers in GatedNumbers.Fields order, then the four class lists in NamedLists.Fields order, each
    /// list sorted ordinally. Internal so the fixture suite can record a baseline file.
    /// </summary>
    internal static void Write(string path, GatedNumbers numbers, NamedLists lists)
    {
        var fields = new Dictionary<string, object>(StringComparer.Ordinal);
        foreach (var (name, value) in GatedNumbers.Fields) fields.Add(name, value(numbers));
        foreach (var (name, _, items) in NamedLists.Fields)
            fields.Add(name, items(lists).OrderBy(x => x, StringComparer.Ordinal).ToArray());
        var json = JsonSerializer.Serialize(fields, Indented);
        File.WriteAllText(path, json.Replace("\r\n", "\n", StringComparison.Ordinal) + "\n");
    }
}
