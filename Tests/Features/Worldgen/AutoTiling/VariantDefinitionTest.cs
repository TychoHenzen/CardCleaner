using CardCleaner.Features.Worldgen.AutoTiling;
using GdUnit4;
using Godot;
using static GdUnit4.Assertions;

namespace CardCleaner.Tests.Features.Worldgen.AutoTiling;

/// <summary>
/// Tests for VariantDefinition record struct.
/// Validates constructor behavior, default values, and property calculations.
/// </summary>
[TestSuite]
[RequireGodotRuntime]
public class VariantDefinitionTest
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

    // ==================== AtlasRegionSize Property ====================

    /// <summary>
    /// Test AtlasRegionSize is null by default.
    /// </summary>
    [TestCase]
    public void TestAtlasRegionSize_NullByDefault()
    {
        var variant = new VariantDefinition(new Vector2I(0, 0));

        AssertThat(variant.AtlasRegionSize).IsNull();
    }

    /// <summary>
    /// Test AtlasRegionSize can be explicitly set.
    /// </summary>
    [TestCase]
    public void TestAtlasRegionSize_CanBeExplicitlySet()
    {
        var variant = new VariantDefinition(
            new Vector2I(0, 0),
            new Vector2I(1, 1),
            Vector2I.Zero,
            new Vector2I(32, 48));

        AssertThat(variant.AtlasRegionSize).IsNotNull();
        AssertThat(variant.AtlasRegionSize!.Value.X).IsEqual(32);
        AssertThat(variant.AtlasRegionSize!.Value.Y).IsEqual(48);
    }

    /// <summary>
    /// Test AtlasRegionSize can be explicitly set to null.
    /// </summary>
    [TestCase]
    public void TestAtlasRegionSize_CanBeExplicitlyNull()
    {
        var variant = new VariantDefinition(
            new Vector2I(0, 0),
            new Vector2I(1, 1),
            Vector2I.Zero,
            null);

        AssertThat(variant.AtlasRegionSize).IsNull();
    }

    // ==================== Record Equality ====================

    /// <summary>
    /// Test record equality for identical values.
    /// </summary>
    [TestCase]
    public void TestRecordEquality_IdenticalValuesAreEqual()
    {
        var v1 = new VariantDefinition(new Vector2I(5, 3), new Vector2I(2, 2), new Vector2I(1, -1));
        var v2 = new VariantDefinition(new Vector2I(5, 3), new Vector2I(2, 2), new Vector2I(1, -1));

        AssertBool(v1 == v2).IsTrue();
        AssertBool(v1.Equals(v2)).IsTrue();
    }

    /// <summary>
    /// Test record inequality when AtlasCoords differ.
    /// </summary>
    [TestCase]
    public void TestRecordEquality_DifferentAtlasCoords()
    {
        var v1 = new VariantDefinition(new Vector2I(5, 3));
        var v2 = new VariantDefinition(new Vector2I(5, 4));

        AssertBool(v1 != v2).IsTrue();
        AssertBool(v1.Equals(v2)).IsFalse();
    }

    /// <summary>
    /// Test record inequality when Size differs.
    /// </summary>
    [TestCase]
    public void TestRecordEquality_DifferentSize()
    {
        var v1 = new VariantDefinition(new Vector2I(0, 0), new Vector2I(1, 1));
        var v2 = new VariantDefinition(new Vector2I(0, 0), new Vector2I(2, 1));

        AssertBool(v1 != v2).IsTrue();
    }

    /// <summary>
    /// Test record inequality when Offset differs.
    /// </summary>
    [TestCase]
    public void TestRecordEquality_DifferentOffset()
    {
        var v1 = new VariantDefinition(new Vector2I(0, 0), new Vector2I(1, 3), new Vector2I(0, 0));
        var v2 = new VariantDefinition(new Vector2I(0, 0), new Vector2I(1, 3), new Vector2I(0, -2));

        AssertBool(v1 != v2).IsTrue();
    }

    /// <summary>
    /// Test record inequality when AtlasRegionSize differs.
    /// </summary>
    [TestCase]
    public void TestRecordEquality_DifferentAtlasRegionSize()
    {
        var v1 = new VariantDefinition(new Vector2I(0, 0), new Vector2I(1, 1), Vector2I.Zero, null);
        var v2 = new VariantDefinition(new Vector2I(0, 0), new Vector2I(1, 1), Vector2I.Zero, new Vector2I(32, 32));

        AssertBool(v1 != v2).IsTrue();
    }

    /// <summary>
    /// Test default values compare equal.
    /// </summary>
    [TestCase]
    public void TestRecordEquality_DefaultValuesEqual()
    {
        var v1 = new VariantDefinition(new Vector2I(0, 0));
        var v2 = new VariantDefinition(new Vector2I(0, 0), new Vector2I(1, 1), Vector2I.Zero, null);

        // Both should have Size=(1,1), Offset=(0,0), AtlasRegionSize=null
        AssertBool(v1 == v2).IsTrue();
    }

    // ==================== Value Semantics ====================

    /// <summary>
    /// Test that record struct has value semantics (copy on assignment).
    /// </summary>
    [TestCase]
    public void TestValueSemantics_CopyOnAssignment()
    {
        var original = new VariantDefinition(new Vector2I(5, 5), new Vector2I(2, 2));
        var copy = original;

        // Modify copy using with expression
        copy = copy with { AtlasCoords = new Vector2I(10, 10) };

        // Original should be unchanged
        AssertThat(original.AtlasCoords.X).IsEqual(5);
        AssertThat(copy.AtlasCoords.X).IsEqual(10);
    }

    /// <summary>
    /// Test with expression creates modified copy.
    /// </summary>
    [TestCase]
    public void TestWithExpression_CreatesModifiedCopy()
    {
        var original = new VariantDefinition(new Vector2I(0, 0), new Vector2I(1, 1));
        var modified = original with { Size = new Vector2I(3, 3) };

        AssertThat(original.Size).IsEqual(Vector2I.One);
        AssertThat(modified.Size).IsEqual(new Vector2I(3, 3));
        AssertThat(modified.AtlasCoords).IsEqual(original.AtlasCoords);
    }
}
