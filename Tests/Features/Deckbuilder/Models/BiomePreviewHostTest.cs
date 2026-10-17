using System.Collections.Generic;
using CardCleaner.Scripts.Core.Interfaces;
using CardCleaner.Scripts.Features.Card.Models;
using CardCleaner.Scripts.Features.Deckbuilder.Models;
using CardCleaner.Scripts.Features.Worldgen.Biomes;
using CardCleaner.Tests.TestUtilities;
using Godot;

namespace CardCleaner.Tests.Features.Deckbuilder.Models;

/// <summary>
/// The biome preview lives inside the map viewport. Without a viewport there is nowhere to put it, so no preview is
/// created: a parentless Control would never be freed.
/// </summary>
[TestSuite]
[RequireGodotRuntime]
public class BiomePreviewHostTest
{
    [AfterTest]
    public static void TearDown()
    {
        TestServices.Reset();
    }

    [TestCase]
    [TestCategory("Unit")]
    public static void SetupWithoutAViewportLeavesNoOrphanNode()
    {
        var orphansBefore = OrphanNodeCount();

        new BiomePreviewHost().Setup(null);

        AssertThat(OrphanNodeCount()).IsEqual(orphansBefore);
    }

    [TestCase]
    [TestCategory("Unit")]
    public static void SetupPutsThePreviewInsideTheViewport()
    {
        var viewport = AutoFree(new SubViewport());

        new BiomePreviewHost().Setup(viewport);

        AssertThat(viewport.GetChildCount()).IsEqual(1);
    }

    [TestCase]
    [TestCategory("Unit")]
    public static void TwoHostsResolveTheRegisteredCatalog()
    {
        var catalog = new BiomeRegistry();
        TestServices.Container.RegisterSingleton<IBiomeCatalog>(catalog);
        var first = new BiomePreviewHost();
        var second = new BiomePreviewHost();

        TestServices.Resolver.Get<IBiomeCatalog>(first.UseCatalog);
        TestServices.Resolver.Get<IBiomeCatalog>(second.UseCatalog);

        AssertThat(first.Catalog).IsSame(catalog);
        AssertThat(second.Catalog).IsSame(catalog);
    }

    [TestCase]
    [TestCategory("Unit")]
    public static void SetupDoesNotBuildACatalog()
    {
        var host = new BiomePreviewHost();

        host.Setup(AutoFree(new SubViewport()));

        AssertThat(host.Catalog).IsNull();
    }

    [TestCase]
    [TestCategory("Unit")]
    public static void UpdateBeforeTheCatalogArrivesDoesNothing()
    {
        var host = new BiomePreviewHost();
        host.Setup(AutoFree(new SubViewport()));
        var recorder = new RecordingCatalog();

        host.Update(new[] { new CardSignature() });
        host.UseCatalog(recorder);

        // The early Update neither threw nor left work behind for the catalog.
        AssertThat(recorder.Lookups).IsEqual(0);
    }

    [TestCase]
    [TestCategory("Unit")]
    public static void ACatalogSuppliedAfterSetupIsUsedByTheNextUpdate()
    {
        var host = new BiomePreviewHost();
        host.Setup(AutoFree(new SubViewport()));
        var recorder = new RecordingCatalog();
        host.UseCatalog(recorder);

        host.Update(new[] { new CardSignature() });

        AssertThat(host.Catalog).IsSame(recorder);
        AssertThat(recorder.Lookups).IsGreater(0);
    }

    [TestCase]
    [TestCategory("Unit")]
    public static void UpdateWithoutAPreviewNeverConsultsTheCatalog()
    {
        var host = new BiomePreviewHost();
        var recorder = new RecordingCatalog();
        host.UseCatalog(recorder);

        host.Update(new[] { new CardSignature() });

        AssertThat(recorder.Lookups).IsEqual(0);
    }

    private static double OrphanNodeCount() => Performance.GetMonitor(Performance.Monitor.ObjectOrphanNodeCount);

    /// <summary>A default catalog that counts how often the distribution calculation asks it for biomes.</summary>
    private sealed class RecordingCatalog : IBiomeCatalog
    {
        private readonly BiomeRegistry _inner = CreateDefault();

        public int Lookups { get; private set; }

        public int Count => _inner.Count;

        public BiomeDefinition? GetBiome(string biomeId) => _inner.GetBiome(biomeId);

        public IEnumerable<BiomeDefinition> GetAllBiomes()
        {
            Lookups++;
            return _inner.GetAllBiomes();
        }

        public IEnumerable<string> GetAllBiomeIds() => _inner.GetAllBiomeIds();

        public BiomeDefinition? FindClosestBySignature(CardSignature signature)
        {
            Lookups++;
            return _inner.FindClosestBySignature(signature);
        }

        private static BiomeRegistry CreateDefault()
        {
            var registry = new BiomeRegistry();
            registry.RegisterDefaultBiomes();
            return registry;
        }
    }
}
