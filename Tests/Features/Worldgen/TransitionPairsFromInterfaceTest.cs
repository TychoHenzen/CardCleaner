using System.Linq;
using CardCleaner.Scripts.Core.Services;
using CardCleaner.Scripts.Features.Worldgen;
using CardCleaner.Scripts.Features.Worldgen.AutoTiling;

namespace CardCleaner.Tests.Features.Worldgen;

[TestSuite]
[RequireGodotRuntime]
public class TransitionPairsFromInterfaceTest
{
    [TestCase]
    [TestCategory("Unit")]
    public void PairsFromTheInterfaceEqualThePairsOfTheConcreteResolver()
    {
        var expected = new CompiledTransitionResolver(new GodotAssetPaths()).GetAllTransitionPairs().ToList();

        ITransitionResolver resolver = TransitionResolvers.Create(new GodotAssetPaths());
        var pairs = new TransitionPairs(resolver.GetAllTransitionPairs());

        AssertThat(expected.Count).IsGreater(0);
        AssertThat(pairs.Pairs.Count).IsEqual(expected.Count);
        AssertBool(pairs.Pairs.SequenceEqual(expected)).IsTrue();
    }
}
