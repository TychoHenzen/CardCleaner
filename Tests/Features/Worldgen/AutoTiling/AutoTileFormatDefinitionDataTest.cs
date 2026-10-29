using System.Collections.Generic;
using CardCleaner.Features.Worldgen.AutoTiling;
using CardCleaner.Scripts.Core.Enumeration;
using CardCleaner.Scripts.Features.Deckbuilder.Tiles;
using CardCleaner.Scripts.Features.Worldgen.AutoTiling;
using Godot;

namespace CardCleaner.Tests.Features.Worldgen.AutoTiling;

[TestSuite]
[RequireGodotRuntime]
public class AutoTileFormatDefinitionDataTest
{
    [TestCase]
    [TestCategory("Unit")]
    public void TwoDefinitionsBuiltFromTheSameInputsAreEqual()
    {
        var first = CreateFormat();
        var second = CreateFormat();

        AssertBool(first.Equals(second)).IsTrue();
        AssertInt(first.GetHashCode()).IsEqual(second.GetHashCode());
    }

    [TestCase]
    [TestCategory("Unit")]
    public void ATileWithoutAFormatIsComputedAsCorner4()
    {
        var tile = new TileDefinition("grass", "grass", TilePassability.Passable, new Vector2I(0, 0));
        var position = new Vector2I(5, 5);
        var sameTerrain = new HashSet<Vector2I> { new(5, 4), new(6, 5) };

        var viaHelper = AutoTileHelper.ComputeBitmask(
            position, tile, p => sameTerrain.Contains(p) ? "grass" : null);
        var viaCorner4 = NeighborBitmaskCorner.Compute(position, sameTerrain.Contains);

        AssertInt(viaHelper).IsEqual(viaCorner4);
    }

    private static AutoTileFormatDefinition CreateFormat()
    {
        var bitmasks = new HashSet<int> { 0, 1, 2 };
        var variants = new Dictionary<int, VariantDefinition>
        {
            [0] = new(new Vector2I(0, 0)),
            [1] = new(new Vector2I(1, 0)),
            [2] = new(new Vector2I(2, 0)),
        };
        return new AutoTileFormatDefinition("test_format", BitmaskType.Edge4, bitmasks, variants);
    }
}
