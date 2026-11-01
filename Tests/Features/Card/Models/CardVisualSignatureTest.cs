using System.Linq;
using CardCleaner.Scripts.Core.Randomness;
using CardCleaner.Scripts.Features.Card.Models;
using Godot;

namespace CardCleaner.Tests.Features.Card.Models;

[TestSuite]
[RequireGodotRuntime]
public class CardVisualSignatureTest
{
    private const ulong Seed = 42;

    [TestCase]
    [TestCategory("Unit")]
    public static void SpecialSignatureReturnsItself()
    {
        var special = new CardSignature(new float[] { 0.5f, 0f, 0f, 0f, 0f, 0f, 0f, 0f });

        var visual = CardVisualSignature.For(special, RandomSources.Create(Seed));

        AssertThat(visual.Elements).IsEqual(special.Elements);
    }

    [TestCase]
    [TestCategory("Unit")]
    public static void ZeroSignatureGetsARandomVisualSignature()
    {
        var zero = new CardSignature(new float[8]);

        var visual = CardVisualSignature.For(zero, RandomSources.Create(Seed));

        AssertBool(visual.Elements.SequenceEqual(zero.Elements)).IsFalse();
    }

    [TestCase]
    [TestCategory("Unit")]
    public static void TwoDrawsFromOneSourceDiffer()
    {
        var zero = new CardSignature(new float[8]);
        var rng = RandomSources.Create(Seed);

        var first = CardVisualSignature.For(zero, rng);
        var second = CardVisualSignature.For(zero, rng);

        AssertBool(first.Elements.SequenceEqual(second.Elements)).IsFalse();
    }

    [TestCase]
    [TestCategory("Unit")]
    public static void SameSeedGivesTheSameVisualSignature()
    {
        var zero = new CardSignature(new float[8]);

        var first = CardVisualSignature.For(zero, RandomSources.Create(Seed));
        var second = CardVisualSignature.For(zero, RandomSources.Create(Seed));

        AssertThat(second.Elements).IsEqual(first.Elements);
    }
}
