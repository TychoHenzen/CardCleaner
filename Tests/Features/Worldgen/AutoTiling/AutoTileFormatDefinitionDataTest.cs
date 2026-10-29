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
    public void DefinitionsDifferingInNameBitmasksOrVariantsAreNotEqual()
    {
        var baseline = CreateFormat();
        var otherName = new AutoTileFormatDefinition(
            "other_format", BitmaskType.Edge4, new HashSet<int> { 0, 1, 2 }, Variants(new Vector2I(2, 0)));
        var otherBitmasks = new AutoTileFormatDefinition(
            "test_format", BitmaskType.Edge4, new HashSet<int> { 0, 1, 3 }, Variants(new Vector2I(2, 0)));
        var otherVariant = new AutoTileFormatDefinition(
            "test_format", BitmaskType.Edge4, new HashSet<int> { 0, 1, 2 }, Variants(new Vector2I(9, 9)));

        AssertBool(baseline.Equals(otherName)).IsFalse();
        AssertBool(baseline.Equals(otherBitmasks)).IsFalse();
        AssertBool(baseline.Equals(otherVariant)).IsFalse();
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

    private static Dictionary<int, VariantDefinition> Variants(Vector2I third) => new()
    {
        [0] = new(new Vector2I(0, 0)),
        [1] = new(new Vector2I(1, 0)),
        [2] = new(third),
    };

    private static AutoTileFormatDefinition CreateFormat()
    {
        var bitmasks = new HashSet<int> { 0, 1, 2 };
        return new AutoTileFormatDefinition("test_format", BitmaskType.Edge4, bitmasks, Variants(new Vector2I(2, 0)));
    }
}
