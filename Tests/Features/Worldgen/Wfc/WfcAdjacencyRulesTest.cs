using CardCleaner.Scripts.Features.Worldgen.Wfc;
using GdUnit4;
using static GdUnit4.Assertions;

namespace CardCleaner.Tests.Features.Worldgen.Wfc;

[TestSuite]
public class WfcAdjacencyRulesTest
{
    [TestCase]
    public void TestTileCanBeAdjacentToItself()
    {
        var rules = new WfcAdjacencyRules(new[]
        {
            ("grass", "dirt")
        });

        AssertBool(rules.CanBeAdjacent("grass", "grass")).IsTrue();
        AssertBool(rules.CanBeAdjacent("dirt", "dirt")).IsTrue();
    }

    [TestCase]
    public void TestSymmetricAdjacency()
    {
        var rules = new WfcAdjacencyRules(new[]
        {
            ("grass", "dirt")
        });

        // grass → dirt means both can be neighbors
        AssertBool(rules.CanBeAdjacent("grass", "dirt")).IsTrue();
        AssertBool(rules.CanBeAdjacent("dirt", "grass")).IsTrue();
    }

    [TestCase]
    public void TestNoTransitionMeansNotAdjacent()
    {
        var rules = new WfcAdjacencyRules(new[]
        {
            ("grass", "dirt"),
            ("dirt", "sand")
        });

        // No grass-sand transition defined
        AssertBool(rules.CanBeAdjacent("grass", "sand")).IsFalse();
    }

    [TestCase]
    public void TestGetValidNeighborsIncludesSelf()
    {
        var rules = new WfcAdjacencyRules(new[]
        {
            ("grass", "dirt")
        });

        var neighbors = rules.GetValidNeighbors("grass");

        AssertBool(neighbors.Contains("grass")).IsTrue();
    }

    [TestCase]
    public void TestGetValidNeighborsIncludesTransitions()
    {
        var rules = new WfcAdjacencyRules(new[]
        {
            ("grass", "dirt"),
            ("grass", "sand")
        });

        var neighbors = rules.GetValidNeighbors("grass");

        AssertBool(neighbors.Contains("grass")).IsTrue();
        AssertBool(neighbors.Contains("dirt")).IsTrue();
        AssertBool(neighbors.Contains("sand")).IsTrue();
    }

    [TestCase]
    public void TestAllTileIdsContainsAllTiles()
    {
        var rules = new WfcAdjacencyRules(new[]
        {
            ("grass", "dirt"),
            ("dirt", "sand"),
            ("sand", "water")
        });

        var allIds = rules.AllTileIds;

        AssertBool(allIds.Contains("grass")).IsTrue();
        AssertBool(allIds.Contains("dirt")).IsTrue();
        AssertBool(allIds.Contains("sand")).IsTrue();
        AssertBool(allIds.Contains("water")).IsTrue();
        AssertThat(allIds.Count).IsEqual(4);
    }

    [TestCase]
    public void TestGetCommonValidNeighbors()
    {
        var rules = new WfcAdjacencyRules(new[]
        {
            ("grass", "dirt"),
            ("grass", "sand"),
            ("dirt", "sand"),
            ("dirt", "water")
        });

        // Common neighbors of grass and dirt
        var common = rules.GetCommonValidNeighbors(new[] { "grass", "dirt" });

        // grass can neighbor: grass, dirt, sand
        // dirt can neighbor: grass, dirt, sand, water
        // Common: grass, dirt, sand
        AssertBool(common.Contains("grass")).IsTrue();
        AssertBool(common.Contains("dirt")).IsTrue();
        AssertBool(common.Contains("sand")).IsTrue();
        AssertBool(common.Contains("water")).IsFalse();
    }

    [TestCase]
    public void TestUnknownTileCanOnlyNeighborItself()
    {
        var rules = new WfcAdjacencyRules(new[]
        {
            ("grass", "dirt")
        });

        var neighbors = rules.GetValidNeighbors("unknown");

        AssertThat(neighbors.Count).IsEqual(1);
        AssertBool(neighbors.Contains("unknown")).IsTrue();
    }
}
