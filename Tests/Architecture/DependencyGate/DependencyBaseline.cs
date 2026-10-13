using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;

namespace CardCleaner.Tests.Architecture.DependencyGate;

/// <summary>
/// The ratchet over Tests/Architecture/dependency-baseline.json (#240 AC-4, AC-5): a measured number above its
/// baseline fails; one below it fails until the baseline is lowered with the update switch. The switch never writes
/// a baseline that would raise a number.
/// </summary>
internal static class DependencyBaseline
{
    public const string SwitchVariable = "CARDCLEANER_UPDATE_DEPENDENCY_BASELINE";

    public const string UpdateCommand = SwitchVariable + "=1 <godot console exe> --headless --path . -s " +
                                        "res://addons/gdUnit4/bin/GdUnitCmdTool.gd -c --ignoreHeadlessMode -rc 100000 " +
                                        "-rd reports/ci -a res://Tests/Architecture/DependencyGateTest.cs";

    private static readonly JsonSerializerOptions Indented = new() { WriteIndented = true };

    private static readonly string[] Names = ["up_edges", "biggest_loop", "seams_leaky", "seams_missing",
        "refs_from_non_ui_to_ui"];

    public static BaselineReadResult Read(string path)
    {
        if (!File.Exists(path)) return new BaselineReadResult(null, $"dependency baseline {path} is missing");
        try
        {
            using var document = JsonDocument.Parse(File.ReadAllText(path));
            var values = new int[Names.Length];
            for (var i = 0; i < Names.Length; i++)
            {
                if (!document.RootElement.TryGetProperty(Names[i], out var element) || !element.TryGetInt32(out values[i]))
                    return new BaselineReadResult(null, $"dependency baseline {path} is malformed: no number {Names[i]}");
            }

            return new BaselineReadResult(new GatedNumbers(values[0], values[1], values[2], values[3], values[4]), null);
        }
        catch (Exception e) when (e is JsonException or InvalidOperationException or IOException)
        {
            return new BaselineReadResult(null, $"dependency baseline {path} is malformed: {e.Message}");
        }
    }

    /// <summary>One message per number that differs from its baseline.</summary>
    public static List<string> Compare(GatedNumbers baseline, GatedNumbers measured)
    {
        var messages = new List<string>();
        var expected = Values(baseline);
        var actual = Values(measured);
        for (var i = 0; i < Names.Length; i++)
        {
            if (actual[i] > expected[i])
                messages.Add($"{Names[i]} is {actual[i]}, above the baseline {expected[i]}");
            else if (actual[i] < expected[i])
                messages.Add($"{Names[i]} is {actual[i]}, below the baseline {expected[i]}: lower the baseline with " +
                             UpdateCommand);
        }

        return messages;
    }

    /// <summary>
    /// The ratchet. Without the switch: the comparison messages. With it: rewrite the file unless a number is higher.
    /// ASSUMPTION: with the switch, a missing file is written fresh, so the first baseline can be recorded. A malformed
    /// file always fails, so corrupting it cannot bypass the ratchet.
    /// </summary>
    public static List<string> Apply(string path, GatedNumbers measured, bool update)
    {
        var read = Read(path);
        if (read.Numbers is null)
        {
            if (!update || File.Exists(path)) return [read.Failure!];
            Write(path, measured);
            return [];
        }

        var messages = Compare(read.Numbers, measured);
        if (!update) return messages;
        var higher = messages.Where(m => m.Contains("above the baseline", StringComparison.Ordinal)).ToList();
        if (higher.Count > 0) return higher;
        Write(path, measured);
        return [];
    }

    private static int[] Values(GatedNumbers n) => [n.UpEdges, n.BiggestLoop, n.SeamsLeaky, n.SeamsMissing,
        n.RefsFromNonUiToUi];

    private static void Write(string path, GatedNumbers numbers)
    {
        var json = JsonSerializer.Serialize(numbers, Indented);
        File.WriteAllText(path, json.Replace("\r\n", "\n", StringComparison.Ordinal) + "\n");
    }
}
