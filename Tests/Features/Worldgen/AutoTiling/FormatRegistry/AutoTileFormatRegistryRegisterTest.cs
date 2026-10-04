using System;
using CardCleaner.Features.Worldgen.AutoTiling;

namespace CardCleaner.Tests.Features.Worldgen.AutoTiling.FormatRegistry;

/// <summary>
///     AutoTileFormatRegistryRegisterTest scenarios split out of AutoTileFormatRegistryTest.
/// </summary>
[TestSuite]
[RequireGodotRuntime]
public class AutoTileFormatRegistryRegisterTest : AutoTileFormatRegistryTestBase
{
    // ==================== Register ====================

    /// <summary>
    /// Test Register succeeds for new custom format.
    /// </summary>
    [TestCase]
    public void TestRegister_SucceedsForNewFormat()
    {
        var customFormat = CreateCustomFormat("custom_test");

        var result = AutoTileFormatRegistry.Register(customFormat);

        AssertBool(result).IsTrue();
        AssertBool(AutoTileFormatRegistry.Contains("custom_test")).IsTrue();
    }

    /// <summary>
    /// Test Register returns false for duplicate name.
    /// </summary>
    [TestCase]
    public void TestRegister_ReturnsFalseForDuplicate()
    {
        var format1 = CreateCustomFormat("duplicate_test");
        var format2 = CreateCustomFormat("duplicate_test");

        AutoTileFormatRegistry.Register(format1);
        var result = AutoTileFormatRegistry.Register(format2);

        AssertBool(result).IsFalse();
    }

    /// <summary>
    /// Test Register returns false when trying to replace built-in.
    /// </summary>
    [TestCase]
    public void TestRegister_ReturnsFalseForBuiltInName()
    {
        var customFormat = CreateCustomFormat("corner16");

        var result = AutoTileFormatRegistry.Register(customFormat);

        AssertBool(result).IsFalse();
    }

    /// <summary>
    /// Test Register throws for null format.
    /// </summary>
    [TestCase]
    public void TestRegister_ThrowsForNull()
    {
        AssertThrown(() => AutoTileFormatRegistry.Register(null!))
            .IsInstanceOf<ArgumentNullException>();
    }

    /// <summary>
    /// Test registered custom format can be retrieved.
    /// </summary>
    [TestCase]
    public void TestRegister_FormatCanBeRetrieved()
    {
        var customFormat = CreateCustomFormat("retrieve_test");
        AutoTileFormatRegistry.Register(customFormat);

        var found = AutoTileFormatRegistry.TryGet("retrieve_test", out var retrieved);

        AssertBool(found).IsTrue();
        AssertString(retrieved!.Name).IsEqual("retrieve_test");
    }
}
