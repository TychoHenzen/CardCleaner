using System;
using CardCleaner.Scripts.Core.Enum;
using CardCleaner.Scripts.Core.Utilities;
using GdUnit4;

namespace CardCleaner.Tests.Features;

[TestSuite]
public class EleAspectsTest
{
    [TestCase]
    public void TestPositiveConversions()
    {
        Assertions.AssertThat(Element.Solidum.Positive()).IsEqual(Aspect.Tellus);
        Assertions.AssertThat(Element.Febris.Positive()).IsEqual(Aspect.Ignis);
        Assertions.AssertThat(Element.Ordinem.Positive()).IsEqual(Aspect.Vitrio);
        Assertions.AssertThat(Element.Lumines.Positive()).IsEqual(Aspect.Luminus);
        Assertions.AssertThat(Element.Varias.Positive()).IsEqual(Aspect.Spatius);
        Assertions.AssertThat(Element.Inertiae.Positive()).IsEqual(Aspect.Gravitas);
        Assertions.AssertThat(Element.Subsidium.Positive()).IsEqual(Aspect.Auxillus);
        Assertions.AssertThat(Element.Spatium.Positive()).IsEqual(Aspect.Disis);
    }

    [TestCase]
    public void TestNegativeConversions()
    {
        Assertions.AssertThat(Element.Solidum.Negative()).IsEqual(Aspect.Aeolis);
        Assertions.AssertThat(Element.Febris.Negative()).IsEqual(Aspect.Hydris);
        Assertions.AssertThat(Element.Ordinem.Negative()).IsEqual(Aspect.Empyrus);
        Assertions.AssertThat(Element.Lumines.Negative()).IsEqual(Aspect.Noctis);
        Assertions.AssertThat(Element.Varias.Negative()).IsEqual(Aspect.Tempus);
        Assertions.AssertThat(Element.Inertiae.Negative()).IsEqual(Aspect.Levitas);
        Assertions.AssertThat(Element.Subsidium.Negative()).IsEqual(Aspect.Malus);
        Assertions.AssertThat(Element.Spatium.Negative()).IsEqual(Aspect.Iuxta);
    }

    [TestCase]
    public void TestIsElementConversions()
    {
        // Test positive aspects
        Assertions.AssertThat(Aspect.Tellus.IsElement()).IsEqual(Element.Solidum);
        Assertions.AssertThat(Aspect.Ignis.IsElement()).IsEqual(Element.Febris);
        Assertions.AssertThat(Aspect.Vitrio.IsElement()).IsEqual(Element.Ordinem);

        // Test negative aspects
        Assertions.AssertThat(Aspect.Aeolis.IsElement()).IsEqual(Element.Solidum);
        Assertions.AssertThat(Aspect.Hydris.IsElement()).IsEqual(Element.Febris);
        Assertions.AssertThat(Aspect.Empyrus.IsElement()).IsEqual(Element.Ordinem);
    }

    [TestCase]
    public void TestBidirectionalConversion()
    {
        // Test that Element -> Positive Aspect -> Element works
        foreach (var element in Enum.GetValues<Element>())
        {
            var positiveAspect = element.Positive();
            var backToElement = positiveAspect.IsElement();
            Assertions.AssertThat(backToElement).IsEqual(element);
        }

        // Test that Element -> Negative Aspect -> Element works
        foreach (var element in Enum.GetValues<Element>())
        {
            var negativeAspect = element.Negative();
            var backToElement = negativeAspect.IsElement();
            Assertions.AssertThat(backToElement).IsEqual(element);
        }
    }
}