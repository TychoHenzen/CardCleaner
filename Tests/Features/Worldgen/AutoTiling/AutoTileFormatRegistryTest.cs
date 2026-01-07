using System;
using System.Collections.Generic;
using System.Linq;
using CardCleaner.Features.Worldgen.AutoTiling;
using GdUnit4;
using Godot;
using static GdUnit4.Assertions;

namespace CardCleaner.Tests.Features.Worldgen.AutoTiling;

/// <summary>
/// Tests for AutoTileFormatRegistry static class.
/// Validates thread-safe registration, lookup, and management of auto-tile formats.
/// </summary>
[TestSuite]
[RequireGodotRuntime]
public class AutoTileFormatRegistryTest
{
    /// <summary>
    /// Clean up custom formats after each test to ensure test isolation.
    /// </summary>
    [AfterTest]
    public void Cleanup()
    {
        AutoTileFormatRegistry.ClearCustomFormats();
    }

    // ==================== Built-In Registration ====================

    /// <summary>
    /// Test that corner16 built-in format is auto-registered.
    /// </summary>
    [TestCase]
    public void TestBuiltIn_Corner16_AutoRegistered()
    {
        var found = AutoTileFormatRegistry.TryGet("corner16", out var format);

        AssertBool(found).IsTrue();
        AssertThat(format).IsNotNull();
        AssertString(format!.Name).IsEqual("corner16");
        AssertBool(format.IsBuiltIn).IsTrue();
    }

    /// <summary>
    /// Test that edge16 built-in format is auto-registered.
    /// </summary>
    [TestCase]
    public void TestBuiltIn_Edge16_AutoRegistered()
    {
        var found = AutoTileFormatRegistry.TryGet("edge16", out var format);

        AssertBool(found).IsTrue();
        AssertThat(format).IsNotNull();
        AssertString(format!.Name).IsEqual("edge16");
        AssertBool(format.IsBuiltIn).IsTrue();
    }

    /// <summary>
    /// Test that blob47 built-in format is auto-registered.
    /// </summary>
    [TestCase]
    public void TestBuiltIn_Blob47_AutoRegistered()
    {
        var found = AutoTileFormatRegistry.TryGet("blob47", out var format);

        AssertBool(found).IsTrue();
        AssertThat(format).IsNotNull();
        AssertString(format!.Name).IsEqual("blob47");
        AssertBool(format.IsBuiltIn).IsTrue();
    }

    /// <summary>
    /// Test GetAll returns at least 3 built-in formats.
    /// </summary>
    [TestCase]
    public void TestGetAll_ReturnsAtLeast3BuiltIns()
    {
        var formats = AutoTileFormatRegistry.GetAll();

        AssertThat(formats.Count).IsGreaterEqual(3);

        var names = formats.Select(f => f.Name.ToLowerInvariant()).ToHashSet();
        AssertBool(names.Contains("corner16")).IsTrue();
        AssertBool(names.Contains("edge16")).IsTrue();
        AssertBool(names.Contains("blob47")).IsTrue();
    }

    // ==================== Case-Insensitive Lookup ====================

    /// <summary>
    /// Test case-insensitive lookup: lowercase.
    /// </summary>
    [TestCase]
    public void TestCaseInsensitive_Lowercase()
    {
        var found = AutoTileFormatRegistry.TryGet("corner16", out var format);

        AssertBool(found).IsTrue();
        AssertThat(format).IsNotNull();
    }

    /// <summary>
    /// Test case-insensitive lookup: uppercase.
    /// </summary>
    [TestCase]
    public void TestCaseInsensitive_Uppercase()
    {
        var found = AutoTileFormatRegistry.TryGet("CORNER16", out var format);

        AssertBool(found).IsTrue();
        AssertThat(format).IsNotNull();
    }

    /// <summary>
    /// Test case-insensitive lookup: mixed case.
    /// </summary>
    [TestCase]
    public void TestCaseInsensitive_MixedCase()
    {
        var found = AutoTileFormatRegistry.TryGet("Corner16", out var format);

        AssertBool(found).IsTrue();
        AssertThat(format).IsNotNull();
    }

    /// <summary>
    /// Test case-insensitive lookup: BLOB47.
    /// </summary>
    [TestCase]
    public void TestCaseInsensitive_Blob47()
    {
        var found = AutoTileFormatRegistry.TryGet("BLOB47", out var format);

        AssertBool(found).IsTrue();
        AssertString(format!.Name).IsEqual("blob47");
    }

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

    // ==================== TryGet ====================

    /// <summary>
    /// Test TryGet returns false and null for missing format.
    /// </summary>
    [TestCase]
    public void TestTryGet_ReturnsFalseForMissing()
    {
        var found = AutoTileFormatRegistry.TryGet("nonexistent_format", out var format);

        AssertBool(found).IsFalse();
        AssertThat(format).IsNull();
    }

