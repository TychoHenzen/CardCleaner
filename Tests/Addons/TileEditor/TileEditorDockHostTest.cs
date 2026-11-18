#if TOOLS
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using CardCleaner.Addons.TileEditor;
using GdUnit4;
using Godot;

namespace CardCleaner.Tests.Addons.TileEditor;

/// <summary>
/// The Tile Editor dock loads the whole TileSet (576 textures) when it is built, which took the editor from 0.8 GB
/// to 4.5 GB at startup. The plugin therefore registers an empty host that builds the dock only once shown.
/// </summary>
[TestSuite]
[RequireGodotRuntime]
public class TileEditorDockHostTest
{
    private const string PluginSource = "res://addons/tile_editor/TileEditorPlugin.cs";

    [TestCase]
    public static void ThePluginRegistersTheHostAndNeverBuildsTheDockItself()
    {
        var source = FileAccess.GetFileAsString(PluginSource);
        AssertThat(source).IsNotEmpty();
        AssertThat(Regex.IsMatch(source, @"new\s+TileEditorDockHost\s*\(")).IsTrue();
        AssertThat(Regex.IsMatch(source, @"new\s+TileEditorDock\s*\(")).IsFalse();
    }

    [TestCase]
    public static async Task AHiddenHostBuildsNoDockUntilItIsShown()
    {
        var host = new TileEditorDockHost
        {
            Visible = false,
            ConfigureDock = dock =>
            {
                dock.SkipInitialTileLoad = true;
                dock.SuppressDialogs = true;
            }
        };
        AddNode(host);
        await WaitForFrames(3);

        AssertThat(host.Dock).IsNull();
        AssertThat(host.GetChildCount()).IsEqual(0);

        host.Visible = true;
        await WaitForFrames(2);

        AssertThat(host.Dock).IsNotNull();
        AssertThat(host.Dock!.GetParent()).IsSame(host);
    }

    private static async Task WaitForFrames(int count)
    {
        for (var index = 0; index < count; index++)
            await ISceneRunner.SyncProcessFrame;
    }
}
#endif
