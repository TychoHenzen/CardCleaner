using System;
using System.Collections.Generic;
using System.Linq;
using CardCleaner.Scripts.Features.Worldgen;
using CardCleaner.Scripts.Features.Worldgen.AutoTiling;

namespace CardCleaner.Tests.Features.Worldgen;

[TestSuite]
[RequireGodotRuntime]
public class TransitionPairsTest
{
    [TestCase]
    [TestCategory("Unit")]
    public void KeepsThePairsInTheOrderGiven()
    {
        var pairs = new TransitionPairs(new[] { ("grass", "dirt"), ("dirt", "sand") });

        AssertThat(pairs.Pairs.Count).IsEqual(2);
        AssertThat(pairs.Pairs[0]).IsEqual(("grass", "dirt"));
        AssertThat(pairs.Pairs[1]).IsEqual(("dirt", "sand"));
    }

    [TestCase]
    [TestCategory("Unit")]
    public void LaterChangesToTheSourceDoNotReachTheValue()
    {
        var source = new List<(string tileA, string tileB)> { ("grass", "dirt") };
        var pairs = new TransitionPairs(source);

        source.Add(("dirt", "sand"));

        AssertThat(pairs.Pairs.Count).IsEqual(1);
    }

    [TestCase]
    [TestCategory("Unit")]
    public void NullPairsAreRefused()
    {
        AssertThrown(() => new TransitionPairs(null!)).IsInstanceOf<ArgumentNullException>();
    }

    [TestCase]
    public void CompiledResolverPairsCarryOverUnchanged()
    {
        var expected = new CompiledTransitionResolver().GetAllTransitionPairs().ToList();

        var pairs = new TransitionPairs(new CompiledTransitionResolver().GetAllTransitionPairs());

        AssertThat(expected.Count).IsGreater(0);
        AssertThat(pairs.Pairs.Count).IsEqual(expected.Count);
        AssertBool(pairs.Pairs.SequenceEqual(expected)).IsTrue();
    }
}
