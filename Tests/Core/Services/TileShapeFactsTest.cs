using System.Collections.Generic;
using CardCleaner.Features.Worldgen.AutoTiling;
using CardCleaner.Scripts.Core.Services;
using Godot;

namespace CardCleaner.Tests.Core.Services;

/// <summary>
/// Pins the shape facts TileShapeFacts resolves for an auto-tile format name: the allowed bitmasks and the largest
/// multi-cell bounds, or nothing when the format is unknown.
/// </summary>
[TestSuite]
[RequireGodotRuntime]
public class TileShapeFactsTest
{
    [TestCase]
    [TestCategory("Unit")]
    public static void Corner16ResolvesAllSixteenBitmasksAndNoBounds()
    {
        var shape = TileShapeFacts.Resolve("corner16");
        var allowed = shape.AllowedBitmasks;

        AssertThat(allowed).IsNotNull();
        AssertInt(allowed!.Count).IsEqual(16);
        AssertBool(allowed.Contains(0)).IsTrue();
        AssertBool(allowed.Contains(15)).IsTrue();
        AssertBool(shape.MultiCellBounds == null).IsTrue();
    }

    [TestCase]
    [TestCategory("Unit")]
    public static void UnknownFormatResolvesToNothing()
    {
        var shape = TileShapeFacts.Resolve("no_such_format");

        AssertBool(shape.AllowedBitmasks == null).IsTrue();
        AssertBool(shape.MultiCellBounds == null).IsTrue();
    }

    [TestCase]
    [TestCategory("Unit")]
    public static void CustomFormatResolvesItsBitmasksAndBounds()
    {
        var variantMappings = new Dictionary<int, VariantDefinition>
        {
            [0] = new(new Vector2I(0, 0)),
            [5] = new(new Vector2I(5, 0), new Vector2I(1, 3), new Vector2I(0, -2)),
            [10] = new(new Vector2I(10, 0))
        };
        var format = new AutoTileFormatDefinition(
            "shape_facts_test",
            BitmaskType.Edge4,
            new HashSet<int> { 0, 5, 10 },
            variantMappings);
        AutoTileFormatRegistry.Register(format);
        try
        {
            var shape = TileShapeFacts.Resolve("shape_facts_test");
            var allowed = shape.AllowedBitmasks;

            AssertBool(allowed != null && allowed.Contains(5)).IsTrue();
            AssertBool(allowed != null && !allowed.Contains(3)).IsTrue();
            AssertBool(shape.MultiCellBounds == (new Vector2I(1, 3), new Vector2I(0, -2))).IsTrue();
        }
        finally
        {
            AutoTileFormatRegistry.Unregister("shape_facts_test");
        }
    }
}
