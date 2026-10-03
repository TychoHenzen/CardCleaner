using CardCleaner.Features.Worldgen.AutoTiling;

namespace CardCleaner.Tests.Features.Worldgen.AutoTiling.BuiltInFormats;

/// <summary>
///     BuiltInBlob47IdentityTest scenarios split out of BuiltInAutoTileFormatsTest.
/// </summary>
[TestSuite]
[RequireGodotRuntime]
public class BuiltInBlob47IdentityTest
{
    // ==================== CreateBlob47 ====================

    /// <summary>
    /// Test CreateBlob47 returns format with correct name.
    /// </summary>
    [TestCase]
    public void TestCreateBlob47_HasCorrectName()
    {
        var format = BuiltInAutoTileFormats.CreateBlob47();

        AssertString(format.Name).IsEqual("blob47");
    }

    /// <summary>
    /// Test CreateBlob47 uses Full8 bitmask type.
    /// </summary>
    [TestCase]
    public void TestCreateBlob47_UsesFull8BitmaskType()
    {
        var format = BuiltInAutoTileFormats.CreateBlob47();

        AssertThat(format.BitmaskType).IsEqual(BitmaskType.Full8);
    }

    /// <summary>
    /// Test CreateBlob47 has exactly 47 allowed bitmasks.
    /// </summary>
    [TestCase]
    public void TestCreateBlob47_HasExactly47AllowedBitmasks()
    {
        var format = BuiltInAutoTileFormats.CreateBlob47();

        AssertThat(format.AllowedBitmasks.Count).IsEqual(47);
    }

    /// <summary>
    /// Test CreateBlob47 is marked as built-in.
    /// </summary>
    [TestCase]
    public void TestCreateBlob47_IsBuiltIn()
    {
        var format = BuiltInAutoTileFormats.CreateBlob47();

        AssertBool(format.IsBuiltIn).IsTrue();
    }

    /// <summary>
    /// Test CreateBlob47 has 47 variant mappings.
    /// </summary>
    [TestCase]
    public void TestCreateBlob47_Has47VariantMappings()
    {
        var format = BuiltInAutoTileFormats.CreateBlob47();

        AssertThat(format.VariantMappings.Count).IsEqual(47);
    }

    /// <summary>
    /// Test blob47 includes bitmask 0 (isolated tile).
    /// </summary>
    [TestCase]
    public void TestCreateBlob47_IncludesIsolatedTile()
    {
        var format = BuiltInAutoTileFormats.CreateBlob47();

        AssertBool(format.IsBitmaskAllowed(0)).IsTrue();
    }

    /// <summary>
    /// Test blob47 includes bitmask 255 (fully surrounded).
    /// </summary>
    [TestCase]
    public void TestCreateBlob47_IncludesFullySurrounded()
    {
        var format = BuiltInAutoTileFormats.CreateBlob47();

        // 255 = all 8 neighbors present (valid because all corners have their edges)
        AssertBool(format.IsBitmaskAllowed(255)).IsTrue();
    }
}
