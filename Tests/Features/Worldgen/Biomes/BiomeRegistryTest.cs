using CardCleaner.Scripts.Core.Enumeration;
using CardCleaner.Scripts.Features.Card.Models;
using CardCleaner.Scripts.Features.Worldgen.Biomes;
using GdUnit4;
using static GdUnit4.Assertions;

namespace CardCleaner.Tests.Features.Worldgen.Biomes;

[TestSuite]
[RequireGodotRuntime]
public class BiomeRegistryTest
{
    private BiomeRegistry _registry = null!;

    [BeforeTest]
    public void Setup() => _registry = new BiomeRegistry();

    [TestCase]
    public void TestRegisterAndRetrieveBiome()
    {
        var biome = CreateTestBiome(BiomeType.Forest);
        _registry.Register(biome);

        var retrieved = _registry.GetBiome(BiomeType.Forest);

        AssertThat(retrieved).IsNotNull();
        AssertThat(retrieved!.Type).IsEqual(BiomeType.Forest);
    }

    [TestCase]
    public void TestGetBiomeReturnsNullForMissing()
    {
        var result = _registry.GetBiome(BiomeType.Desert);

        AssertThat(result).IsNull();
    }

    [TestCase]
    public void TestCountReturnsCorrectValue()
    {
        AssertThat(_registry.Count).IsEqual(0);

        _registry.Register(CreateTestBiome(BiomeType.Forest));
        AssertThat(_registry.Count).IsEqual(1);

        _registry.Register(CreateTestBiome(BiomeType.Desert));
        AssertThat(_registry.Count).IsEqual(2);
    }

    [TestCase]
    public void TestFindClosestBySignatureReturnsNullForEmptyRegistry()
    {
        var signature = new CardSignature();

        var result = _registry.FindClosestBySignature(signature);

        AssertThat(result).IsNull();
    }

    [TestCase]
    public void TestFindClosestBySignatureFindsExactMatch()
    {
        var forestSignature = new CardSignature(new[] { 0f, -0.5f, 0f, 0f, 0f, 0f, 0f, 0f });
        var desertSignature = new CardSignature(new[] { 0f, 0.5f, 0f, 0f, 0f, 0f, 0f, 0f });

        var forest = CreateTestBiome(BiomeType.Forest, forestSignature);
        var desert = CreateTestBiome(BiomeType.Desert, desertSignature);

        _registry.Register(forest);
        _registry.Register(desert);

        var querySignature = new CardSignature(new[] { 0f, -0.5f, 0f, 0f, 0f, 0f, 0f, 0f });
        var result = _registry.FindClosestBySignature(querySignature);

        AssertThat(result).IsNotNull();
        AssertThat(result!.Type).IsEqual(BiomeType.Forest);
    }

    [TestCase]
    public void TestFindClosestBySignatureFindsClosestBiome()
    {
        var coldSignature = new CardSignature(new[] { 0f, -0.8f, 0f, 0f, 0f, 0f, 0f, 0f });
        var hotSignature = new CardSignature(new[] { 0f, 0.8f, 0f, 0f, 0f, 0f, 0f, 0f });

        var tundra = CreateTestBiome(BiomeType.Tundra, coldSignature);
        var desert = CreateTestBiome(BiomeType.Desert, hotSignature);

        _registry.Register(tundra);
        _registry.Register(desert);

        var slightlyColdQuery = new CardSignature(new[] { 0f, -0.3f, 0f, 0f, 0f, 0f, 0f, 0f });
        var result = _registry.FindClosestBySignature(slightlyColdQuery);

        AssertThat(result).IsNotNull();
        AssertThat(result!.Type).IsEqual(BiomeType.Tundra);
    }

    [TestCase]
    public void TestClearRemovesAllBiomes()
    {
        _registry.Register(CreateTestBiome(BiomeType.Forest));
        _registry.Register(CreateTestBiome(BiomeType.Desert));
        AssertThat(_registry.Count).IsEqual(2);

        _registry.Clear();

        AssertThat(_registry.Count).IsEqual(0);
    }

    [TestCase]
    public void TestRegisterDefaultBiomesCreates4Biomes()
    {
        _registry.RegisterDefaultBiomes();

        AssertThat(_registry.Count).IsEqual(4);
        AssertThat(_registry.GetBiome(BiomeType.Plains)).IsNotNull();
        AssertThat(_registry.GetBiome(BiomeType.Forest)).IsNotNull();
        AssertThat(_registry.GetBiome(BiomeType.Desert)).IsNotNull();
        AssertThat(_registry.GetBiome(BiomeType.Tundra)).IsNotNull();
    }

    [TestCase]
    public void TestGetAllBiomesReturnsRegisteredBiomes()
    {
        _registry.Register(CreateTestBiome(BiomeType.Forest));
        _registry.Register(CreateTestBiome(BiomeType.Desert));

        var allBiomes = _registry.GetAllBiomes();
        var count = 0;

        foreach (var biome in allBiomes)
        {
            count++;
            AssertBool(biome.Type == BiomeType.Forest || biome.Type == BiomeType.Desert).IsTrue();
        }

        AssertThat(count).IsEqual(2);
    }

    private static BiomeDefinition CreateTestBiome(BiomeType type, CardSignature? signature = null)
    {
        var passable = new TilePool();
        passable.Add("grass");
        var blocked = new TilePool();
        blocked.Add("wall");

        return new BiomeDefinition(
            type,
            signature ?? new CardSignature(),
            passable,
            blocked);
    }
}
