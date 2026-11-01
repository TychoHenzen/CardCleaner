using System.Linq;
using CardCleaner.Scripts.Features.Card.Models;

namespace CardCleaner.Tests.Features.Card.Models;

[TestSuite]
[RequireGodotRuntime]
public class CardTemplateEdgesTest
{
    [TestCase]
    [TestCategory("Unit")]
    public static void EdgeLayersAreVisibleByDefault()
    {
        var template = new CardTemplate();

        AssertThat(template.GemSockets.Concat(template.Gems).All(l => l.RenderOnFront)).IsTrue();
    }

    [TestCase]
    [TestCategory("Unit")]
    public static void HidingEdgesTurnsOffEverySocketAndGemButNothingElse()
    {
        var template = new CardTemplate();

        template.SetSignatureEdgesVisible(false);

        AssertThat(template.GemSockets.Concat(template.Gems).Any(l => l.RenderOnFront)).IsFalse();
        AssertThat(template.CardBase.RenderOnFront).IsTrue();
        AssertThat(template.Border.RenderOnFront).IsTrue();
    }

    [TestCase]
    [TestCategory("Unit")]
    public static void EdgesCanBeShownAgain()
    {
        var template = new CardTemplate();
        template.SetSignatureEdgesVisible(false);

        template.SetSignatureEdgesVisible(true);

        AssertThat(template.GemSockets.Concat(template.Gems).All(l => l.RenderOnFront)).IsTrue();
    }

    [TestCase]
    [TestCategory("Unit")]
    public static void HidingTheEnergyOrbTurnsOffOnlyItsThreeLayers()
    {
        var template = new CardTemplate();

        template.SetEnergyOrbVisible(false);

        AssertThat(template.EnergyContainer.RenderOnFront).IsFalse();
        AssertThat(template.EnergyFill1.RenderOnFront).IsFalse();
        AssertThat(template.EnergyFill2.RenderOnFront).IsFalse();
        AssertThat(template.Border.RenderOnFront).IsTrue();
        AssertThat(template.Art.RenderOnFront).IsTrue();
        AssertThat(template.GemSockets.Concat(template.Gems).All(l => l.RenderOnFront)).IsTrue();
    }

    [TestCase]
    [TestCategory("Unit")]
    public static void TheEnergyOrbCanBeShownAgain()
    {
        var template = new CardTemplate();
        template.SetEnergyOrbVisible(false);

        template.SetEnergyOrbVisible(true);

        AssertThat(template.EnergyContainer.RenderOnFront).IsTrue();
        AssertThat(template.EnergyFill1.RenderOnFront).IsTrue();
        AssertThat(template.EnergyFill2.RenderOnFront).IsTrue();
    }

    [TestCase]
    [TestCategory("Unit")]
    public static void HidingTheEnergyOrbKeepsTheTwentySevenShaderLayers()
    {
        var template = new CardTemplate();

        template.SetEnergyOrbVisible(false);

        AssertThat(template.GatherAllLayers().Length).IsEqual(27);
    }
}
