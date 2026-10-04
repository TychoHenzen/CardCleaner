using System.Collections.Generic;
using CardCleaner.Features.Worldgen.AutoTiling;
using Godot;

namespace CardCleaner.Tests.Features.Worldgen.AutoTiling.FormatDefinition;

/// <summary>
///     FormatDefinitionConstructorAndLookupTest scenarios split out of AutoTileFormatDefinitionTest.
/// </summary>
[TestSuite]
[RequireGodotRuntime]
public class FormatDefinitionConstructorAndLookupTest : AutoTileFormatDefinitionTestBase
{
    // ==================== Construction ====================

    /// <summary>
    /// Test that constructor sets all properties correctly.
    /// </summary>
    [TestCase]
    public void TestConstructor_SetsAllProperties()
    {
        var allowedBitmasks = new HashSet<int> { 0, 1, 2, 3 };
        var variantMappings = new Dictionary<int, VariantDefinition>
        {
            [0] = new(new Vector2I(0, 0)),
            [1] = new(new Vector2I(1, 0)),
            [2] = new(new Vector2I(2, 0)),
            [3] = new(new Vector2I(3, 0))
        };

        var format = new AutoTileFormatDefinition(
            "test_format",
            BitmaskType.Edge4,
            allowedBitmasks,
            variantMappings,
            isBuiltIn: false);

        AssertString(format.Name).IsEqual("test_format");
        AssertThat(format.BitmaskType).IsEqual(BitmaskType.Edge4);
        AssertBool(format.IsBuiltIn).IsFalse();
        AssertThat(format.AllowedBitmasks.Count).IsEqual(4);
        AssertThat(format.VariantMappings.Count).IsEqual(4);
    }

    /// <summary>
    /// Test IsBuiltIn defaults to false.
    /// </summary>
    [TestCase]
    public void TestConstructor_IsBuiltInDefaultsFalse()
    {
        var format = new AutoTileFormatDefinition(
            "custom",
            BitmaskType.Corner4,
            new HashSet<int> { 0 },
            new Dictionary<int, VariantDefinition> { [0] = new(Vector2I.Zero) });

        AssertBool(format.IsBuiltIn).IsFalse();
    }

    // ==================== GetVariant ====================

    /// <summary>
    /// Test GetVariant returns correct variant for allowed bitmask.
    /// </summary>
    [TestCase]
    public void TestGetVariant_ReturnsVariantForAllowedBitmask()
    {
        var format = CreateTestFormat(new[] { 0, 5, 15 });

        var variant = format.GetVariant(5);

        AssertThat(variant).IsNotNull();
        AssertThat(variant!.Value.AtlasCoords).IsEqual(new Vector2I(5, 0));
    }

    /// <summary>
    /// Test GetVariant returns null for disallowed bitmask.
    /// </summary>
    [TestCase]
    public void TestGetVariant_ReturnsNullForDisallowedBitmask()
    {
        var format = CreateTestFormat(new[] { 0, 5, 15 }); // 10 is not allowed

        var variant = format.GetVariant(10);

        AssertThat(variant).IsNull();
    }

    /// <summary>
    /// Test GetVariant returns null when bitmask allowed but not mapped.
    /// </summary>
    [TestCase]
    public void TestGetVariant_ReturnsNullWhenAllowedButNotMapped()
    {
        var allowedBitmasks = new HashSet<int> { 0, 1, 2 };
        var variantMappings = new Dictionary<int, VariantDefinition>
        {
            [0] = new(new Vector2I(0, 0)),
            // 1 is allowed but not mapped
            [2] = new(new Vector2I(2, 0))
        };

        var format = new AutoTileFormatDefinition(
            "test",
            BitmaskType.Edge4,
            allowedBitmasks,
            variantMappings);

        var variant = format.GetVariant(1);

        AssertThat(variant).IsNull();
    }

    /// <summary>
    /// Test GetVariant at boundary values (0 and max).
    /// </summary>
    [TestCase]
    public void TestGetVariant_BoundaryValues()
    {
        var format = CreateTestFormat(new[] { 0, 15 });

        AssertThat(format.GetVariant(0)).IsNotNull();
        AssertThat(format.GetVariant(15)).IsNotNull();
    }
}
