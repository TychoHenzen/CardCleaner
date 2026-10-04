using System.IO;
using System.Text.Json;
using CardCleaner.Scripts.Core.Services;
using Godot;

namespace CardCleaner.Tests.Core.Services.TilesJsonValidationScenarios;

/// <summary>
///     Shared tiles.json document and TileRegistry fixture for the tiles.json validation scenario suites.
/// </summary>
public abstract class TilesJsonValidationTestBase
{
    protected const string TilesJsonPath = "res://Data/Tiles/tiles.json";
    protected JsonDocument? _tilesDoc;
    protected TileRegistry _registry = null!;

    [BeforeTest]
    public void Setup()
    {
        var absolutePath = ProjectSettings.GlobalizePath(TilesJsonPath);
        if (File.Exists(absolutePath))
        {
            var json = File.ReadAllText(absolutePath);
            _tilesDoc = JsonDocument.Parse(json);
        }
        // Load registry explicitly from JSON (not from default TMX) for JSON validation tests
        _registry = new TileRegistry();
        _registry.Clear();
        _registry.LoadFromData(TilesJsonPath);
    }

    [AfterTest]
    public void Teardown()
    {
        _tilesDoc?.Dispose();
    }
}
