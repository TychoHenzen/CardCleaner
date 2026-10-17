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
        var viewport = AutoFree(new SubViewport());
        var host = new BiomePreviewHost();
        host.Setup(viewport);

        host.Update(new[] { new CardSignature() });

        AssertThat(viewport.GetChildCount()).IsEqual(1);
    }

    [TestCase]
    [TestCategory("Unit")]
    public static void ACatalogSuppliedAfterSetupIsUsedByTheNextUpdate()
    {
        var viewport = AutoFree(new SubViewport());
        var host = new BiomePreviewHost();
        host.Setup(viewport);
        var catalog = new BiomeRegistry();
        catalog.RegisterDefaultBiomes();
        host.UseCatalog(catalog);

        host.Update(new[] { new CardSignature() });

        AssertThat(host.Catalog).IsSame(catalog);
        AssertThat(viewport.GetChildCount()).IsEqual(1);
    }

    private static double OrphanNodeCount() => Performance.GetMonitor(Performance.Monitor.ObjectOrphanNodeCount);
}
