using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;

namespace CardCleaner.Tests.Architecture.DependencyGate;

/// <summary>
/// The ratchet over Tests/Architecture/dependency-baseline.json (#240 AC-4, AC-5): a measured number above its
/// baseline fails; one below it fails until the baseline is lowered with the update switch. A missing or malformed
/// baseline fails with its path, switch or not, so the switch never writes a baseline that would raise a number.
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

    /// <summary>One diff per number that differs from its baseline, in file order.</summary>
    public static List<NumberDiff> Compare(GatedNumbers baseline, GatedNumbers measured) =>
        GatedNumbers.Fields
            .Select(f => new NumberDiff(f.Name, f.Value(baseline), f.Value(measured)))
            .Where(d => d.Measured != d.Baseline)
            .ToList();

    /// <summary>
    /// The ratchet. Without the switch: the message of every differing number. With it: rewrite the file unless a
    /// number is higher. A missing or malformed file always fails, so neither can bypass the ratchet.
    /// </summary>
    public static List<string> Apply(string path, GatedNumbers measured, bool update)
    {
        var read = Read(path);
        if (read.Numbers is null) return [read.Failure!];

        var diffs = Compare(read.Numbers, measured);
        if (!update) return diffs.Select(d => d.Message).ToList();
        var higher = diffs.Where(d => d.IsHigher).Select(d => d.Message).ToList();
        if (higher.Count > 0) return higher;
        Write(path, measured);
        return [];
    }

    private static void Write(string path, GatedNumbers numbers)
    {
        var fields = new Dictionary<string, int>(StringComparer.Ordinal);
        foreach (var (name, value) in GatedNumbers.Fields) fields.Add(name, value(numbers));
        var json = JsonSerializer.Serialize(fields, Indented);
        File.WriteAllText(path, json.Replace("\r\n", "\n", StringComparison.Ordinal) + "\n");
    }
}
