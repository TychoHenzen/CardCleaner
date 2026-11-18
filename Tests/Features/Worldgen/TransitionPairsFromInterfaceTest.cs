using System.Linq;
using CardCleaner.Scripts.Features.Worldgen;
using CardCleaner.Scripts.Features.Worldgen.AutoTiling;
using CardCleaner.Tests.TestUtilities;

namespace CardCleaner.Tests.Features.Worldgen;

[TestSuite]
[RequireGodotRuntime]
public class TransitionPairsFromInterfaceTest
{
    [TestCase]
    public void PairsFromTheInterfaceEqualThePairsOfTheConcreteResolver()
    {
        var expected = GameTransitionResolver.Shared.GetAllTransitionPairs().ToList();

        ITransitionResolver resolver = GameTransitionResolver.Shared;
        var pairs = new TransitionPairs(resolver.GetAllTransitionPairs());

        AssertThat(expected.Count).IsGreater(0);
        AssertThat(pairs.Pairs.Count).IsEqual(expected.Count);
        AssertBool(pairs.Pairs.SequenceEqual(expected)).IsTrue();
    }
}
