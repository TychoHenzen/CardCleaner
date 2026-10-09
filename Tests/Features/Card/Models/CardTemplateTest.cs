// Tests/Features/Card/Models/CardTemplateTest.cs

using CardCleaner.Scripts.Features.Card.Models;
using GdUnit4;

namespace CardCleaner.Tests.Features.Card.Models;

[TestSuite]
[RequireGodotRuntime]
public class CardTemplateTest
{
    private CardTemplate _template = null!;

    [BeforeTest]
    public void Setup()
    {
        _template = new CardTemplate();
    }

    [TestCase]
    [TestCategory("Unit")]
    public void GatherAllLayers_ReturnsCorrectLayerCount()
    {
        // Act
        var layers = _template.GatherAllLayers();

        // Assert
        var expectedCount = 11 + 8 + 8; // Base layers + GemSockets + Gems
        Assertions.AssertThat(layers.Length).IsEqual(expectedCount);
    }

    [TestCase]
    [TestCategory("Unit")]
    public void GatherAllLayers_ReturnsLayersInCorrectOrder()
    {
        // Act
        var layers = _template.GatherAllLayers();

        // Assert - Should be reversed (ToArray() is called)
        Assertions.AssertThat(layers[0]).IsEqual(_template.Gems[7]);
        Assertions.AssertThat(layers[^1]).IsEqual(_template.CardBase);
    }

    [TestCase]
    [TestCategory("Unit")]
    public void DefaultLayers_HaveCorrectRenderingSides()
    {
        // Assert - Verify default rendering flags
        Assertions.AssertBool(_template.CardBase.RenderOnFront).IsTrue();
        Assertions.AssertBool(_template.CardBase.RenderOnBack).IsTrue();

        Assertions.AssertBool(_template.Art.RenderOnFront).IsTrue();
        Assertions.AssertBool(_template.Art.RenderOnBack).IsFalse();

        Assertions.AssertBool(_template.Symbol.RenderOnFront).IsFalse();
        Assertions.AssertBool(_template.Symbol.RenderOnBack).IsTrue();
    }

    [TestCase]
    [TestCategory("Unit")]
    public void DefaultRegions_AreWithinValidBounds()
    {
        // Act & Assert - All regions should be within 0-1 range
        var allLayers = _template.GatherAllLayers();

        foreach (var layer in allLayers)
        {
            var region = layer.Region;
            Assertions.AssertThat(region.Position.X).IsBetween(0f, 1f);
            Assertions.AssertThat(region.Position.Y).IsBetween(0f, 1f);
            //we want to scale some regions up slightly
            Assertions.AssertThat(region.Size.X).IsBetween(0f, 1.1f);
            Assertions.AssertThat(region.Size.Y).IsBetween(0f, 1.1f);
        }
    }

    [TestCase]
    [TestCategory("Unit")]
    public void GemSocketsAndGems_HaveSameCount()
    {
        // Assert
        Assertions.AssertThat(_template.GemSockets.Length).IsEqual(_template.Gems.Length);
        Assertions.AssertThat(_template.GemSockets.Length).IsEqual(8);
    }

    [TestCase]
    [TestCategory("Unit")]
    public void GemSocketsAndGems_HaveMatchingRegions()
    {
        // Assert - Corresponding sockets and gems should have same regions
        for (var i = 0; i < _template.GemSockets.Length; i++)
            Assertions.AssertThat(_template.GemSockets[i].Region)
                .IsEqual(_template.Gems[i].Region);
    }

    [TestCase]
    [TestCategory("Unit")]
    public void EnergyLayers_HaveSameRegion()
    {
        // Assert - All energy layers should occupy same space
        var containerRegion = _template.EnergyContainer.Region;

        Assertions.AssertThat(_template.EnergyFill1.Region).IsEqual(containerRegion);
        Assertions.AssertThat(_template.EnergyFill2.Region).IsEqual(containerRegion);
    }
}