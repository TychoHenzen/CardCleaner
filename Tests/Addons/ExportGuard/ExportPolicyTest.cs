using System.Collections.Generic;
using System.Linq;
using Godot;

namespace CardCleaner.Tests.Addons.ExportGuard;

/// <summary>
///     The export guard (addons/export_guard) keeps test files, the in-engine test framework and the AI-editor helper
///     out of an exported game. Its policy is plain GDScript, so what it decides for each kind of project file is
///     pinned here, together with the stand-in it ships for the helper autoload.
/// </summary>
[TestSuite]
[RequireGodotRuntime]
public class ExportPolicyTest
{
    private const string PolicyScript = "res://addons/export_guard/export_policy.gd";
    private const string PluginConfig = "res://addons/export_guard/plugin.cfg";
    private const string HelperScript = "res://addons/godot_ai/runtime/game_helper.gd";

    private static readonly Dictionary<string, string> Verdicts = new()
    {
        ["res://Tests/Features/Card/CardSignatureTest.cs"] = "skip",
        ["res://Tests/Scenes/Fixtures/RoundTripOverride.tscn"] = "skip",
        ["res://UnitTests/Features/Card/CardSignatureTest.cs"] = "skip",
        ["res://addons/gdUnit4/plugin.cfg"] = "skip",
        ["res://addons/gdUnit4/src/dotnet/GdUnit4CSharpApi.cs"] = "skip",
        ["res://addons/godot_ai/plugin.cfg"] = "skip",
        ["res://addons/godot_ai/runtime/game_logger.gd"] = "skip",
        ["res://addons/godot_ai/runtime/game_helper.gd"] = "stub",
        ["res://addons/export_guard/export_policy.gd"] = "skip",
        ["res://addons/saveable/plugin.cfg"] = "keep",
        ["res://addons/tile_editor/plugin.cfg"] = "keep",
        ["res://Scripts/Core/Interfaces/ILog.cs"] = "keep",
        ["res://Scenes/UI/MainMenu.tscn"] = "keep",
        ["res://TestData/notes.tres"] = "keep",
    };

    [TestCase]
    public static void ThePolicyScriptCompiles()
    {
        var script = GD.Load<GDScript>(PolicyScript);
        AssertThat(script).IsNotNull();
        AssertThat(script.CanInstantiate()).IsTrue();
    }

    [TestCase]
    public static void ThePluginIsEnabled()
    {
        var enabled = ProjectSettings.GetSetting("editor_plugins/enabled").AsStringArray();
        AssertThat(enabled.Contains(PluginConfig)).IsTrue();
        // The setting still lists the plugin when its folder is missing, so the config file itself must exist.
        AssertThat(FileAccess.FileExists(PluginConfig)).IsTrue();
    }

    [TestCase]
    public static void EachKindOfFileGetsItsVerdict()
    {
        var policy = NewPolicy();
        var wrong = Verdicts
            .Where(pair => Decide(policy, pair.Key) != pair.Value)
            .Select(pair => $"{pair.Key} is {Decide(policy, pair.Key)}, not {pair.Value}")
            .ToList();
        AssertThat(string.Join("; ", wrong)).IsEmpty();
    }

    [TestCase]
    public static void TheHelperStandInIsAnEmptyNodeThatCompiles()
    {
        var source = NewPolicy().Call("stub_source", HelperScript).AsString();
        var script = new GDScript { SourceCode = source };
        AssertThat(script.Reload() == Error.Ok).IsTrue();
        AssertThat(source.Length).IsLess(64);
    }

    private static GodotObject NewPolicy()
    {
        return GD.Load<GDScript>(PolicyScript).New().AsGodotObject();
    }

    private static string Decide(GodotObject policy, string path)
    {
        return policy.Call("decide", path).AsString();
    }
}
