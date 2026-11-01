using CardCleaner.Scripts.Core.Randomness;
using CardCleaner.Scripts.Features.Card.Models;
using Godot;

namespace CardCleaner.Tests.Features.Card.Models;

[TestSuite]
[RequireGodotRuntime]
public class CardLookSeedTest
{
    private const ulong Seed = 42;

    [TestCase]
    [TestCategory("Unit")]
    public static void SpecialSignatureHasNoLookSeed()
    {
        var special = new CardSignature(new float[] { 0.5f, 0f, 0f, 0f, 0f, 0f, 0f, 0f });
        var rng = RandomSources.Create(Seed);

        var look = CardLookSeed.Resolve(special, null, rng);

        AssertThat(look).IsNull();
        AssertThat(rng.Randi()).IsEqual(RandomSources.Create(Seed).Randi());
    }

    [TestCase]
    [TestCategory("Unit")]
    public static void SpecialSignatureIgnoresASavedSeed()
    {
        var special = new CardSignature(new float[] { 0.5f, 0f, 0f, 0f, 0f, 0f, 0f, 0f });

        var look = CardLookSeed.Resolve(special, 5, RandomSources.Create(Seed));

        AssertThat(look).IsNull();
    }

    [TestCase]
    [TestCategory("Unit")]
    public static void ZeroSignatureDrawsASeed()
    {
        var zero = new CardSignature(new float[8]);

        var look = CardLookSeed.Resolve(zero, null, RandomSources.Create(Seed));

        AssertBool(look.HasValue).IsTrue();
    }

    [TestCase]
    [TestCategory("Unit")]
    public static void TwoDrawsFromOneSourceDiffer()
    {
        var zero = new CardSignature(new float[8]);
        var rng = RandomSources.Create(Seed);

        var first = CardLookSeed.Resolve(zero, null, rng);
        var second = CardLookSeed.Resolve(zero, null, rng);

        AssertBool(first == second).IsFalse();
    }

    [TestCase]
    [TestCategory("Unit")]
    public static void SameRandomSeedGivesTheSameLookSeed()
    {
        var zero = new CardSignature(new float[8]);

        var first = CardLookSeed.Resolve(zero, null, RandomSources.Create(Seed));
        var second = CardLookSeed.Resolve(zero, null, RandomSources.Create(Seed));

        AssertBool(second == first).IsTrue();
    }

    [TestCase]
    [TestCategory("Unit")]
    public static void SavedSeedIsKeptAndRandomIsNotAdvanced()
    {
        var zero = new CardSignature(new float[8]);
        var rng = RandomSources.Create(Seed);

        var look = CardLookSeed.Resolve(zero, 1234, rng);

        AssertThat(look).IsEqual(1234);
        AssertThat(rng.Randi()).IsEqual(RandomSources.Create(Seed).Randi());
    }
}
