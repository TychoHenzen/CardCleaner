using CardCleaner.Features.Worldgen.AutoTiling;

namespace CardCleaner.Tests.Features.Worldgen.AutoTiling.BuiltInFormats;

/// <summary>
///     BuiltInCornerAndEdge16FormatTest scenarios split out of BuiltInAutoTileFormatsTest.
/// </summary>
[TestSuite]
[RequireGodotRuntime]
public class BuiltInCornerAndEdge16FormatTest
{
    // ==================== CreateCorner16 ====================

    /// <summary>
    /// Test CreateCorner16 returns format with correct name.
    /// </summary>
    [TestCase]
    public void TestCreateCorner16_HasCorrectName()
    {
        var format = BuiltInAutoTileFormats.CreateCorner16();

        AssertString(format.Name).IsEqual("corner16");
    }

    /// <summary>
    /// Test CreateCorner16 uses Corner4 bitmask type.
    /// </summary>
    [TestCase]
    public void TestCreateCorner16_UsesCorner4BitmaskType()
    {
        var format = BuiltInAutoTileFormats.CreateCorner16();

        AssertThat(format.BitmaskType).IsEqual(BitmaskType.Corner4);
    }

    /// <summary>
    /// Test CreateCorner16 has exactly 16 allowed bitmasks.
    /// </summary>
    [TestCase]
    public void TestCreateCorner16_Has16AllowedBitmasks()
    {
        var format = BuiltInAutoTileFormats.CreateCorner16();

        AssertThat(format.AllowedBitmasks.Count).IsEqual(16);
    }

    /// <summary>
    /// Test CreateCorner16 allows bitmasks 0-15.
    /// </summary>
    [TestCase]
    public void TestCreateCorner16_AllowsBitmasks0To15()
    {
        var format = BuiltInAutoTileFormats.CreateCorner16();

        for (var i = 0; i < 16; i++)
        {
            AssertBool(format.IsBitmaskAllowed(i)).IsTrue();
        }

        AssertBool(format.IsBitmaskAllowed(16)).IsFalse();
    }

    /// <summary>
    /// Test CreateCorner16 is marked as built-in.
    /// </summary>
    [TestCase]
    public void TestCreateCorner16_IsBuiltIn()
    {
        var format = BuiltInAutoTileFormats.CreateCorner16();

        AssertBool(format.IsBuiltIn).IsTrue();
    }

    /// <summary>
    /// Test CreateCorner16 has 16 variant mappings.
    /// </summary>
    [TestCase]
    public void TestCreateCorner16_Has16VariantMappings()
    {
        var format = BuiltInAutoTileFormats.CreateCorner16();

        AssertThat(format.VariantMappings.Count).IsEqual(16);
    }

    // ==================== CreateEdge16 ====================

    /// <summary>
    /// Test CreateEdge16 returns format with correct name.
    /// </summary>
    [TestCase]
    public void TestCreateEdge16_HasCorrectName()
    {
        var format = BuiltInAutoTileFormats.CreateEdge16();

        AssertString(format.Name).IsEqual("edge16");
    }

    /// <summary>
    /// Test CreateEdge16 uses Edge4 bitmask type.
    /// </summary>
    [TestCase]
    public void TestCreateEdge16_UsesEdge4BitmaskType()
    {
        var format = BuiltInAutoTileFormats.CreateEdge16();

        AssertThat(format.BitmaskType).IsEqual(BitmaskType.Edge4);
    }

    /// <summary>
    /// Test CreateEdge16 has exactly 16 allowed bitmasks.
    /// </summary>
    [TestCase]
    public void TestCreateEdge16_Has16AllowedBitmasks()
    {
        var format = BuiltInAutoTileFormats.CreateEdge16();

        AssertThat(format.AllowedBitmasks.Count).IsEqual(16);
    }

    /// <summary>
    /// Test CreateEdge16 allows bitmasks 0-15.
    /// </summary>
    [TestCase]
    public void TestCreateEdge16_AllowsBitmasks0To15()
    {
        var format = BuiltInAutoTileFormats.CreateEdge16();

        for (var i = 0; i < 16; i++)
        {
            AssertBool(format.IsBitmaskAllowed(i)).IsTrue();
        }

        AssertBool(format.IsBitmaskAllowed(16)).IsFalse();
    }

    /// <summary>
    /// Test CreateEdge16 is marked as built-in.
    /// </summary>
    [TestCase]
    public void TestCreateEdge16_IsBuiltIn()
    {
        var format = BuiltInAutoTileFormats.CreateEdge16();

        AssertBool(format.IsBuiltIn).IsTrue();
    }

    /// <summary>
    /// Test CreateEdge16 has 16 variant mappings.
    /// </summary>
    [TestCase]
    public void TestCreateEdge16_Has16VariantMappings()
    {
        var format = BuiltInAutoTileFormats.CreateEdge16();

        AssertThat(format.VariantMappings.Count).IsEqual(16);
    }
}
