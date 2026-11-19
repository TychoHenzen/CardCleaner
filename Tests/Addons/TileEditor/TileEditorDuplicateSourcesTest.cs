#if TOOLS
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using CardCleaner.Addons.TileEditor;
using Godot;

namespace CardCleaner.Tests.Addons.TileEditor;

/// <summary>
/// Duplicate atlas sources are found by pixel content. The scan decodes textures, so it runs only when its button
/// is pressed: run on every panel refresh it held every decoded image of the TileSet at once (1.8 GB).
/// </summary>
[TestSuite]
[RequireGodotRuntime]
public class TileEditorDuplicateSourcesTest
{
    private const string PanelDisplaySource = "res://addons/tile_editor/UnusedSources/UnusedSourcesPanelDisplay.cs";

    [TestCase]
    public static void SourcesWithIdenticalPixelsAreGroupedByLowestId()
    {
        var tileSet = new TileSet();
        AddSource(tileSet, 0, Filled(16, 16, Colors.Red));
        AddSource(tileSet, 1, Filled(16, 16, Colors.Blue));
        AddSource(tileSet, 2, Filled(16, 16, Colors.Red));
        AddSource(tileSet, 3, Filled(8, 8, Colors.Red));
        AddSource(tileSet, 5, Filled(16, 16, Colors.Blue));
        AddSource(tileSet, 6, Filled(16, 16, Colors.Green));
        var service = new TileEditorService();
        service.UseTileSetForTesting(tileSet);

        var groups = service.FindDuplicateSources();

        AssertThat(Describe(groups)).IsEqual("0,2 | 1,5");
    }

    [TestCase]
    public static void ATileSetWithoutDuplicatesHasNoGroups()
    {
        var tileSet = new TileSet();
        AddSource(tileSet, 0, Filled(16, 16, Colors.Red));
        AddSource(tileSet, 1, Filled(16, 16, Colors.Blue));
        var service = new TileEditorService();
        service.UseTileSetForTesting(tileSet);

        AssertThat(service.FindDuplicateSources().Count).IsEqual(0);
    }

    [TestCase]
    public static void APanelRefreshNeverScansForDuplicates()
    {
        var source = FileAccess.GetFileAsString(PanelDisplaySource);
        AssertThat(source).IsNotEmpty();
        AssertThat(Regex.IsMatch(source, @"FindDuplicateSources\s*\(")).IsFalse();
    }

    private static void AddSource(TileSet tileSet, int id, Image image)
    {
        var source = new TileSetAtlasSource
        {
            Texture = ImageTexture.CreateFromImage(image),
            TextureRegionSize = new Vector2I(image.GetWidth(), image.GetHeight())
        };
        tileSet.AddSource(source, id);
    }

    private static Image Filled(int width, int height, Color color)
    {
        var image = Image.CreateEmpty(width, height, false, Image.Format.Rgba8);
        image.Fill(color);
        return image;
    }

    private static string Describe(IEnumerable<List<int>> groups) =>
        string.Join(" | ", groups.Select(group => string.Join(",", group)));
}
#endif
