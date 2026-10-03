using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using CardCleaner.Addons.TileEditor;
using GdUnit4;
using Godot;

namespace CardCleaner.Tests.Addons.TileEditor;

[TestSuite]
[RequireGodotRuntime]
public class TileEditorWiringSmokeTest
{
    private static readonly string[] CompiledOutputRelativePaths =
    {
        "Data/CompiledAtlas/terrain_atlas.png",
        "Data/CompiledAtlas/atlas_mapping.json",
        "Data/CompiledAtlas/transition_map.json"
    };

    private readonly Dictionary<string, byte[]> _compiledOutputBackups = new();
    private readonly HashSet<string> _missingCompiledOutputs = new();
    private TileEditorDock? _dock;
    private string _fixtureDirectory = string.Empty;

    [BeforeTest]
    public void BeforeTest()
    {
        foreach (var relativePath in CompiledOutputRelativePaths)
        {
            var absolutePath = ProjectSettings.GlobalizePath($"res://{relativePath}");
            if (File.Exists(absolutePath))
                _compiledOutputBackups[relativePath] = File.ReadAllBytes(absolutePath);
            else
                _missingCompiledOutputs.Add(relativePath);
        }

        _fixtureDirectory = $"user://tile-editor-smoke-{Guid.NewGuid():N}";
        Directory.CreateDirectory(ProjectSettings.GlobalizePath(_fixtureDirectory));
        WriteInvalidFixture();
    }

    [AfterTest]
    public void AfterTest()
    {
        foreach (var relativePath in CompiledOutputRelativePaths)
        {
            var absolutePath = ProjectSettings.GlobalizePath($"res://{relativePath}");
            if (_compiledOutputBackups.TryGetValue(relativePath, out var contents))
                File.WriteAllBytes(absolutePath, contents);
            else if (_missingCompiledOutputs.Contains(relativePath) && File.Exists(absolutePath))
                File.Delete(absolutePath);
        }

        var fixturePath = ProjectSettings.GlobalizePath(_fixtureDirectory);
        if (Directory.Exists(fixturePath))
            Directory.Delete(fixturePath, recursive: true);
    }

    [TestCase]
    public async Task PluginRegistrationAndToolbarActionsWorkHeadlessly()
    {
        var pluginConfig = new ConfigFile();
        Assertions.AssertThat(pluginConfig.Load("res://addons/tile_editor/plugin.cfg")).IsEqual(Error.Ok);
        Assertions.AssertThat(pluginConfig.GetValue("plugin", "script").AsString()).IsEqual("TileEditorPlugin.cs");

        var enabledPlugins = ProjectSettings.GetSetting("editor_plugins/enabled").AsStringArray();
        Assertions.AssertThat(enabledPlugins).Contains("res://addons/tile_editor/plugin.cfg");

        _dock = new TileEditorDock
        {
            TiledDirectory = _fixtureDirectory,
            SuppressDialogs = true
        };
        Assertions.AddNode(_dock);
        await WaitForFrames(2);

        var compileButton = _dock.CompileTmxButton;
        var insertButton = _dock.InsertTsxPropsButton;
        Assertions.AssertThat(compileButton).IsNotNull();
        Assertions.AssertThat(insertButton).IsNotNull();
        Assertions.AssertThat(compileButton!.Text).IsEqual("Compile from TMX");
        Assertions.AssertThat(insertButton!.Text).IsEqual("Insert TSX Props");

        compileButton.EmitSignal(Button.SignalName.Pressed);
        await WaitForFrames(2);
        var failed = FindDescendants<Label>(_dock)
            .Any(label => label.Text.Contains("TMX compilation failed"));
        Assertions.AssertThat(failed).IsTrue();

        WriteValidFixture();
        compileButton.EmitSignal(Button.SignalName.Pressed);
        await WaitForFrames(2);
        var completed = FindDescendants<Label>(_dock)
            .Any(label => label.Text == "TMX compilation complete!");
        Assertions.AssertThat(completed).IsTrue();
        var atlasPath = ProjectSettings.GlobalizePath(
            "res://Data/CompiledAtlas/terrain_atlas.png");
        Assertions.AssertThat(File.Exists(atlasPath)).IsTrue();

        insertButton.EmitSignal(Button.SignalName.Pressed);
        await WaitForFrames(1);
        var inserted = FindDescendants<Label>(_dock)
            .Any(label => label.Text.StartsWith("Inserted properties into"));
        Assertions.AssertThat(inserted).IsTrue();
        Assertions.AssertThat(File.ReadAllText(GetFixturePath("smoke.tsx"))).Contains("passability");
    }

    private void WriteInvalidFixture()
    {
        File.WriteAllText(GetFixturePath("smoke.tsx"), """
            <?xml version="1.0" encoding="UTF-8"?>
            <tileset version="1.10" name="smoke" tilewidth="16" tileheight="16" tilecount="1" columns="1">
              <image source="missing.png" width="16" height="16"/>
              <tile id="0" type="smoke_tile"/>
            </tileset>
            """);
    }

    private void WriteValidFixture()
    {
        var image = Image.CreateEmpty(16, 16, false, Image.Format.Rgba8);
        Assertions.AssertThat(image.SavePng(GetFixturePath("smoke.png"))).IsEqual(Error.Ok);
        File.WriteAllText(GetFixturePath("smoke.tsx"), """
            <?xml version="1.0" encoding="UTF-8"?>
            <tileset version="1.10" name="smoke" tilewidth="16" tileheight="16" tilecount="1" columns="1">
              <image source="smoke.png" width="16" height="16"/>
              <tile id="0" type="smoke_tile"/>
            </tileset>
            """);
    }

    private string GetFixturePath(string fileName) =>
        Path.Combine(ProjectSettings.GlobalizePath(_fixtureDirectory), fileName);

    private static async Task WaitForFrames(int count)
    {
        for (var index = 0; index < count; index++)
            await ISceneRunner.SyncProcessFrame;
    }

    private static IEnumerable<T> FindDescendants<T>(Node root) where T : Node
    {
        foreach (var child in root.GetChildren())
        {
            if (child is T match)
                yield return match;

            foreach (var descendant in FindDescendants<T>(child))
                yield return descendant;
        }
    }

}
