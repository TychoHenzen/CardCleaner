using CardCleaner.Scripts.Features.Worldgen.Biomes;
using Godot;

namespace CardCleaner.Tests.Features.Worldgen.Biomes;

[TestSuite]
[RequireGodotRuntime]
public class TilePoolTest
{
    private RandomNumberGenerator _rng = null!;

    [BeforeTest]
    public void Setup()
    {
        _rng = new RandomNumberGenerator();
        _rng.Seed = 12345;
    }

    [TestCase]
    public void TestEmptyPoolReturnsNull()
    {
        var pool = new TilePool();

        var result = pool.SelectRandom(_rng);

        AssertThat(result).IsNull();
    }

    [TestCase]
    public void TestSingleEntryAlwaysReturned()
    {
        var pool = new TilePool();
        pool.Add("grass");

        for (var i = 0; i < 10; i++)
        {
            var result = pool.SelectRandom(_rng);
            AssertString(result).IsEqual("grass");
        }
    }

    [TestCase]
    public void TestWeightedSelectionFavorsHigherWeight()
    {
        var pool = new TilePool();
        pool.Add("common", 9.0f);
        pool.Add("rare");

        var commonCount = 0;
        var rareCount = 0;

        for (var i = 0; i < 100; i++)
        {
            var result = pool.SelectRandom(_rng);
            if (result == "common") commonCount++;
            else if (result == "rare") rareCount++;
        }

        AssertThat(commonCount).IsGreater(rareCount);
        AssertThat(commonCount).IsGreater(70);
    }

    [TestCase]
    public void TestCountReturnsCorrectValue()
    {
        var pool = new TilePool();
        AssertThat(pool.Count).IsEqual(0);

        pool.Add("grass");
        AssertThat(pool.Count).IsEqual(1);

        pool.Add("dirt");
        AssertThat(pool.Count).IsEqual(2);
    }

    [TestCase]
    public void TestIsEmptyReturnsCorrectValue()
    {
        var pool = new TilePool();
        AssertBool(pool.IsEmpty).IsTrue();

        pool.Add("grass");
        AssertBool(pool.IsEmpty).IsFalse();
    }

    [TestCase]
    public void TestTilePoolEntryProperties()
    {
        var entry = new TilePoolEntry("floor", 2.5f);

        AssertString(entry.TileId).IsEqual("floor");
        AssertThat(entry.Weight).IsEqual(2.5f);
    }

    [TestCase]
    public void TestDeterministicWithSameSeed()
    {
        var pool = new TilePool();
        pool.Add("a");
        pool.Add("b");
        pool.Add("c");

        _rng.Seed = 42;
        var results1 = new string[10];
        for (var i = 0; i < 10; i++)
            results1[i] = pool.SelectRandom(_rng)!;

        _rng.Seed = 42;
        var results2 = new string[10];
        for (var i = 0; i < 10; i++)
            results2[i] = pool.SelectRandom(_rng)!;

        for (var i = 0; i < 10; i++)
            AssertString(results1[i]).IsEqual(results2[i]);
    }
}
