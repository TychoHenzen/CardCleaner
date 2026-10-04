using System.Collections.Generic;
using CardCleaner.Features.Worldgen.AutoTiling;
using Godot;

namespace CardCleaner.Tests.Features.Worldgen.AutoTiling.FormatRegistry;

/// <summary>
///     Shared fixture for the AutoTileFormatRegistryTest scenario suites.
/// </summary>
public abstract class AutoTileFormatRegistryTestBase
{
    /// <summary>
    /// Clean up custom formats after each test to ensure test isolation.
    /// </summary>
    [AfterTest]
    public void Cleanup()
    {
        AutoTileFormatRegistry.ClearCustomFormats();
    }

    // ==================== Helper Methods ====================

    protected static AutoTileFormatDefinition CreateCustomFormat(string name)
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
