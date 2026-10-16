using CardCleaner.Scripts.Features.Card.Models;
using CardCleaner.Scripts.Core.Randomness;
using Godot;

namespace CardCleaner.Tests.Features.Card.Models;

[TestSuite]
[RequireGodotRuntime]
public class CardSignatureRandomSpecialTest
{
    private const ulong Seed = 20241007;
    private const int Samples = 200;

    [TestCase]
    [TestCategory("Unit")]
    public static void EveryRandomSpecialSignatureHasMagicalPotential()
    {
        var rng = RandomSources.Create(Seed);

        for (var i = 0; i < Samples; i++)
            AssertBool(CardSignatureHash.CreateSpecial(rng).HasMagicalPotential()).IsTrue();
    }

    [TestCase]
    [TestCategory("Unit")]
    public static void RandomSpecialSignatureElementsStayInTheCardRange()
    {
        var rng = RandomSources.Create(Seed);

        for (var i = 0; i < Samples; i++)
        {
            var signature = CardSignatureHash.CreateSpecial(rng);
            for (var element = 0; element < 8; element++)
                AssertBool(signature[element] is >= -1f and <= 1f).IsTrue();
        }
    }

    [TestCase]
    [TestCategory("Unit")]
    public static void SameSeedGivesTheSameSpecialSignature()
    {
        var first = CardSignatureHash.CreateSpecial(RandomSources.Create(Seed));
        var second = CardSignatureHash.CreateSpecial(RandomSources.Create(Seed));

        AssertThat(second.Elements).IsEqual(first.Elements);
    }
}
