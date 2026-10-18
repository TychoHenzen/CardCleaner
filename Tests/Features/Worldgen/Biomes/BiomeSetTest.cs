using System.Linq;
using CardCleaner.Scripts.Core.Services;
using CardCleaner.Scripts.Features.Card.Models;
using CardCleaner.Scripts.Features.Worldgen.Biomes;

namespace CardCleaner.Tests.Features.Worldgen.Biomes;

[TestSuite]
[RequireGodotRuntime]
public class BiomeSetTest
{
    [TestCase]
    [TestCategory("Unit")]
    public static void From_KeepsEnumerationOrder()
    {
        var set = BiomeSet.From(new[]
        {
            CreateTestBiome("forest"),
            CreateTestBiome("desert"),
            CreateTestBiome("tundra")
        });

        AssertThat(set.Ids.ToList()).IsEqual(new[] { "forest", "desert", "tundra" }.ToList());
        AssertThat(set.Count).IsEqual(3);
    }

    [TestCase]
    [TestCategory("Unit")]
    public static void Get_UnknownId_ReturnsNull()
    {
        var set = BiomeSet.From(new[] { CreateTestBiome("forest") });

        AssertThat(set.Get("desert")).IsNull();
        AssertThat(set.Get("forest")).IsNotNull();
    }

    [TestCase]
    [TestCategory("Unit")]
    public static void FindClosestBySignature_EmptySet_ReturnsNull()
    {
        AssertThat(BiomeSet.Empty.FindClosestBySignature(new CardSignature())).IsNull();
    }

    [TestCase]
    [TestCategory("Unit")]
    public static void FindClosestBySignature_PicksNearestAffinity()
    {
        var set = BiomeSet.From(new[]
        {
            CreateTestBiome("tundra", new CardSignature(new[] { 0f, -0.8f, 0f, 0f, 0f, 0f, 0f, 0f })),
            CreateTestBiome("desert", new CardSignature(new[] { 0f, 0.8f, 0f, 0f, 0f, 0f, 0f, 0f }))
        });

        var query = new CardSignature(new[] { 0f, -0.3f, 0f, 0f, 0f, 0f, 0f, 0f });

        AssertThat(set.FindClosestBySignature(query)!.Id).IsEqual("tundra");
    }

    private static BiomeDefinition CreateTestBiome(string id, CardSignature? signature = null)
    {
        var passable = new TilePool.Builder();
        passable.Add("grass");
        var blocked = new TilePool.Builder();
        blocked.Add("wall");

        return new BiomeDefinition(
            id,
            signature ?? new CardSignature(),
            passable.Build(),
            blocked.Build());
    }
}
