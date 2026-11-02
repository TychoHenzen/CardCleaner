using System.Linq;
using System.Text.RegularExpressions;
using Godot;
using GodotDictionary = Godot.Collections.Dictionary;

namespace CardCleaner.Tests.Features.AssetManager;

/// <summary>
/// Contract for the vendored Asset-Manager plugin (addons/asset_manager): it is enabled in project.godot, and
/// every asset type it knows exports into the Assets submodule.
/// </summary>
[TestSuite]
[RequireGodotRuntime]
public class AssetManagerSetupTest
{
    private const string PluginConfig = "res://addons/asset_manager/plugin.cfg";
    private const string AssetTypesScript = "res://addons/asset_manager/core/asset_types.gd";
    private const string ExportRootsSetting = "asset_manager/export_roots";
    private const string AssetsPrefix = "res://Assets/";

    [TestCase]
    [TestCategory("Unit")]
    public static void PluginIsEnabled()
    {
        var enabled = ProjectSettings.GetSetting("editor_plugins/enabled").AsStringArray();
        AssertThat(enabled.Contains(PluginConfig)).IsTrue();
    }

    [TestCase]
    [TestCategory("Unit")]
    public static void EveryExportRootIsInsideTheAssetsSubmodule()
    {
        var roots = ExportRoots();
        AssertThat(roots.Count).IsGreater(0);

        var outside = roots
            .Where(pair => !pair.Value.AsString().StartsWith(AssetsPrefix, System.StringComparison.Ordinal))
            .Select(pair => $"{pair.Key.AsString()} -> {pair.Value.AsString()}")
            .ToList();
        AssertThat(string.Join(", ", outside)).IsEmpty();
    }

    [TestCase]
    [TestCategory("Unit")]
    public static void ExportRootsCoverEveryAssetType()
    {
        var typeIds = AssetTypeIds();
        AssertThat(typeIds.Length).IsGreater(0);

        var keys = ExportRoots().Keys.Select(key => key.AsString()).ToList();
        var missing = typeIds.Except(keys).ToList();
        var unknown = keys.Except(typeIds).ToList();
        AssertThat(string.Join(", ", missing)).IsEmpty();
        AssertThat(string.Join(", ", unknown)).IsEmpty();
    }

    private static GodotDictionary ExportRoots()
    {
        return ProjectSettings.GetSetting(ExportRootsSetting).AsGodotDictionary();
    }

    private static string[] AssetTypeIds()
    {
        var source = FileAccess.GetFileAsString(AssetTypesScript);
        return Regex.Matches(source, "\"id\":\\s*\"([^\"]+)\"")
            .Select(match => match.Groups[1].Value)
            .ToArray();
    }
}