    /// <summary>
    /// Test TryGet returns true and format for existing.
    /// </summary>
    [TestCase]
    public void TestTryGet_ReturnsTrueForExisting()
    {
        var found = AutoTileFormatRegistry.TryGet("edge16", out var format);

        AssertBool(found).IsTrue();
        AssertThat(format).IsNotNull();
        AssertString(format!.Name).IsEqual("edge16");
    }

    /// <summary>
    /// Test TryGet works for custom format.
    /// </summary>
    [TestCase]
    public void TestTryGet_WorksForCustomFormat()
    {
        AutoTileFormatRegistry.Register(CreateCustomFormat("tryget_custom"));

        var found = AutoTileFormatRegistry.TryGet("tryget_custom", out var format);

        AssertBool(found).IsTrue();
        AssertString(format!.Name).IsEqual("tryget_custom");
    }

    // ==================== Get ====================

    /// <summary>
    /// Test Get returns format for existing name.
    /// </summary>
    [TestCase]
    public void TestGet_ReturnsFormatForExisting()
    {
        var format = AutoTileFormatRegistry.Get("corner16");

        AssertString(format.Name).IsEqual("corner16");
    }

    /// <summary>
    /// Test Get throws KeyNotFoundException for missing name.
    /// </summary>
    [TestCase]
    public void TestGet_ThrowsForMissing()
    {
        AssertThrown(() => AutoTileFormatRegistry.Get("definitely_not_a_format"))
            .IsInstanceOf<KeyNotFoundException>();
    }

    /// <summary>
    /// Test Get throws with descriptive message.
    /// </summary>
    [TestCase]
    public void TestGet_ThrowsWithDescriptiveMessage()
    {
        try
        {
            AutoTileFormatRegistry.Get("missing_format_xyz");
            AssertBool(false).IsTrue(); // Should not reach here
        }
        catch (KeyNotFoundException ex)
        {
            AssertString(ex.Message).Contains("missing_format_xyz");
        }
    }

    // ==================== Contains ====================

    /// <summary>
    /// Test Contains returns true for built-in format.
    /// </summary>
    [TestCase]
    public void TestContains_TrueForBuiltIn()
    {
        AssertBool(AutoTileFormatRegistry.Contains("blob47")).IsTrue();
    }

    /// <summary>
    /// Test Contains returns false for non-existent format.
    /// </summary>
    [TestCase]
    public void TestContains_FalseForNonExistent()
    {
        AssertBool(AutoTileFormatRegistry.Contains("not_a_format")).IsFalse();
    }

    /// <summary>
    /// Test Contains returns true for registered custom format.
    /// </summary>
    [TestCase]
    public void TestContains_TrueForCustom()
    {
        AutoTileFormatRegistry.Register(CreateCustomFormat("contains_custom"));

        AssertBool(AutoTileFormatRegistry.Contains("contains_custom")).IsTrue();
    }

    /// <summary>
    /// Test Contains is case-insensitive.
    /// </summary>
    [TestCase]
    public void TestContains_CaseInsensitive()
    {
        AssertBool(AutoTileFormatRegistry.Contains("EDGE16")).IsTrue();
        AssertBool(AutoTileFormatRegistry.Contains("Edge16")).IsTrue();
        AssertBool(AutoTileFormatRegistry.Contains("edge16")).IsTrue();
    }

    // ==================== Unregister ====================

    /// <summary>
    /// Test Unregister returns false for built-in formats.
    /// </summary>
    [TestCase]
    public void TestUnregister_ReturnsFalseForBuiltIn()
    {
        var result = AutoTileFormatRegistry.Unregister("corner16");

        AssertBool(result).IsFalse();
        AssertBool(AutoTileFormatRegistry.Contains("corner16")).IsTrue();
    }

    /// <summary>
    /// Test Unregister returns true and removes custom format.
    /// </summary>
    [TestCase]
    public void TestUnregister_RemovesCustomFormat()
    {
        AutoTileFormatRegistry.Register(CreateCustomFormat("unregister_test"));
        AssertBool(AutoTileFormatRegistry.Contains("unregister_test")).IsTrue();

        var result = AutoTileFormatRegistry.Unregister("unregister_test");

        AssertBool(result).IsTrue();
        AssertBool(AutoTileFormatRegistry.Contains("unregister_test")).IsFalse();
    }

    /// <summary>
    /// Test Unregister returns false for non-existent format.
    /// </summary>
    [TestCase]
    public void TestUnregister_ReturnsFalseForNonExistent()
    {
        var result = AutoTileFormatRegistry.Unregister("never_existed");

        AssertBool(result).IsFalse();
    }

    /// <summary>
    /// Test Unregister is case-insensitive.
    /// </summary>
    [TestCase]
    public void TestUnregister_CaseInsensitive()
    {
        AutoTileFormatRegistry.Register(CreateCustomFormat("case_test"));

        var result = AutoTileFormatRegistry.Unregister("CASE_TEST");

        AssertBool(result).IsTrue();
        AssertBool(AutoTileFormatRegistry.Contains("case_test")).IsFalse();
    }

