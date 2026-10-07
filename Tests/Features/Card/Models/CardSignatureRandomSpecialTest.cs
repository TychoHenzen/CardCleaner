using CardCleaner.Scripts.Features.Card.Models;
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
        var rng = new RandomNumberGenerator { Seed = Seed };

        for (var i = 0; i < Samples; i++)
            AssertBool(CardSignature.RandomSpecial(rng).HasMagicalPotential()).IsTrue();
    }

    [TestCase]
    [TestCategory("Unit")]
    public static void RandomSpecialSignatureElementsStayInTheCardRange()
    {
        var rng = new RandomNumberGenerator { Seed = Seed };

        for (var i = 0; i < Samples; i++)
        {
            var signature = CardSignature.RandomSpecial(rng);
            for (var element = 0; element < 8; element++)
                AssertBool(signature[element] is >= -1f and <= 1f).IsTrue();
        }
    }

    [TestCase]
    [TestCategory("Unit")]
    public static void SameSeedGivesTheSameSpecialSignature()
    {
        var first = CardSignature.RandomSpecial(new RandomNumberGenerator { Seed = Seed });
        var second = CardSignature.RandomSpecial(new RandomNumberGenerator { Seed = Seed });

        AssertThat(second.Elements).IsEqual(first.Elements);
    }
}
