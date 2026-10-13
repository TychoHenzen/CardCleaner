using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;

namespace CardCleaner.Tests.Architecture.DependencyGate;

/// <summary>
/// The ratchet over Tests/Architecture/dependency-baseline.json (#240, #299). The measured numbers are compared with
/// the baseline in priority order (GatedNumbers.Fields) and the first number that differs decides: if it went up the
/// gate fails naming it; if it went down the step is accepted, a rise in a lower-priority number with it, and the gate
/// fails until the baseline is re-recorded with the update switch. The switch never writes a baseline whose deciding
/// number went up, and a missing or malformed baseline fails with its path, switch or not.
/// </summary>
internal static class DependencyBaseline
{
    public const string SwitchVariable = "CARDCLEANER_UPDATE_DEPENDENCY_BASELINE";

    public const string UpdateCommand = SwitchVariable + "=1 <godot console exe> --headless --path . -s " +
                                        "res://addons/gdUnit4/bin/GdUnitCmdTool.gd -c --ignoreHeadlessMode " +
                                        "-rc 100000 -rd reports/ci -a res://Tests/Architecture/DependencyGateTest.cs";

    private static readonly JsonSerializerOptions Indented = new() { WriteIndented = true };

    public static BaselineReadResult Read(string path)
    {
        if (!File.Exists(path)) return new BaselineReadResult(null, $"dependency baseline {path} is missing");
        try
        {
            using var document = JsonDocument.Parse(File.ReadAllText(path));
            var values = new int[GatedNumbers.Fields.Count];
            for (var i = 0; i < values.Length; i++)
            {
                var name = GatedNumbers.Fields[i].Name;
                if (!document.RootElement.TryGetProperty(name, out var element) ||
                    !element.TryGetInt32(out values[i]))
                    return new BaselineReadResult(null, $"dependency baseline {path} is malformed: no number {name}");
            }

            return new BaselineReadResult(GatedNumbers.FromValues(values), null);
        }
        catch (Exception e) when (e is JsonException or InvalidOperationException or IOException)
        {
            return new BaselineReadResult(null, $"dependency baseline {path} is malformed: {e.Message}");
        }
    }

    /// <summary>One diff per number that differs from its baseline, in priority order (GatedNumbers.Fields).</summary>
    public static List<NumberDiff> Compare(GatedNumbers baseline, GatedNumbers measured) =>
        GatedNumbers.Fields
            .Select(f => new NumberDiff(f.Name, f.Value(baseline), f.Value(measured)))
            .Where(d => d.Measured != d.Baseline)
            .ToList();

    /// <summary>
    /// The ratchet. Without the switch: the decision's failures. With it: rewrite the file unless the deciding number
    /// went up, and report the accepted rises. A missing or malformed file always fails and is never written, so
    /// neither can bypass the ratchet.
    /// </summary>
    public static BaselineResult Apply(string path, GatedNumbers measured, bool update)
    {
        var read = Read(path);
        if (read.Numbers is null) return new BaselineResult([read.Failure!], []);

        var diffs = Compare(read.Numbers, measured);
        var decision = Decide(diffs);
        if (!update || IsRefused(diffs)) return decision;
        Write(path, measured);
        return decision with { Failures = [] };
    }

    private static bool IsRefused(List<NumberDiff> diffs) => diffs.Count > 0 && diffs[0].IsHigher;

    /// <summary>
    /// The first diff in priority order decides (#299). Up: refused, with the deciding number's message first and
    /// every other rise after it, and no lowering instruction. Down: each lowered number's lowering message and each
    /// rise's accepted-rise message, in priority order, so the author re-records the baseline knowingly.
    /// </summary>
    private static BaselineResult Decide(List<NumberDiff> diffs)
    {
        if (diffs.Count == 0) return new BaselineResult([], []);
        if (IsRefused(diffs))
            return new BaselineResult(diffs.Where(d => d.IsHigher).Select(d => d.Message).ToList(), []);

        var deciding = diffs[0].Name;
        var rises = diffs.Where(d => d.IsHigher).Select(d => d.AcceptedRiseMessage(deciding)).ToList();
        var failures = diffs.Select(d => d.IsHigher ? d.AcceptedRiseMessage(deciding) : d.Message).ToList();
        return new BaselineResult(failures, rises);
    }

    private static void Write(string path, GatedNumbers numbers)
    {
        var fields = new Dictionary<string, int>(StringComparer.Ordinal);
        foreach (var (name, value) in GatedNumbers.Fields) fields.Add(name, value(numbers));
        var json = JsonSerializer.Serialize(fields, Indented);
        File.WriteAllText(path, json.Replace("\r\n", "\n", StringComparison.Ordinal) + "\n");
    }
}
