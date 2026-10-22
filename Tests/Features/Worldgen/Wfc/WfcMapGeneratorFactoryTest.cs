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
/// Pins WfcMapGeneratorFactory: each Create call returns a fresh generator with the default retry count, and
/// GridMapGenerator refuses a null factory.
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
        var factory = new WfcMapGeneratorFactory();

        AssertBool(ReferenceEquals(factory.Create(pairs, null), factory.Create(pairs, null))).IsFalse();
    }

    [TestCase]
    [TestCategory("Unit")]
    public static void CreateReturnsAGeneratorWithTheDefaultRetryCount()
    {
        var pairs = new CompiledTransitionResolver(new GodotAssetPaths()).GetAllTransitionPairs().ToList();

        AssertInt(new WfcMapGeneratorFactory().Create(pairs, null).MaxRetries).IsEqual(3);
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
