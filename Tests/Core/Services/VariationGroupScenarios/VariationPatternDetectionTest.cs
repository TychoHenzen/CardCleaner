using CardCleaner.Scripts.Core.Services.TilesetLoading;
using CardCleaner.Scripts.Core.Services;
using CardCleaner.Scripts.Features.Deckbuilder.Tiles;


namespace CardCleaner.Tests.Core.Services.VariationGroupScenarios;

/// <summary>
///     Tests for numbered and lettered tile variation name pattern detection; split out of VariationGroupTest.
/// </summary>
[TestSuite]
[RequireGodotRuntime]
public class VariationPatternDetectionTest
{
    // ==================== Name Pattern Detection Tests ====================

    [TestCase]
    public void TestDetectNumberedSuffix_ReturnsPerGenerationMode()
    {
        var result = VariationPatternDetector.DetectVariationPattern("grass1");

        AssertThat(result).IsNotNull();
        AssertThat(result!.BaseName).IsEqual("grass");
        AssertThat(result.VariantIndex).IsEqual(1);
        AssertThat(result.Mode).IsEqual(VariationMode.PerGeneration);
    }

    [TestCase]
    public void TestDetectNumberedSuffix_MultiDigit()
    {
        var result = VariationPatternDetector.DetectVariationPattern("terrain12");

        AssertThat(result).IsNotNull();
        AssertThat(result!.BaseName).IsEqual("terrain");
        AssertThat(result.VariantIndex).IsEqual(12);
        AssertThat(result.Mode).IsEqual(VariationMode.PerGeneration);
    }

    [TestCase]
    public void TestDetectLetteredSuffix_ReturnsPerInstanceMode()
    {
        var result = VariationPatternDetector.DetectVariationPattern("flower_a");

        AssertThat(result).IsNotNull();
        AssertThat(result!.BaseName).IsEqual("flower");
        AssertThat(result.VariantIndex).IsEqual(0); // a = 0
        AssertThat(result.Mode).IsEqual(VariationMode.PerInstance);
    }

    [TestCase]
    public void TestDetectLetteredSuffix_LetterC()
    {
        var result = VariationPatternDetector.DetectVariationPattern("rose_c");

        AssertThat(result).IsNotNull();
        AssertThat(result!.BaseName).IsEqual("rose");
        AssertThat(result.VariantIndex).IsEqual(2); // c = 2
        AssertThat(result.Mode).IsEqual(VariationMode.PerInstance);
    }

    [TestCase]
    public void TestDetectLetteredSuffix_CaseInsensitive()
    {
        var result = VariationPatternDetector.DetectVariationPattern("poppy_B");

        AssertThat(result).IsNotNull();
        AssertThat(result!.BaseName).IsEqual("poppy");
        AssertThat(result.VariantIndex).IsEqual(1); // B = 1
        AssertThat(result.Mode).IsEqual(VariationMode.PerInstance);
    }

    [TestCase]
    public void TestDetectNoPattern_ReturnsNull()
    {
        var result = VariationPatternDetector.DetectVariationPattern("dirt");

        AssertThat(result).IsNull();
    }

    [TestCase]
    public void TestDetectNoPattern_ShortBaseName_ReturnsNull()
    {
        // Base name too short (less than 2 chars)
        var result = VariationPatternDetector.DetectVariationPattern("a1");

        AssertThat(result).IsNull();
    }

    [TestCase]
    public void TestDetectNoPattern_SnakeCaseWithoutLetter_ReturnsNull()
    {
        // Has underscore but not followed by single letter
        var result = VariationPatternDetector.DetectVariationPattern("tall_grass");

        AssertThat(result).IsNull();
    }
}
