using CardCleaner.Scripts.Features.Worldgen.Biomes;
using CardCleaner.Scripts.Core.Interfaces;
using CardCleaner.Scripts.Core.Randomness;
using Godot;

namespace CardCleaner.Tests.Features.Worldgen.Biomes;

[TestSuite]
[RequireGodotRuntime]
public class TilePoolTest
{
    private IRandom _rng = null!;

    [BeforeTest]
    public void Setup()
    {
        _rng = RandomSources.Create();
        _rng.Seed = 12345;
    }

    [TestCase]
    public void TestEmptyPoolReturnsNull()
    {
        var pool = TilePool.Empty;

        var result = TilePoolSelection.SelectRandom(pool, _rng);

        AssertThat(result).IsNull();
    }

    [TestCase]
    public void TestSingleEntryAlwaysReturned()
    {
        var builder = new TilePool.Builder();
        builder.Add("grass");
        var pool = builder.Build();

        for (var i = 0; i < 10; i++)
        {
            var result = TilePoolSelection.SelectRandom(pool, _rng);
            AssertString(result).IsEqual("grass");
        }
    }

    [TestCase]
    public void TestWeightedSelectionFavorsHigherWeight()
    {
        var builder = new TilePool.Builder();
        builder.Add("common", 9.0f);
        builder.Add("rare");
        var pool = builder.Build();

        var commonCount = 0;
        var rareCount = 0;

        for (var i = 0; i < 100; i++)
        {
            var result = TilePoolSelection.SelectRandom(pool, _rng);
            if (result == "common") commonCount++;
            else if (result == "rare") rareCount++;
        }

        AssertThat(commonCount).IsGreater(rareCount);
        AssertThat(commonCount).IsGreater(70);
    }

    [TestCase]
    public static void TestCountReturnsCorrectValue()
    {
        AssertThat(TilePool.Empty.Count).IsEqual(0);

        AssertThat(new TilePool.Builder().Add("grass").Build().Count).IsEqual(1);

        AssertThat(new TilePool.Builder().Add("grass").Add("dirt").Build().Count).IsEqual(2);
    }

    [TestCase]
    public static void TestIsEmptyReturnsCorrectValue()
    {
        AssertBool(TilePool.Empty.IsEmpty).IsTrue();

        AssertBool(new TilePool.Builder().Add("grass").Build().IsEmpty).IsFalse();
    }

    [TestCase]
    public static void TestTilePoolEntryProperties()
    {
        var entry = new TilePoolEntry("floor", 2.5f);

        AssertString(entry.TileId).IsEqual("floor");
        AssertThat(entry.Weight).IsEqual(2.5f);
    }

    [TestCase]
    public void TestDeterministicWithSameSeed()
    {
        var builder = new TilePool.Builder();
        builder.Add("a");
        builder.Add("b");
        builder.Add("c");
        var pool = builder.Build();

        _rng.Seed = 42;
        var results1 = new string[10];
        for (var i = 0; i < 10; i++)
            results1[i] = TilePoolSelection.SelectRandom(pool, _rng)!;

        _rng.Seed = 42;
        var results2 = new string[10];
        for (var i = 0; i < 10; i++)
            results2[i] = TilePoolSelection.SelectRandom(pool, _rng)!;

        for (var i = 0; i < 10; i++)
            AssertString(results1[i]).IsEqual(results2[i]);
    }

    [TestCase]
    public void TestZeroTotalWeightReturnsFirstEntry()
    {
        var pool = new TilePool.Builder().Add("first", 0.0f).Add("second", 0.0f).Build();

        AssertString(TilePoolSelection.SelectRandom(pool, _rng)).IsEqual("first");
    }

    [TestCase]
    public static void TestBuildCopiesTheBuilderEntries()
    {
        var builder = new TilePool.Builder().Add("grass");
        var pool = builder.Build();

        builder.Add("dirt");

        AssertThat(pool.Count).IsEqual(1);
    }
}
