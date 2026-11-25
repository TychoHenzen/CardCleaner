using System.Collections.Generic;
using Godot;

namespace CardCleaner.Tests.Addons.ExportGuard;

/// <summary>
///     project.godot lists every autoload in an exported game too, so an autoload whose script the export skips would
///     log an error on every start. Each autoload target must be kept, or replaced by the guard's stand-in.
/// </summary>
[TestSuite]
[RequireGodotRuntime]
public class ExportAutoloadsTest
{
    private const string PolicyScript = "res://addons/export_guard/export_policy.gd";
    private const string ProjectFile = "res://project.godot";

    [TestCase]
    public static void NoAutoloadScriptIsSkippedByTheExport()
    {
        var config = new ConfigFile();
        AssertThat(config.Load(ProjectFile) == Error.Ok).IsTrue();
        var keys = config.GetSectionKeys("autoload");
        AssertThat(keys.Length).IsGreater(0);

        var policy = GD.Load<GDScript>(PolicyScript).New().AsGodotObject();
        var skipped = new List<string>();
        foreach (var key in keys)
        {
            var target = config.GetValue("autoload", key).AsString().TrimStart('*');
            if (policy.Call("decide", target).AsString() == "skip")
            {
                skipped.Add($"{key} -> {target}");
            }
        }

        AssertThat(string.Join(", ", skipped)).IsEmpty();
    }
}
