using CardCleaner.Scripts.Features.Card.Models;
using CardCleaner.Scripts.Core.Randomness;
using Godot;

namespace CardCleaner.Tests.Features.Card.Models;

[TestSuite]
public class CardSignatureRandomFaintTest
{
    private const ulong Seed = 20261120;
    private const int Samples = 500;

    [TestCase]
    public static void EveryFaintElementStaysWithinTheCap()
    {
        var rng = RandomSources.Create(Seed);

        for (var i = 0; i < Samples; i++)
        {
            var signature = SignatureRandom.CreateFaint(rng);
            for (var element = 0; element < 8; element++)
                AssertBool(Mathf.Abs(signature[element]) <= SignatureRandom.FaintCap).IsTrue();
        }
    }

    [TestCase]
    public static void EveryFaintSignatureHasANonZeroElement()
    {
        var rng = RandomSources.Create(Seed);

        for (var i = 0; i < Samples; i++)
            AssertBool(SignatureRandom.CreateFaint(rng).HasMagicalPotential()).IsTrue();
    }

    [TestCase]
    public static void SameSeedGivesTheSameFaintSignature()
    {
        var first = SignatureRandom.CreateFaint(RandomSources.Create(Seed));
        var second = SignatureRandom.CreateFaint(RandomSources.Create(Seed));

        AssertThat(second.Elements).IsEqual(first.Elements);
    }
}
