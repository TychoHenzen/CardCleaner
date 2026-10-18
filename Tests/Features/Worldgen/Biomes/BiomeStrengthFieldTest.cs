using CardCleaner.Scripts.Core.Services;
using CardCleaner.Scripts.Features.Card.Models;
using CardCleaner.Scripts.Features.Worldgen.Biomes;
using Godot;

namespace CardCleaner.Tests.Features.Worldgen.Biomes;

[TestSuite]
[RequireGodotRuntime]
public class BiomeStrengthFieldTest
{
    [TestCase]
    [TestCategory("Unit")]
    public static void GetStrength_OutOfRangeOrUnknownBiome_ReturnsZero()
    {
        var size = new Vector2I(4, 3);
        var biomes = BiomeSet.From(new[] { CreateTestBiome("forest") });
        var field = BiomeStrengthGrid.Sample(size, (_, _) => new CardSignature(), biomes);

        AssertThat(field.GetStrength(new Vector2I(-1, 0), "forest")).IsEqual(0f);
        AssertThat(field.GetStrength(new Vector2I(0, -1), "forest")).IsEqual(0f);
        AssertThat(field.GetStrength(new Vector2I(size.X, 0), "forest")).IsEqual(0f);
        AssertThat(field.GetStrength(new Vector2I(0, size.Y), "forest")).IsEqual(0f);
        AssertThat(field.GetStrength(new Vector2I(1, 1), "desert")).IsEqual(0f);
    }

    private static BiomeDefinition CreateTestBiome(string id)
    {
        var passable = new TilePool.Builder();
        passable.Add("grass");
        var blocked = new TilePool.Builder();
        blocked.Add("wall");

        return new BiomeDefinition(id, new CardSignature(), passable.Build(), blocked.Build());
    }
}
