using System.Linq;
using CardCleaner.Scripts.Core.Randomness;
using CardCleaner.Scripts.Features.Card.Models;
using CardCleaner.Scripts.Features.Packs.Services;
using Godot;

namespace CardCleaner.Tests.Features.Packs.Services;

[TestSuite]
[RequireGodotRuntime]
public class CardPackGeneratorBoxShareTest
{
    private const ulong Seed = 20240531;
    private const int BoxesForShareCheck = 400;
    private const int CardsPerBox = 512;

    /// <summary>Share of 512-card boxes holding at least one special card: 1 - (199/200)^512, about 0.923.</summary>
    private const double ExpectedBoxShare = 0.923;

    /// <summary>Tolerance around <see cref="ExpectedBoxShare" />: about 4.5 sigma over 400 boxes.</summary>
    private const double BoxShareTolerance = 0.06;

    /// <summary>The user's lower target for the share of boxes holding at least one special card.</summary>
    private const double MinimumBoxShare = 0.90;

    [TestCase]
    [TestCategory("Unit")]
    public static void AnalyticBoxShareWithSpecialCardIsWithinUserTarget()
    {
        var analytic = 1.0 - System.Math.Pow(1.0 - 1.0 / CardPackGenerator.SpecialCardOdds, CardsPerBox);
        AssertThat(analytic).IsBetween(MinimumBoxShare, 0.95);
    }

    [TestCase]
    [TestCategory("Unit")]
    public static void SeededBoxShareWithSpecialCardIsNearAnalyticShare()
    {
        var generator = new CardPackGenerator(RandomSources.Create(Seed));
        var boxesWithSpecial = 0;

        for (var i = 0; i < BoxesForShareCheck; i++)
        {
            if (generator.OpenBox().Any(c => c.HasMagicalPotential()))
                boxesWithSpecial++;
        }

        // No floor at the target here: a 400-box sample falls below 0.90 by chance about 2 percent of the time.
        var share = (double)boxesWithSpecial / BoxesForShareCheck;
        AssertThat(share).IsBetween(ExpectedBoxShare - BoxShareTolerance, ExpectedBoxShare + BoxShareTolerance);
    }
}
