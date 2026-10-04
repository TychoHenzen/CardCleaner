using CardCleaner.Scripts.Core.Enumeration;
using Godot;
using CardSignature = CardCleaner.Scripts.Features.Card.Models.CardSignature;

namespace CardCleaner.Tests.Features.Card.Models.CardSignatureScenarios;

/// <summary>
///     CardSignature distance, subtraction, intensity and dominant aspect scenarios split out of CardSignatureTest.
/// </summary>
[TestSuite]
[RequireGodotRuntime]
public class CardSignatureMetricsTest
{
    [TestCase]
    public void TestDistanceCalculation()
    {
        var sig1 = new CardSignature(new[] { 1.0f, 0.0f, 0.0f, 0.0f, 0.0f, 0.0f, 0.0f, 0.0f });
        var sig2 = new CardSignature(new[] { 0.0f, 1.0f, 0.0f, 0.0f, 0.0f, 0.0f, 0.0f, 0.0f });

        var distance = sig1.DistanceTo(sig2);
        var expected = Mathf.Sqrt(2.0f); // sqrt(1^2 + 1^2)

        AssertFloat(distance).IsEqualApprox(expected, 0.001f);
    }

    [TestCase]
    public void TestSubtraction()
    {
        var sig1 = new CardSignature(new[] { 0.8f, 0.2f, -0.3f, 0.5f, 0.0f, -0.7f, 1.0f, -0.1f });
        var sig2 = new CardSignature(new[] { 0.3f, -0.1f, 0.2f, 0.0f, 0.5f, -0.2f, 0.3f, 0.4f });

        var result = sig1.Subtract(sig2);

        AssertFloat(result[0]).IsEqualApprox(0.5f, 0.001f); // 0.8 - 0.3
        AssertFloat(result[1]).IsEqualApprox(0.3f, 0.001f); // 0.2 - (-0.1)
        AssertFloat(result[2]).IsEqualApprox(-0.5f, 0.001f); // -0.3 - 0.2
        AssertFloat(result[7]).IsEqualApprox(-0.5f, 0.001f); // -0.1 - 0.4
    }

    [TestCase]
    public void TestDominantAspect()
    {
        var signature = new CardSignature();
        signature.Solidum = 0.7f; // Positive -> Tellus
        signature.Febris = -0.5f; // Negative -> Hydris

        AssertThat(signature.GetDominantAspect(Element.Solidum)).IsEqual(Aspect.Tellus);
        AssertThat(signature.GetDominantAspect(Element.Febris)).IsEqual(Aspect.Hydris);
    }

    [TestCase]
    public void TestIntensity()
    {
        var signature = new CardSignature();
        signature.Solidum = 0.7f;
        signature.Febris = -0.8f;

        AssertFloat(signature.GetIntensity(Element.Solidum)).IsEqual(0.7f);
        AssertFloat(signature.GetIntensity(Element.Febris)).IsEqual(0.8f);
    }

    [TestCase]
    public void TestIsSignificant()
    {
        var signature = new CardSignature();
        signature.Solidum = 0.15f;
        signature.Febris = 0.05f;

        AssertBool(signature.IsSignificant(Element.Solidum)).IsTrue();
        AssertBool(signature.IsSignificant(Element.Febris)).IsFalse();
    }

    [TestCase]
    public void TestDistanceToSelf()
    {
        var signature = new CardSignature(new[] { 0.5f, -0.3f, 0.8f, 0.0f, 0.0f, 0.0f, 0.0f, 0.0f });

        var distance = signature.DistanceTo(signature);

        AssertFloat(distance).IsEqual(0f);
    }

    [TestCase]
    public void TestDistanceIsSymmetric()
    {
        var sig1 = new CardSignature(new[] { 0.5f, -0.3f, 0.8f, 0.0f, 0.0f, 0.0f, 0.0f, 0.0f });
        var sig2 = new CardSignature(new[] { -0.2f, 0.7f, 0.1f, 0.4f, 0.0f, 0.0f, 0.0f, 0.0f });

        var dist1 = sig1.DistanceTo(sig2);
        var dist2 = sig2.DistanceTo(sig1);

        AssertFloat(dist1).IsEqualApprox(dist2, 0.0001f);
    }

    [TestCase]
    public void TestSubtractionResultIsClamped()
    {
        var sig1 = new CardSignature(new[] { 1.0f, -1.0f, 0.0f, 0.0f, 0.0f, 0.0f, 0.0f, 0.0f });
        var sig2 = new CardSignature(new[] { -1.0f, 1.0f, 0.0f, 0.0f, 0.0f, 0.0f, 0.0f, 0.0f });

        var result = sig1.Subtract(sig2);

        AssertFloat(result[0]).IsEqual(1.0f);
        AssertFloat(result[1]).IsEqual(-1.0f);
    }

    [TestCase]
    public void TestIsSignificantWithCustomThreshold()
    {
        var signature = new CardSignature();
        signature.Solidum = 0.3f;

        AssertBool(signature.IsSignificant(Element.Solidum, 0.2f)).IsTrue();
        AssertBool(signature.IsSignificant(Element.Solidum, 0.4f)).IsFalse();
    }

    [TestCase]
    public void TestDominantAspectAtZero()
    {
        var signature = new CardSignature();
        signature.Solidum = 0.0f;

        AssertThat(signature.GetDominantAspect(Element.Solidum)).IsEqual(Aspect.Tellus);
    }
}
