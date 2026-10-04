using CardCleaner.Features.Worldgen.AutoTiling;
using Godot;

namespace CardCleaner.Tests.Features.Worldgen.AutoTiling.VariantDefinitionScenarios;

/// <summary>
///     VariantDefinitionValueSemanticsTest scenarios split out of VariantDefinitionTest.
/// </summary>
[TestSuite]
[RequireGodotRuntime]
public class VariantDefinitionValueSemanticsTest
{
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
