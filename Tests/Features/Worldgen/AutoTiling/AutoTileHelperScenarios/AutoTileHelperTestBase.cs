using CardCleaner.Scripts.Core.Enumeration;
using CardCleaner.Scripts.Features.Deckbuilder.Tiles;
using Godot;

namespace CardCleaner.Tests.Features.Worldgen.AutoTiling.AutoTileHelperScenarios;

/// <summary>
///     Shared fixture for the AutoTileHelperTest scenario suites.
/// </summary>
public abstract class AutoTileHelperTestBase
{
    // ==================== Helper Methods ====================

    protected static TileDefinition CreateTileDefinition(string id, string formatName, bool hasVariants = true)
    {
        var variants = hasVariants ? CreateVariantArray(formatName == "blob47" ? 47 : 16) : null;

        return new TileDefinition(
            id,
            id,
            TilePassability.Passable,
            new Vector2I(0, 0),
            new TileDefinitionOptions
            {
                AutoTileVariants = variants,
                AutoTileFormatName = formatName
            });
    }

    protected static Vector2I?[] CreateVariantArray(int count)
    {
        var variants = new Vector2I?[count];
        for (var i = 0; i < count; i++)
        {
            variants[i] = new Vector2I(i, 0);
        }
        return variants;
    }
}
