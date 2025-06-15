using System;
using CardCleaner.Scripts.Core.Enumeration;
using CardCleaner.Scripts.Core.Utilities;
using GdUnit4;

namespace CardCleaner.Tests.Core.Utilities;

[TestSuite]
public class EleAspectsEnhancedTest
{
    [TestCase]
    [TestCategory("Unit")]
    public void AllElements_HavePositiveAspects()
    {
        // Arrange & Act & Assert - Every element should have a positive aspect
        foreach (var element in Enum.GetValues<Element>())
        {
            var positiveAspect = element.Positive();
            Assertions.AssertThat(positiveAspect).IsNotNull();
            
            // Verify the aspect maps back to the same element
            var backToElement = positiveAspect.IsElement();
            Assertions.AssertThat(backToElement).IsEqual(element);
        }
    }

    [TestCase]
    [TestCategory("Unit")]
    public void AllElements_HaveNegativeAspects()
    {
        // Arrange & Act & Assert - Every element should have a negative aspect
        foreach (var element in Enum.GetValues<Element>())
        {
            var negativeAspect = element.Negative();
            Assertions.AssertThat(negativeAspect).IsNotNull();
            
            // Verify the aspect maps back to the same element
            var backToElement = negativeAspect.IsElement();
            Assertions.AssertThat(backToElement).IsEqual(element);
        }
    }

    [TestCase]
    [TestCategory("Unit")]
    public void PositiveAndNegative_AreDifferentForEachElement()
    {
        // Arrange & Act & Assert - Positive and negative aspects should be different
        foreach (var element in Enum.GetValues<Element>())
        {
            var positiveAspect = element.Positive();
            var negativeAspect = element.Negative();
            
            Assertions.AssertThat(positiveAspect).IsNotEqual(negativeAspect);
        }
    }

    [TestCase]
    [TestCategory("Unit")]
    public void SpecificElementMappings_AreCorrect()
    {
        // Test specific known mappings to ensure they're correct
        Assertions.AssertThat(Element.Solidum.Positive()).IsEqual(Aspect.Tellus);
        Assertions.AssertThat(Element.Solidum.Negative()).IsEqual(Aspect.Aeolis);
        
        Assertions.AssertThat(Element.Febris.Positive()).IsEqual(Aspect.Ignis);
        Assertions.AssertThat(Element.Febris.Negative()).IsEqual(Aspect.Hydris);
        
        Assertions.AssertThat(Element.Ordinem.Positive()).IsEqual(Aspect.Vitrio);
        Assertions.AssertThat(Element.Ordinem.Negative()).IsEqual(Aspect.Empyrus);
    }

    [TestCase]
    [TestCategory("Unit")]
    public void AllAspects_MapToValidElements()
    {
        // Arrange & Act & Assert - Every aspect should map to a valid element
        foreach (var aspect in Enum.GetValues<Aspect>())
        {
            var element = aspect.IsElement();
            Assertions.AssertThat(element).IsNotNull();
            
            // Verify this element has this aspect as either positive or negative
            var hasAspect = element.Positive() == aspect || element.Negative() == aspect;
            Assertions.AssertBool(hasAspect).IsTrue();
        }
    }

    [TestCase]
    [TestCategory("Unit")]
    public void ElementCount_MatchesExpectedGameDesign()
    {
        // Assert - Verify we have the expected number of elements for game balance
        var elementCount = Enum.GetValues<Element>().Length;
        Assertions.AssertThat(elementCount).IsEqual(8);
        
        // Each element should have exactly 2 aspects (positive + negative)
        var aspectCount = Enum.GetValues<Aspect>().Length;
        Assertions.AssertThat(aspectCount).IsEqual(elementCount * 2);
    }
}
