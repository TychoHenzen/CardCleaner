using CardCleaner.Scripts.Features.Deckbuilder.Models;
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

    private static double OrphanNodeCount() => Performance.GetMonitor(Performance.Monitor.ObjectOrphanNodeCount);
}
