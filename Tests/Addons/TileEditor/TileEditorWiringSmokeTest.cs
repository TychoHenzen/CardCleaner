#if TOOLS
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using CardCleaner.Addons.TileEditor;
using Godot;

namespace CardCleaner.Tests.Addons.TileEditor;

[TestSuite]
[RequireGodotRuntime]
public class TileEditorWiringSmokeTest
{
    // The committed atlas the game loads. The smoke compile goes to the fixture folder and must leave these
    // untouched, write time included: Run-GdUnit.ps1 skips the Godot import while no file it reads changed, and a
    // rewritten file with the same bytes still counts as changed.
    private static readonly string[] CommittedAtlasPaths =
    {
        "res://Data/CompiledAtlas/terrain_atlas.png",
        "res://Data/CompiledAtlas/atlas_mapping.json",
        "res://Data/CompiledAtlas/transition_map.json"
    };

    private readonly Dictionary<string, DateTime> _committedAtlasWriteTimes = new();
    private TileEditorDock? _dock;
    private string _fixtureDirectory = string.Empty;

    [BeforeTest]
    public void BeforeTest()
    {
        foreach (var path in CommittedAtlasPaths)
            _committedAtlasWriteTimes[path] = File.GetLastWriteTimeUtc(ProjectSettings.GlobalizePath(path));

        _fixtureDirectory = $"user://tile-editor-smoke-{Guid.NewGuid():N}";
        Directory.CreateDirectory(ProjectSettings.GlobalizePath(_fixtureDirectory));
        WriteInvalidFixture();
    }

    [AfterTest]
    public void AfterTest()
    {
        var fixturePath = ProjectSettings.GlobalizePath(_fixtureDirectory);
        if (Directory.Exists(fixturePath))
            Directory.Delete(fixturePath, recursive: true);
    }

    [TestCase]
    public async Task PluginRegistrationAndToolbarActionsWorkHeadlessly()
    {
        var pluginConfig = new ConfigFile();
        Assertions.AssertThat(pluginConfig.Load("res://addons/tile_editor/plugin.cfg")).IsEqual(Error.Ok);
        Assertions.AssertThat(pluginConfig.GetValue("plugin", "script").AsString())
            .IsEqual($"{nameof(TileEditorPlugin)}.cs");

        var enabledPlugins = ProjectSettings.GetSetting("editor_plugins/enabled").AsStringArray();
        Assertions.AssertThat(enabledPlugins).Contains("res://addons/tile_editor/plugin.cfg");

        _dock = new TileEditorDock
        {
            TiledDirectory = _fixtureDirectory,
            CompiledAtlasDirectory = $"{_fixtureDirectory}/compiled",
            SuppressDialogs = true,
            SkipInitialTileLoad = true
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
        foreach (var fileName in new[] { "terrain_atlas.png", "atlas_mapping.json", "transition_map.json" })
            Assertions.AssertThat(File.Exists(GetFixturePath($"compiled/{fileName}"))).IsTrue();
        foreach (var path in CommittedAtlasPaths)
        {
            Assertions.AssertThat(File.GetLastWriteTimeUtc(ProjectSettings.GlobalizePath(path)))
                .OverrideFailureMessage($"{path} was rewritten by the smoke compile")
                .IsEqual(_committedAtlasWriteTimes[path]);
        }

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

#endif
