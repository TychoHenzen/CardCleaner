using CardCleaner.Features.Worldgen.AutoTiling;
using Godot;

namespace CardCleaner.Tests.Features.Worldgen.AutoTiling.VariantDefinitionScenarios;

/// <summary>
///     VariantDefinitionGeometryTest scenarios split out of VariantDefinitionTest.
/// </summary>
[TestSuite]
[RequireGodotRuntime]
public class VariantDefinitionGeometryTest
{
    // ==================== Constructor & Default Values ====================

    /// <summary>
    /// Test that default Size is (1, 1) when not specified.
    /// </summary>
    [TestCase]
    public void TestDefaultSize_IsOneByOne()
    {
        var variant = new VariantDefinition(new Vector2I(5, 3));

        AssertThat(variant.Size.X).IsEqual(1);
        AssertThat(variant.Size.Y).IsEqual(1);
    }

    /// <summary>
    /// Test that explicitly passing default(Vector2I) for Size results in (1, 1).
    /// </summary>
    [TestCase]
    public void TestDefaultVectorSize_BecomesOneByOne()
    {
        var variant = new VariantDefinition(new Vector2I(0, 0), default);

        AssertThat(variant.Size).IsEqual(Vector2I.One);
    }

    /// <summary>
    /// Test that explicit Size is preserved.
    /// </summary>
    [TestCase]
    public void TestExplicitSize_IsPreserved()
    {
        var variant = new VariantDefinition(new Vector2I(0, 0), new Vector2I(2, 3));

        AssertThat(variant.Size.X).IsEqual(2);
        AssertThat(variant.Size.Y).IsEqual(3);
    }

    /// <summary>
    /// Test that default Offset is (0, 0).
    /// </summary>
    [TestCase]
    public void TestDefaultOffset_IsZero()
    {
        var variant = new VariantDefinition(new Vector2I(5, 3));

        AssertThat(variant.Offset).IsEqual(Vector2I.Zero);
    }

    /// <summary>
    /// Test that explicit Offset is preserved.
    /// </summary>
    [TestCase]
    public void TestExplicitOffset_IsPreserved()
    {
        var variant = new VariantDefinition(
            new Vector2I(0, 0),
            new Vector2I(1, 1),
            new Vector2I(0, -2));

        AssertThat(variant.Offset.X).IsEqual(0);
        AssertThat(variant.Offset.Y).IsEqual(-2);
    }

    /// <summary>
    /// Test that AtlasCoords is correctly stored.
    /// </summary>
    [TestCase]
    public void TestAtlasCoords_IsStored()
    {
        var variant = new VariantDefinition(new Vector2I(7, 12));

        AssertThat(variant.AtlasCoords.X).IsEqual(7);
        AssertThat(variant.AtlasCoords.Y).IsEqual(12);
    }

    // ==================== IsMultiCell Property ====================

    /// <summary>
    /// Test IsMultiCell returns false for 1x1 size.
    /// </summary>
    [TestCase]
    public void TestIsMultiCell_FalseForOneByOne()
    {
        var variant = new VariantDefinition(new Vector2I(0, 0), new Vector2I(1, 1));

        AssertBool(variant.IsMultiCell).IsFalse();
    }

    /// <summary>
    /// Test IsMultiCell returns true when width > 1.
    /// </summary>
    [TestCase]
    public void TestIsMultiCell_TrueWhenWidthGreaterThanOne()
    {
        var variant = new VariantDefinition(new Vector2I(0, 0), new Vector2I(2, 1));

        AssertBool(variant.IsMultiCell).IsTrue();
    }

    /// <summary>
    /// Test IsMultiCell returns true when height > 1.
    /// </summary>
    [TestCase]
    public void TestIsMultiCell_TrueWhenHeightGreaterThanOne()
    {
        var variant = new VariantDefinition(new Vector2I(0, 0), new Vector2I(1, 3));

        AssertBool(variant.IsMultiCell).IsTrue();
    }

    /// <summary>
    /// Test IsMultiCell returns true when both dimensions > 1.
    /// </summary>
    [TestCase]
    public void TestIsMultiCell_TrueWhenBothGreaterThanOne()
    {
        var variant = new VariantDefinition(new Vector2I(0, 0), new Vector2I(2, 3));

        AssertBool(variant.IsMultiCell).IsTrue();
    }

    /// <summary>
    /// Test IsMultiCell returns false for default constructor (Size defaults to 1x1).
    /// </summary>
    [TestCase]
    public void TestIsMultiCell_FalseForDefaultSize()
    {
        var variant = new VariantDefinition(new Vector2I(5, 5));

        AssertBool(variant.IsMultiCell).IsFalse();
    }

    // ==================== HasOffset Property ====================

    /// <summary>
    /// Test HasOffset returns false when Offset is zero.
    /// </summary>
    [TestCase]
    public void TestHasOffset_FalseWhenZero()
    {
        var variant = new VariantDefinition(new Vector2I(0, 0), new Vector2I(1, 1), Vector2I.Zero);

        AssertBool(variant.HasOffset).IsFalse();
    }

    /// <summary>
    /// Test HasOffset returns true when X offset is non-zero.
    /// </summary>
    [TestCase]
    public void TestHasOffset_TrueWhenXNonZero()
    {
        var variant = new VariantDefinition(new Vector2I(0, 0), new Vector2I(1, 1), new Vector2I(1, 0));

        AssertBool(variant.HasOffset).IsTrue();
    }

    /// <summary>
    /// Test HasOffset returns true when Y offset is non-zero (negative).
    /// </summary>
    [TestCase]
    public void TestHasOffset_TrueWhenYNegative()
    {
        var variant = new VariantDefinition(new Vector2I(0, 0), new Vector2I(1, 3), new Vector2I(0, -2));

        AssertBool(variant.HasOffset).IsTrue();
    }

    /// <summary>
    /// Test HasOffset returns false for default constructor.
    /// </summary>
    [TestCase]
    public void TestHasOffset_FalseForDefault()
    {
        var variant = new VariantDefinition(new Vector2I(0, 0));

        AssertBool(variant.HasOffset).IsFalse();
    }
}
