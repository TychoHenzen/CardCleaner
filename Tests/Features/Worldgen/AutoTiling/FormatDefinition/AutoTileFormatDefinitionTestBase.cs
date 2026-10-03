using System.Collections.Generic;
using System.Linq;
using CardCleaner.Features.Worldgen.AutoTiling;
using Godot;

namespace CardCleaner.Tests.Features.Worldgen.AutoTiling.FormatDefinition;

/// <summary>
///     Shared fixture for the AutoTileFormatDefinitionTest scenario suites.
/// </summary>
public abstract class AutoTileFormatDefinitionTestBase
{
    // ==================== Helper Methods ====================

    protected static AutoTileFormatDefinition CreateTestFormat(int[] allowedBitmasks)
    {
        var bitmasks = allowedBitmasks.ToHashSet();
        return new AutoTileFormatDefinition(
            "test_format",
            BitmaskType.Edge4,
            bitmasks,
            CreateVariantMappings(bitmasks),
            isBuiltIn: false);
    }

    protected static Dictionary<int, VariantDefinition> CreateVariantMappings(IEnumerable<int> bitmasks)
    {
        return bitmasks.ToDictionary(
            b => b,
            b => new VariantDefinition(new Vector2I(b, 0)));
    }
}
