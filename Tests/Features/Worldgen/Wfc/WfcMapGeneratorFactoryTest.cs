using System;
using System.Linq;
using CardCleaner.Scripts.Core.Services;
using CardCleaner.Scripts.Core.Services.CompiledAtlas;
using CardCleaner.Scripts.Features.Deckbuilder.Services;
using CardCleaner.Scripts.Features.Deckbuilder.Tiles;
using CardCleaner.Scripts.Features.Worldgen.AutoTiling;
using CardCleaner.Scripts.Features.Worldgen.Biomes;
using CardCleaner.Scripts.Features.Worldgen.Wfc;
using CardCleaner.Tests.TestUtilities;
using Godot;

namespace CardCleaner.Tests.Features.Worldgen.Wfc;

/// <summary>
/// Pins WfcMapGenerator.Factory: each Create call returns a fresh generator with the default retry count, the
/// generator it creates keeps an assigned retry count and accepts null variants, and GridMapGenerator refuses a
/// null factory.
/// </summary>
[TestSuite]
[RequireGodotRuntime]
public class WfcMapGeneratorFactoryTest
{
    [TestCase]
    [TestCategory("Unit")]
    public static void CreateReturnsANewGeneratorEachCall()
    {
        var pairs = new CompiledTransitionResolver(new GodotAssetPaths()).GetAllTransitionPairs().ToList();
        IWfcMapGeneratorFactory factory = new WfcMapGenerator.Factory();

        AssertBool(ReferenceEquals(factory.Create(pairs, null), factory.Create(pairs, null))).IsFalse();
    }

    [TestCase]
    [TestCategory("Unit")]
    public static void CreateReturnsAGeneratorWithTheDefaultRetryCount()
    {
        var pairs = new CompiledTransitionResolver(new GodotAssetPaths()).GetAllTransitionPairs().ToList();

        AssertInt(WfcMapGenerator.Factory.Build(pairs, null).MaxRetries).IsEqual(3);
    }

    [TestCase]
    [TestCategory("Unit")]
    public static void CreateAcceptsNullVariantsAndKeepsAnAssignedRetryCount()
    {
        var pairs = new CompiledTransitionResolver(new GodotAssetPaths()).GetAllTransitionPairs().ToList();

        var generator = WfcMapGenerator.Factory.Build(pairs, null);
        AssertInt(generator.MaxRetries).IsEqual(3);

        generator.MaxRetries = 5;
        generator.SetSelectedVariants(null);
        AssertInt(generator.MaxRetries).IsEqual(5);
    }

    [TestCase]
    [TestCategory("Unit")]
    public static void GridMapGeneratorRejectsANullFactory()
    {
        var tiles = TileCatalogData.From(TileCatalogLoad.Load(new GodotAssetPaths(), new CompiledAtlasSource()));
        var biomeRegistry = new BiomeRegistry();
        biomeRegistry.RegisterDefaultBiomes(new GodotAssetPaths());
        Exception? caught = null;

        try
        {
            _ = new GridMapGenerator(tiles, biomeRegistry.ToBiomeSet(), CompiledTransitionPairs.Load(), null!);
        }
        catch (Exception ex)
        {
            caught = ex;
        }

        AssertBool(caught is ArgumentNullException).IsTrue();
    }
}
