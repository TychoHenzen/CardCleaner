using System.Linq;
using CardCleaner.Scripts.Core.Services;
using CardCleaner.Scripts.Features.Deckbuilder.Tiles;


namespace CardCleaner.Tests.Core.Services.VariationGroupScenarios;

/// <summary>
///     Tests for VariationGroup, VariationGroupCollection and probability weight handling; split
///     out of VariationGroupTest.
/// </summary>
[TestSuite]
[RequireGodotRuntime]
public class VariationGroupCollectionTest
{
    // ==================== VariationGroup Tests ====================

    [TestCase]
    public void TestVariationGroup_MaxWeight()
    {
        var variants = new[]
        {
            new VariantWeight("grass1", 0.8f),
            new VariantWeight("grass2", 1.0f),
            new VariantWeight("grass3", 0.6f)
        };

        var group = new VariationGroup("grass", VariationMode.PerGeneration, variants);

        AssertThat(group.MaxWeight).IsEqual(1.0f);
    }

    [TestCase]
    public void TestVariationGroup_GetWeight()
    {
        var variants = new[]
        {
            new VariantWeight("flower_a", 0.5f),
            new VariantWeight("flower_b", 0.3f)
        };

        var group = new VariationGroup("flower", VariationMode.PerInstance, variants);

        AssertThat(group.GetWeight("flower_a")).IsEqual(0.5f);
        AssertThat(group.GetWeight("flower_b")).IsEqual(0.3f);
        AssertThat(group.GetWeight("flower_c")).IsEqual(0f); // Not in group
    }

    [TestCase]
    public void TestVariationGroup_GetNormalizedWeight()
    {
        var variants = new[]
        {
            new VariantWeight("rose_a", 0.8f),
            new VariantWeight("rose_b", 0.4f)
        };

        var group = new VariationGroup("rose", VariationMode.PerInstance, variants);

        // MaxWeight = 0.8, so rose_a normalized = 1.0, rose_b normalized = 0.5
        AssertThat(group.GetNormalizedWeight("rose_a")).IsEqual(1.0f);
        AssertThat(group.GetNormalizedWeight("rose_b")).IsEqual(0.5f);
    }

    [TestCase]
    public void TestVariationGroup_ContainsTile()
    {
        var variants = new[]
        {
            new VariantWeight("grass1", 1.0f),
            new VariantWeight("grass2", 1.0f)
        };

        var group = new VariationGroup("grass", VariationMode.PerGeneration, variants);

        AssertThat(group.ContainsTile("grass1")).IsTrue();
        AssertThat(group.ContainsTile("grass2")).IsTrue();
        AssertThat(group.ContainsTile("grass3")).IsFalse();
    }

    // ==================== VariationGroupCollection Tests ====================

    [TestCase]
    public void TestVariationGroupCollection_AddAndLookup()
    {
        var collection = new VariationGroupCollection();

        var grassVariants = new[]
        {
            new VariantWeight("grass1", 1.0f),
            new VariantWeight("grass2", 0.8f)
        };
        var grassGroup = new VariationGroup("grass", VariationMode.PerGeneration, grassVariants);
        collection.AddGroup(grassGroup);

        // Lookup by base name
        var found = collection.GetGroupByBaseName("grass");
        AssertThat(found).IsNotNull();
        AssertThat(found!.BaseName).IsEqual("grass");
    }

    [TestCase]
    public void TestVariationGroupCollection_FindGroupContaining()
    {
        var collection = new VariationGroupCollection();

        var variants = new[]
        {
            new VariantWeight("flower_a", 0.4f),
            new VariantWeight("flower_b", 0.6f)
        };
        var group = new VariationGroup("flower", VariationMode.PerInstance, variants);
        collection.AddGroup(group);

        // Find group by member tile ID
        var foundByA = collection.FindGroupContaining("flower_a");
        var foundByB = collection.FindGroupContaining("flower_b");
        var notFound = collection.FindGroupContaining("rose_a");

        AssertThat(foundByA).IsNotNull();
        AssertThat(foundByB).IsNotNull();
        AssertThat(foundByA!.BaseName).IsEqual("flower");
        AssertThat(notFound).IsNull();
    }

    [TestCase]
    public void TestVariationGroupCollection_GetVariantsFor()
    {
        var collection = new VariationGroupCollection();

        var variants = new[]
        {
            new VariantWeight("grass1", 0.5f),
            new VariantWeight("grass2", 1.0f),
            new VariantWeight("grass3", 0.7f)
        };
        collection.AddGroup(new VariationGroup("grass", VariationMode.PerGeneration, variants));

        var retrieved = collection.GetVariantsFor("grass");

        AssertThat(retrieved.Count).IsEqual(3);
        AssertThat(retrieved.Any(v => v.TileId == "grass1" && v.Weight == 0.5f)).IsTrue();
    }

    [TestCase]
    public void TestVariationGroupCollection_Clear()
    {
        var collection = new VariationGroupCollection();
        collection.AddGroup(new VariationGroup("test", VariationMode.PerGeneration,
            new[] { new VariantWeight("test1", 1f) }));

        AssertThat(collection.Count).IsEqual(1);

        collection.Clear();

        AssertThat(collection.Count).IsEqual(0);
        AssertThat(collection.GetGroupByBaseName("test")).IsNull();
    }

    // ==================== Probability/Density Tests ====================

    [TestCase]
    public void TestDensity_HighProbabilityGroupHasHigherDensity()
    {
        // Flower group with max prob 0.4
        var flowerVariants = new[]
        {
            new VariantWeight("flower_a", 0.4f),
            new VariantWeight("flower_b", 0.2f)
        };
        var flowerGroup = new VariationGroup("flower", VariationMode.PerInstance, flowerVariants);

        // Decoration group with max prob 0.8
        var decorVariants = new[]
        {
            new VariantWeight("decor_a", 0.8f),
            new VariantWeight("decor_b", 0.3f)
        };
        var decorGroup = new VariationGroup("decor", VariationMode.PerInstance, decorVariants);

        // Decor should have 2x the density of flower
        AssertThat(decorGroup.MaxWeight).IsEqual(0.8f);
        AssertThat(flowerGroup.MaxWeight).IsEqual(0.4f);
        AssertThat(decorGroup.MaxWeight / flowerGroup.MaxWeight).IsEqual(2.0f);
    }

    [TestCase]
    public void TestRelativeWeight_RatioPreserved()
    {
        // Poppy weight 2, Rose weight 1 → poppy should be 2x more likely
        var variants = new[]
        {
            new VariantWeight("poppy", 2.0f),
            new VariantWeight("rose", 1.0f)
        };
        var group = new VariationGroup("flowers", VariationMode.PerInstance, variants);

        var poppyNorm = group.GetNormalizedWeight("poppy"); // 2/2 = 1.0
        var roseNorm = group.GetNormalizedWeight("rose");   // 1/2 = 0.5

        // Poppy should have 2x the relative weight of rose
        AssertThat(poppyNorm / roseNorm).IsEqual(2.0f);
    }
}
