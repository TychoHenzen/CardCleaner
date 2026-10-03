using System.Linq;
using System.Text.Json;
using CardCleaner.Scripts.Core.Services;
using Godot;

namespace CardCleaner.Tests.Core.Services.CompiledAtlasLoaderScenarios;

/// <summary>
///     Validates CompiledAtlasLoader coordinate translation and atlas bounds; split out of CompiledAtlasLoaderTest.
/// </summary>
[TestSuite]
[RequireGodotRuntime]
public class CompiledAtlasCoordinateTest : CompiledAtlasLoaderTestBase
{
    // ==================== Coordinate Translation ====================

    [TestCase]
    public void TestTranslateCoordinatesReturnsValidCoords()
    {
        AssertThat(_mapping).IsNotNull();
        AssertThat(_mapping!.Sources).IsNotNull();

        // Pick the first available source/coord pair
        var (sourceIdStr, coordMappings) = _mapping.Sources!.First();
        var sourceId = int.Parse(sourceIdStr);
        var (coordKey, rect) = coordMappings.First();
        var parts = coordKey.Split(',');
        var originalCoords = new Vector2I(int.Parse(parts[0]), int.Parse(parts[1]));

        var (newSourceId, newCoords) = CompiledAtlasLoader.TranslateCoordinates(
            sourceId, originalCoords, _mapping);

        // Should return sourceId 0 for compiled atlas
        AssertThat(newSourceId).IsEqual(0);
        // Should return the mapped coords
        AssertThat(newCoords.X).IsEqual(rect.X);
        AssertThat(newCoords.Y).IsEqual(rect.Y);
    }

    [TestCase]
    public void TestTranslateCoordinatesReturnsOriginalForUnmapped()
    {
        AssertThat(_mapping).IsNotNull();

        // Use an obviously invalid source ID
        var (newSourceId, newCoords) = CompiledAtlasLoader.TranslateCoordinates(
            99999, new Vector2I(999, 999), _mapping);

        // Should return original coords when no mapping exists
        AssertThat(newSourceId).IsEqual(99999);
        AssertThat(newCoords).IsEqual(new Vector2I(999, 999));
    }

    [TestCase]
    public void TestTranslateCoordinatesHandlesNullMapping()
    {
        var (newSourceId, newCoords) = CompiledAtlasLoader.TranslateCoordinates(
            4, new Vector2I(5, 5), null);

        // Should return original coords when mapping is null
        AssertThat(newSourceId).IsEqual(4);
        AssertThat(newCoords).IsEqual(new Vector2I(5, 5));
    }

    // ==================== Coordinate Bounds Validation ====================

    [TestCase]
    public void TestAllMappedCoordsAreWithinAtlasBounds()
    {
        AssertThat(_mapping).IsNotNull();
        AssertThat(_mapping!.Sources).IsNotNull();
        AssertThat(_mapping.Atlas).IsNotNull();

        var maxTileX = _mapping.Atlas!.Width / _mapping.Atlas.TileSize;
        var maxTileY = _mapping.Atlas.Height / _mapping.Atlas.TileSize;
        var outOfBounds = new System.Collections.Generic.List<string>();

        foreach (var (sourceId, coordMappings) in _mapping.Sources!)
        {
            foreach (var (coordKey, rect) in coordMappings)
            {
                if (rect.X < 0 || rect.X >= maxTileX)
                    outOfBounds.Add($"source {sourceId} -> ({rect.X},{rect.Y}): x out of bounds [0, {maxTileX - 1}]");
                if (rect.Y < 0 || rect.Y >= maxTileY)
                    outOfBounds.Add($"source {sourceId} -> ({rect.X},{rect.Y}): y out of bounds [0, {maxTileY - 1}]");
            }
        }

        if (outOfBounds.Count > 0)
            GD.PrintErr($"Out of bounds atlas mappings:\n{string.Join("\n", outOfBounds.Take(20))}...");

        AssertThat(outOfBounds.Count).IsEqual(0);
    }

    [TestCase]
    public void TestAllTransitionCoordsAreWithinAtlasBounds()
    {
        AssertThat(_transitionDoc).IsNotNull();
        AssertThat(_mapping).IsNotNull();
        AssertThat(_mapping!.Atlas).IsNotNull();

        var maxTileX = _mapping.Atlas!.Width / _mapping.Atlas.TileSize;
        var maxTileY = _mapping.Atlas.Height / _mapping.Atlas.TileSize;
        var outOfBounds = new System.Collections.Generic.List<string>();

        var root = _transitionDoc!.RootElement;
        if (root.TryGetProperty("transitions", out var transitions))
        {
            foreach (var transition in transitions.EnumerateObject())
            {
                var key = transition.Name;
                var entry = transition.Value;

                if (!entry.TryGetProperty("variants", out var variants))
                    continue;

                var idx = 0;
                foreach (var variantArray in variants.EnumerateArray())
                {
                    // Each variant is an array of coordinate objects (for visual variations)
                    if (variantArray.ValueKind != JsonValueKind.Null && variantArray.ValueKind == JsonValueKind.Array)
                    {
                        foreach (var coord in variantArray.EnumerateArray())
                        {
                            var x = coord.GetProperty("x").GetInt32();
                            var y = coord.GetProperty("y").GetInt32();

                            if (x < 0 || x >= maxTileX)
                                outOfBounds.Add($"{key}[{idx}]: x={x} out of bounds [0, {maxTileX - 1}]");
                            if (y < 0 || y >= maxTileY)
                                outOfBounds.Add($"{key}[{idx}]: y={y} out of bounds [0, {maxTileY - 1}]");
                        }
                    }
                    idx++;
                }
            }
        }

        if (outOfBounds.Count > 0)
            GD.PrintErr($"Out of bounds transition coordinates:\n{string.Join("\n", outOfBounds.Take(20))}...");

        AssertThat(outOfBounds.Count).IsEqual(0);
    }
}
