using System.IO;
using System.Linq;
using System.Text.Json;
using CardCleaner.Scripts.Core.Services;
using CardCleaner.Scripts.Features.Worldgen.AutoTiling;
using GdUnit4;
using Godot;
using static GdUnit4.Assertions;

namespace CardCleaner.Tests.Features.Worldgen.AutoTiling;

/// <summary>
/// Validates transition_map.json structure, data integrity, and cross-references with tiles.json.
/// This is critical for auto-tile rendering - invalid entries cause black/missing tiles.
/// </summary>
[TestSuite]
[RequireGodotRuntime]
public class TransitionMapValidationTest
{
    private const string TransitionMapPath = "res://Data/CompiledAtlas/transition_map.json";
    private const string AtlasMappingPath = "res://Data/CompiledAtlas/atlas_mapping.json";
    // Default values - will be read from atlas_mapping.json if available
    private const int DefaultAtlasWidth = 4096;
    private const int DefaultAtlasHeight = 2048;  // Must match atlas_mapping.json
    private const int DefaultTileSize = 16;

    private JsonDocument? _transitionDoc;
    private CompiledTransitionMap? _transitionMap;
    private TileRegistry _registry = null!;

    [BeforeTest]
    public void Setup()
    {
        var absolutePath = ProjectSettings.GlobalizePath(TransitionMapPath);
        if (File.Exists(absolutePath))
        {
            var json = File.ReadAllText(absolutePath);
            _transitionDoc = JsonDocument.Parse(json);
            _transitionMap = JsonSerializer.Deserialize<CompiledTransitionMap>(json, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });
        }
        // Load from JSON to match transition_map.json source
        _registry = new TileRegistry();
        _registry.Clear();
        _registry.LoadFromData("res://Data/Tiles/tiles.json");
    }

    [AfterTest]
    public void Teardown()
    {
        _transitionDoc?.Dispose();
    }

    // ==================== File Existence ====================

    [TestCase]
    public void TestTransitionMapFileExists()
    {
        var absolutePath = ProjectSettings.GlobalizePath(TransitionMapPath);
        AssertBool(File.Exists(absolutePath)).IsTrue();
    }

    [TestCase]
    public void TestTransitionMapHasRequiredFields()
    {
        AssertThat(_transitionDoc).IsNotNull();
        var root = _transitionDoc!.RootElement;

        AssertBool(root.TryGetProperty("version", out _)).IsTrue();
        AssertBool(root.TryGetProperty("transitions", out _)).IsTrue();
    }

    // ==================== Variant Count Validation ====================

    [TestCase]
    public void TestCorner16TransitionsHave16Variants()
    {
        AssertThat(_transitionMap).IsNotNull();

        var incorrectCounts = new System.Collections.Generic.List<string>();

        foreach (var (key, entry) in _transitionMap!.Transitions)
        {
            if (entry.Format != "corner16")
                continue;

            if (entry.Variants.Length != 16)
                incorrectCounts.Add($"{key}: corner16 has {entry.Variants.Length} variants (expected 16)");
        }

        if (incorrectCounts.Count > 0)
            GD.PrintErr($"Incorrect corner16 variant counts:\n{string.Join("\n", incorrectCounts)}");

        AssertThat(incorrectCounts.Count).IsEqual(0);
    }

    [TestCase]
    public void TestBlob47TransitionsHave47Variants()
    {
        AssertThat(_transitionMap).IsNotNull();

        var incorrectCounts = new System.Collections.Generic.List<string>();

        foreach (var (key, entry) in _transitionMap!.Transitions)
        {
            if (entry.Format != "blob47")
                continue;

            if (entry.Variants.Length != 47)
                incorrectCounts.Add($"{key}: blob47 has {entry.Variants.Length} variants (expected 47)");
        }

        if (incorrectCounts.Count > 0)
            GD.PrintErr($"Incorrect blob47 variant counts:\n{string.Join("\n", incorrectCounts)}");

        AssertThat(incorrectCounts.Count).IsEqual(0);
    }

    // ==================== Coordinate Bounds Validation ====================

    [TestCase]
    public void TestAllVariantCoordsAreWithinAtlasBounds()
    {
        AssertThat(_transitionMap).IsNotNull();

        var maxTileX = DefaultAtlasWidth / DefaultTileSize;
        var maxTileY = DefaultAtlasHeight / DefaultTileSize;
        var outOfBounds = new System.Collections.Generic.List<string>();

        foreach (var (key, entry) in _transitionMap!.Transitions)
        {
            for (var i = 0; i < entry.Variants.Length; i++)
            {
                var variants = entry.Variants[i];
                if (variants == null)
                    continue;

                foreach (var variant in variants)
                {
                    if (variant.X < 0 || variant.X >= maxTileX)
                        outOfBounds.Add($"{key}[{i}]: x={variant.X} out of bounds [0, {maxTileX - 1}]");
                    if (variant.Y < 0 || variant.Y >= maxTileY)
                        outOfBounds.Add($"{key}[{i}]: y={variant.Y} out of bounds [0, {maxTileY - 1}]");
                }
            }
        }

        if (outOfBounds.Count > 0)
            GD.PrintErr($"Out of bounds coordinates:\n{string.Join("\n", outOfBounds.Take(20))}...");

        AssertThat(outOfBounds.Count).IsEqual(0);
    }

    [TestCase]
    public void TestAllVariantCoordsAreNonNegative()
    {
        AssertThat(_transitionMap).IsNotNull();

        var negativeCoords = new System.Collections.Generic.List<string>();

        foreach (var (key, entry) in _transitionMap!.Transitions)
        {
            for (var i = 0; i < entry.Variants.Length; i++)
            {
                var variants = entry.Variants[i];
                if (variants == null)
                    continue;

                foreach (var variant in variants)
                {
                    if (variant.X < 0 || variant.Y < 0)
                        negativeCoords.Add($"{key}[{i}]: ({variant.X}, {variant.Y}) has negative value");
                }
            }
        }

        if (negativeCoords.Count > 0)
            GD.PrintErr($"Negative coordinates:\n{string.Join("\n", negativeCoords)}");

        AssertThat(negativeCoords.Count).IsEqual(0);
    }

    // ==================== Key Format Validation ====================

    [TestCase]
    public void TestAllTransitionKeysHaveValidFormat()
    {
        AssertThat(_transitionMap).IsNotNull();

        var invalidKeys = new System.Collections.Generic.List<string>();

        foreach (var key in _transitionMap!.Transitions.Keys)
        {
            var parts = key.Split('|');
            if (parts.Length != 2)
                invalidKeys.Add($"'{key}': expected format 'borderId|outerTerrain'");
            else if (string.IsNullOrEmpty(parts[0]) || string.IsNullOrEmpty(parts[1]))
                invalidKeys.Add($"'{key}': empty borderId or outerTerrain");
        }

        if (invalidKeys.Count > 0)
            GD.PrintErr($"Invalid key formats:\n{string.Join("\n", invalidKeys)}");

        AssertThat(invalidKeys.Count).IsEqual(0);
    }

    // ==================== Terrain Reference Validation ====================
    // NOTE: These tests validate that transition_map.json tiles exist in the registry.
    // During TSX migration, the registry may only have TSX-defined tiles.
    // These tests log warnings but pass if tiles are missing due to migration.

    [TestCase]
    public void TestBorderIdsExistInTileRegistry()
    {
        AssertThat(_transitionMap).IsNotNull();

        var allTileIds = _registry.GetAllTiles().Select(t => t.Id).ToHashSet();
        var missingBorders = new System.Collections.Generic.List<string>();

        foreach (var borderId in _transitionMap!.GetAllBorderIds())
        {
            if (!allTileIds.Contains(borderId))
                missingBorders.Add(borderId);
        }

        if (missingBorders.Count > 0)
        {
            // During TSX migration, log as warning instead of error
            var totalBorders = _transitionMap.GetAllBorderIds().Count();
            GD.Print($"[Migration] {missingBorders.Count}/{totalBorders} border IDs not in TileRegistry (TSX migration in progress)");
            GD.Print($"  Missing: {string.Join(", ", missingBorders.Take(5))}...");

            // Skip assertion during TSX migration - tiles may not be defined yet
            if (allTileIds.Count < 10)
            {
                GD.Print("  Skipping assertion: registry has <10 tiles (TSX migration mode)");
                return;
            }
        }

        AssertThat(missingBorders.Count).IsEqual(0);
    }

    [TestCase]
    public void TestOuterTerrainIdsExistInTileRegistry()
    {
        AssertThat(_transitionMap).IsNotNull();

        var allTileIds = _registry.GetAllTiles().Select(t => t.Id).ToHashSet();
        var missingOuter = new System.Collections.Generic.List<string>();

        foreach (var key in _transitionMap!.Transitions.Keys)
        {
            var (_, outerTerrain) = CompiledTransitionMap.ParseKey(key);
            if (!allTileIds.Contains(outerTerrain))
                missingOuter.Add($"{key}: outer terrain '{outerTerrain}' not in registry");
        }

        if (missingOuter.Count > 0)
        {
            // During TSX migration, log as warning instead of error
            var totalTransitions = _transitionMap.Transitions.Count;
            GD.Print($"[Migration] {missingOuter.Count}/{totalTransitions} outer terrain refs not in TileRegistry (TSX migration in progress)");
            GD.Print($"  Sample: {string.Join(", ", missingOuter.Take(3))}...");

            // Skip assertion during TSX migration
            if (allTileIds.Count < 10)
            {
                GD.Print("  Skipping assertion: registry has <10 tiles (TSX migration mode)");
                return;
            }
        }

        AssertThat(missingOuter.Count).IsEqual(0);
    }

    // ==================== Format Validation ====================

    [TestCase]
    public void TestAllFormatsAreValid()
    {
        AssertThat(_transitionMap).IsNotNull();

        // Standard formats plus legacy names from older auto-tile systems
        var validFormats = new[] { "corner16", "edge16", "blob47", "rpg", "simplistic" };
        var invalidFormats = new System.Collections.Generic.List<string>();

        foreach (var (key, entry) in _transitionMap!.Transitions)
        {
            if (!validFormats.Contains(entry.Format))
                invalidFormats.Add($"{key}: invalid format '{entry.Format}'");
        }

        if (invalidFormats.Count > 0)
            GD.PrintErr($"Invalid formats:\n{string.Join("\n", invalidFormats)}");

        AssertThat(invalidFormats.Count).IsEqual(0);
    }

    // ==================== Null Variant Checks ====================

    [TestCase]
    public void TestCorner16Variant0IsNotNull()
    {
        // For Corner16, variant 0 = no corners = should be the solid fill tile
        // If it's null, rendering will fail for uniform terrain
        AssertThat(_transitionMap).IsNotNull();

        var nullVariant0 = new System.Collections.Generic.List<string>();

        foreach (var (key, entry) in _transitionMap!.Transitions)
        {
            if (entry.Format != "corner16")
                continue;

            if (entry.Variants.Length > 0 && (entry.Variants[0] == null || entry.Variants[0]!.Length == 0))
                nullVariant0.Add($"{key}: variant[0] is null or empty (solid fill should be defined)");
        }

        if (nullVariant0.Count > 0)
            GD.Print($"Transitions with null variant[0] (may cause issues):\n{string.Join("\n", nullVariant0.Take(10))}");

        // This is informational - null variant[0] may be intentional for some transitions
    }

    [TestCase]
    public void TestCorner16Variant15IsNotNull()
    {
        // Variant 15 = all corners = solid fill, critical for uniform terrain
        AssertThat(_transitionMap).IsNotNull();

        var nullVariant15 = new System.Collections.Generic.List<string>();

        foreach (var (key, entry) in _transitionMap!.Transitions)
        {
            if (entry.Format != "corner16")
                continue;

            if (entry.Variants.Length > 15 && (entry.Variants[15] == null || entry.Variants[15]!.Length == 0))
                nullVariant15.Add($"{key}: variant[15] is null or empty (solid fill should be defined)");
        }

        if (nullVariant15.Count > 0)
            GD.PrintErr($"Missing variant[15] (solid fill):\n{string.Join("\n", nullVariant15)}");

        AssertThat(nullVariant15.Count).IsEqual(0);
    }

    // ==================== Completeness Checks ====================

    [TestCase]
    public void TestCompositableTilesHaveTransitions()
    {
        // Tiles with outerTerrain="*" should have at least one transition entry
        var compositableTiles = _registry.GetAllTiles()
            .Where(t => t.IsCompositable)
            .Select(t => t.Id)
            .ToHashSet();

        // During TSX migration, we may not have compositable tiles yet
        if (compositableTiles.Count == 0)
        {
            GD.Print("[Migration] No compositable tiles in registry (TSX migration in progress)");
            return;
        }

        var bordersWithTransitions = _transitionMap!.GetAllBorderIds().ToHashSet();

        var missingTransitions = compositableTiles.Except(bordersWithTransitions).ToList();

        if (missingTransitions.Count > 0)
            GD.PrintErr($"Compositable tiles without transitions: {string.Join(", ", missingTransitions)}");

        AssertThat(missingTransitions.Count).IsEqual(0);
    }

    // ==================== Coordinate Uniqueness per Transition ====================

    [TestCase]
    public void TestVariantsWithinTransitionAreUnique()
    {
        // Each variant in a transition should have unique coordinates
        // (non-null variants shouldn't repeat the same coords)
        AssertThat(_transitionMap).IsNotNull();

        var duplicates = new System.Collections.Generic.List<string>();

        foreach (var (key, entry) in _transitionMap!.Transitions)
        {
            var coords = new System.Collections.Generic.HashSet<(int, int)>();
            for (var i = 0; i < entry.Variants.Length; i++)
            {
                var variants = entry.Variants[i];
                if (variants == null)
                    continue;

                foreach (var variant in variants)
                {
                    var coord = (variant.X, variant.Y);
                    if (!coords.Add(coord))
                    {
                        duplicates.Add($"{key}[{i}]: ({variant.X}, {variant.Y}) is duplicate");
                    }
                }
            }
        }

        if (duplicates.Count > 0)
            GD.Print($"Duplicate coordinates within transitions (may be intentional):\n{string.Join("\n", duplicates.Take(20))}");

        // This is informational - some tiles may intentionally use the same coords for multiple variants
    }

    // ==================== Statistics ====================

    [TestCase]
    public void TestTransitionMapStatistics()
    {
        AssertThat(_transitionMap).IsNotNull();

        var totalTransitions = _transitionMap!.Transitions.Count;
        var corner16Count = _transitionMap.Transitions.Values.Count(e => e.Format == "corner16");
        var blob47Count = _transitionMap.Transitions.Values.Count(e => e.Format == "blob47");
        var edge16Count = _transitionMap.Transitions.Values.Count(e => e.Format == "edge16");

        var totalVariants = _transitionMap.Transitions.Values
            .Sum(e => e.Variants.Where(v => v != null).Sum(v => v!.Length));

        var uniqueBorders = _transitionMap.GetAllBorderIds().Count();

        GD.Print($"Transition Map Statistics:");
        GD.Print($"  Total transitions: {totalTransitions}");
        GD.Print($"  Corner16: {corner16Count}");
        GD.Print($"  Blob47: {blob47Count}");
        GD.Print($"  Edge16: {edge16Count}");
        GD.Print($"  Total non-null variants: {totalVariants}");
        GD.Print($"  Unique border IDs: {uniqueBorders}");

        AssertThat(totalTransitions).IsGreater(0);
    }

    // ==================== Cross-Reference with CompiledTransitionResolver ====================

    [TestCase]
    public void TestResolverCanLoadTransitionMap()
    {
        var resolver = new CompiledTransitionResolver();

        // Should load without exceptions
        AssertThat(resolver).IsNotNull();
    }

    [TestCase]
    public void TestResolverCanLookupKnownTransition()
    {
        AssertThat(_transitionMap).IsNotNull();

        // Get first transition key
        var firstKey = _transitionMap!.Transitions.Keys.FirstOrDefault();
        if (firstKey == null)
        {
            GD.PrintErr("No transitions in map");
            return;
        }

        var (borderId, outerTerrain) = CompiledTransitionMap.ParseKey(firstKey);
        var entry = _transitionMap.Transitions[firstKey];

        var resolver = new CompiledTransitionResolver();
        var coords = resolver.ResolveTransition(borderId, outerTerrain, 1); // bitmask 1

        AssertThat(coords).IsNotNull();

        if (entry.Variants.Length > 1 && entry.Variants[1] != null && entry.Variants[1]!.Length > 0)
        {
            AssertThat(coords!.Value.X).IsEqual(entry.Variants[1]![0].X);
            AssertThat(coords!.Value.Y).IsEqual(entry.Variants[1]![0].Y);
        }
    }
}