    // ==================== ClearCustomFormats ====================

    /// <summary>
    /// Test ClearCustomFormats removes custom formats.
    /// </summary>
    [TestCase]
    public void TestClearCustomFormats_RemovesCustom()
    {
        AutoTileFormatRegistry.Register(CreateCustomFormat("clear_test_1"));
        AutoTileFormatRegistry.Register(CreateCustomFormat("clear_test_2"));

        AutoTileFormatRegistry.ClearCustomFormats();

        AssertBool(AutoTileFormatRegistry.Contains("clear_test_1")).IsFalse();
        AssertBool(AutoTileFormatRegistry.Contains("clear_test_2")).IsFalse();
    }

    /// <summary>
    /// Test ClearCustomFormats preserves built-in formats.
    /// </summary>
    [TestCase]
    public void TestClearCustomFormats_PreservesBuiltIns()
    {
        AutoTileFormatRegistry.Register(CreateCustomFormat("to_be_cleared"));

        AutoTileFormatRegistry.ClearCustomFormats();

        AssertBool(AutoTileFormatRegistry.Contains("corner16")).IsTrue();
        AssertBool(AutoTileFormatRegistry.Contains("edge16")).IsTrue();
        AssertBool(AutoTileFormatRegistry.Contains("blob47")).IsTrue();
    }

    /// <summary>
    /// Test ClearCustomFormats is idempotent.
    /// </summary>
    [TestCase]
    public void TestClearCustomFormats_Idempotent()
    {
        AutoTileFormatRegistry.ClearCustomFormats();
        AutoTileFormatRegistry.ClearCustomFormats();

        // Should not throw and built-ins should remain
        AssertThat(AutoTileFormatRegistry.GetAll().Count).IsGreaterEqual(3);
    }

    /// <summary>
    /// Test GetAll count decreases after ClearCustomFormats.
    /// </summary>
    [TestCase]
    public void TestClearCustomFormats_DecreasesCount()
    {
        AutoTileFormatRegistry.Register(CreateCustomFormat("count_test_1"));
        AutoTileFormatRegistry.Register(CreateCustomFormat("count_test_2"));
        var countBefore = AutoTileFormatRegistry.GetAll().Count;

        AutoTileFormatRegistry.ClearCustomFormats();

        var countAfter = AutoTileFormatRegistry.GetAll().Count;
        AssertThat(countAfter).IsLess(countBefore);
        AssertThat(countAfter).IsEqual(3); // Only built-ins remain
    }

    // ==================== GetAll ====================

    /// <summary>
    /// Test GetAll returns read-only collection.
    /// </summary>
    [TestCase]
    public void TestGetAll_ReturnsReadOnlyCollection()
    {
        var formats = AutoTileFormatRegistry.GetAll();

        // IReadOnlyCollection - verify we can iterate and count
        AssertThat(formats.Count).IsGreaterEqual(3);
        var count = 0;
        foreach (var _ in formats) count++;
        AssertThat(count).IsEqual(formats.Count);
    }

    /// <summary>
    /// Test GetAll includes both built-in and custom formats.
    /// </summary>
    [TestCase]
    public void TestGetAll_IncludesCustomFormats()
    {
        AutoTileFormatRegistry.Register(CreateCustomFormat("getall_custom"));

        var formats = AutoTileFormatRegistry.GetAll();
        var names = formats.Select(f => f.Name).ToHashSet(StringComparer.OrdinalIgnoreCase);

        AssertBool(names.Contains("corner16")).IsTrue();
        AssertBool(names.Contains("getall_custom")).IsTrue();
    }

    // ==================== Thread Safety (Basic) ====================

    /// <summary>
    /// Test EnsureBuiltInsRegistered is idempotent.
    /// </summary>
    [TestCase]
    public void TestEnsureBuiltInsRegistered_Idempotent()
    {
        AutoTileFormatRegistry.EnsureBuiltInsRegistered();
        AutoTileFormatRegistry.EnsureBuiltInsRegistered();
        AutoTileFormatRegistry.EnsureBuiltInsRegistered();

        // Should not create duplicate entries
        var formats = AutoTileFormatRegistry.GetAll();
        var corner16Count = formats.Count(f => f.Name.Equals("corner16", StringComparison.OrdinalIgnoreCase));
        AssertThat(corner16Count).IsEqual(1);
    }

    // ==================== Helper Methods ====================

    private static AutoTileFormatDefinition CreateCustomFormat(string name)
    {
        return new AutoTileFormatDefinition(
            name,
            BitmaskType.Edge4,
            new HashSet<int> { 0, 1, 2, 3 },
            new Dictionary<int, VariantDefinition>
            {
                [0] = new(new Vector2I(0, 0)),
                [1] = new(new Vector2I(1, 0)),
                [2] = new(new Vector2I(2, 0)),
                [3] = new(new Vector2I(3, 0))
            },
            isBuiltIn: false);
    }
}
