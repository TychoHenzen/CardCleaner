using System.Text;
using CardCleaner.Scripts.Core.Randomness;
using CardCleaner.Scripts.Features.Worldgen.Biomes;
using Godot;

namespace CardCleaner.Tests.Features.Worldgen.Biomes;

[TestSuite]
[RequireGodotRuntime]
public class TilePoolSelectionPinTest
{
    private const string PinnedSequence = "b,a,d,b,d,d,b,a,d,a,c,b,a,d,c,d,d,d,b,d,b,d,a,d,b,b,c,a,d,a,c,b,b,b,a,d,b,c,b,d,";

    [TestCase]
    [TestCategory("Unit")]
    public void FixedSeedSequenceIsPinned()
    {
        var pool = new TilePool();
        pool.Add("a", 1.0f);
        pool.Add("b", 2.5f);
        pool.Add("c", 0.5f);
        pool.Add("d", 4.0f);
        var rng = RandomSources.Create();
        rng.Seed = 777;

        var text = new StringBuilder();
        for (var i = 0; i < 40; i++)
            text.Append(pool.SelectRandom(rng)).Append(',');

        AssertString(text.ToString()).IsEqual(PinnedSequence);
    }
}
