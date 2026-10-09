using System.Linq;
using CardCleaner.Scripts.Features.Card.Models;
using CardCleaner.Scripts.Features.Worldgen.Biomes;

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
        var biome = CreateTestBiome("forest");
        _registry.Register(biome);

        var retrieved = _registry.GetBiome("forest");

        AssertThat(retrieved).IsNotNull();
        AssertThat(retrieved!.Id).IsEqual("forest");
    }

    [TestCase]
    public void TestGetBiomeReturnsNullForMissing()
    {
        var result = _registry.GetBiome("desert");

        AssertThat(result).IsNull();
    }

    [TestCase]
    public void TestCountReturnsCorrectValue()
    {
        AssertThat(_registry.Count).IsEqual(0);

        _registry.Register(CreateTestBiome("forest"));
        AssertThat(_registry.Count).IsEqual(1);

        _registry.Register(CreateTestBiome("desert"));
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

        var forest = CreateTestBiome("forest", forestSignature);
        var desert = CreateTestBiome("desert", desertSignature);

        _registry.Register(forest);
        _registry.Register(desert);

        var querySignature = new CardSignature(new[] { 0f, -0.5f, 0f, 0f, 0f, 0f, 0f, 0f });
        var result = _registry.FindClosestBySignature(querySignature);

        AssertThat(result).IsNotNull();
        AssertThat(result!.Id).IsEqual("forest");
    }

    [TestCase]
    public void TestFindClosestBySignatureFindsClosestBiome()
    {
        var coldSignature = new CardSignature(new[] { 0f, -0.8f, 0f, 0f, 0f, 0f, 0f, 0f });
        var hotSignature = new CardSignature(new[] { 0f, 0.8f, 0f, 0f, 0f, 0f, 0f, 0f });

        var tundra = CreateTestBiome("tundra", coldSignature);
        var desert = CreateTestBiome("desert", hotSignature);

        _registry.Register(tundra);
        _registry.Register(desert);

        var slightlyColdQuery = new CardSignature(new[] { 0f, -0.3f, 0f, 0f, 0f, 0f, 0f, 0f });
        var result = _registry.FindClosestBySignature(slightlyColdQuery);

        AssertThat(result).IsNotNull();
        AssertThat(result!.Id).IsEqual("tundra");
    }

    [TestCase]
    public void TestClearRemovesAllBiomes()
    {
        _registry.Register(CreateTestBiome("forest"));
        _registry.Register(CreateTestBiome("desert"));
        AssertThat(_registry.Count).IsEqual(2);

        _registry.Clear();

        AssertThat(_registry.Count).IsEqual(0);
    }

    [TestCase]
    public void TestRegisterDefaultBiomesCreatesAllBiomes()
    {
        _registry.RegisterDefaultBiomes();

        AssertThat(_registry.Count).IsEqual(10);
        AssertThat(_registry.GetBiome("plains")).IsNotNull();
        AssertThat(_registry.GetBiome("forest")).IsNotNull();
        AssertThat(_registry.GetBiome("desert")).IsNotNull();
        AssertThat(_registry.GetBiome("tundra")).IsNotNull();
        AssertThat(_registry.GetBiome("swamp")).IsNotNull();
        AssertThat(_registry.GetBiome("mountains")).IsNotNull();
        AssertThat(_registry.GetBiome("water")).IsNotNull();
        AssertThat(_registry.GetBiome("cave")).IsNotNull();
        AssertThat(_registry.GetBiome("volcanic")).IsNotNull();
        AssertThat(_registry.GetBiome("magical")).IsNotNull();
    }

    [TestCase]
    public void TestDefaultBiomesKeepTheirTilesJsonContent()
    {
        _registry.RegisterDefaultBiomes();

        // Pinned from the biomes section of Data/Tiles/tiles.json on master (#205): affinity signature, blocked
        // percentage, passable tile count and blocked tile count for each default biome.
        var expected = new (string Id, float[] Signature, float BlockedPercentage, int Passable, int Blocked)[]
        {
            ("plains", [0f, 0f, 0.2f, 0.1f, 0f, 0f, 0.2f, 0f], 0.15f, 5, 3),
            ("forest", [0f, -0.3f, -0.3f, -0.3f, 0f, 0.2f, 0.3f, 0f], 0.4f, 5, 5),
            ("desert", [0.3f, 0.7f, 0.3f, 0.4f, 0f, -0.2f, -0.2f, 0.3f], 0.12f, 5, 5),
            ("tundra", [0.2f, -0.7f, 0.5f, 0.3f, 0f, 0.1f, 0f, -0.1f], 0.22f, 5, 5),
            ("swamp", [-0.2f, -0.2f, -0.5f, -0.5f, 0f, -0.3f, -0.1f, 0f], 0.35f, 5, 5),
            ("mountains", [0.7f, -0.2f, 0.4f, 0.2f, 0f, 0.4f, 0f, 0.3f], 0.38f, 5, 5),
            ("water", [-0.5f, -0.3f, 0.2f, 0.3f, 0.2f, -0.5f, 0.1f, 0f], 0.3f, 4, 3),
            ("cave", [0.6f, -0.1f, -0.2f, -0.7f, 0f, 0.5f, 0f, -0.3f], 0.35f, 2, 7),
            ("volcanic", [0.3f, 0.9f, -0.4f, 0.4f, 0f, 0.3f, -0.6f, 0f], 0.3f, 5, 2),
            ("magical", [-0.2f, 0.1f, -0.6f, 0.5f, 0.5f, -0.3f, 0.3f, 0.4f], 0.25f, 4, 2),
        };

        foreach (var (id, signature, blockedPercentage, passable, blocked) in expected)
        {
            var biome = _registry.GetBiome(id);
            AssertThat(biome).IsNotNull();
            AssertBool(biome!.AffinitySignature.Elements.SequenceEqual(signature)).IsTrue();
            AssertThat(biome.BlockedPercentage).IsEqual(blockedPercentage);
            AssertThat(biome.PassableTiles.Count).IsEqual(passable);
            AssertThat(biome.BlockedTiles.Count).IsEqual(blocked);
        }
    }

    [TestCase]
    public void TestGetAllBiomesReturnsRegisteredBiomes()
    {
        _registry.Register(CreateTestBiome("forest"));
        _registry.Register(CreateTestBiome("desert"));

        var allBiomes = _registry.GetAllBiomes();
        var count = 0;

        foreach (var biome in allBiomes)
        {
            count++;
            AssertBool(biome.Id == "forest" || biome.Id == "desert").IsTrue();
        }

        AssertThat(count).IsEqual(2);
    }

    private static BiomeDefinition CreateTestBiome(string id, CardSignature? signature = null)
    {
        var passable = new TilePool();
        passable.Add("grass");
        var blocked = new TilePool();
        blocked.Add("wall");

        return new BiomeDefinition(
            id,
            signature ?? new CardSignature(),
            passable,
            blocked);
    }
}
