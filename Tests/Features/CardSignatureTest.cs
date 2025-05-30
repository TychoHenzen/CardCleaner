using CardCleaner.Scripts.Core.Enum;
using GdUnit4;
using Godot;
using static GdUnit4.Assertions;
using CardSignature = CardCleaner.Scripts.Features.Card.Models.CardSignature;

namespace CardCleaner.Tests.Features;

[TestSuite]
public class CardSignatureTest
{
    [TestCase]
    public void TestSignatureCreation()
    {
        var signature = new CardSignature();

        // All elements should start at 0
        for (var i = 0; i < 8; i++) AssertFloat(signature[i]).IsEqual(0f);
    }

    [TestCase]
    public void TestSignatureArrayConstructor()
    {
        float[] elements = { 0.5f, -0.3f, 0.8f, -1.0f, 1.0f, 0.0f, -0.7f, 0.2f };
        var signature = new CardSignature(elements);

        for (var i = 0; i < 8; i++) AssertFloat(signature[i]).IsEqual(elements[i]);
    }

    [TestCase]
    public void TestSignatureClampingOnSet()
    {
        var signature = new CardSignature();

        // Test clamping upper bound
        signature.Solidum = 2.0f;
        AssertFloat(signature.Solidum).IsEqual(1.0f);

        // Test clamping lower bound
        signature.Febris = -2.0f;
        AssertFloat(signature.Febris).IsEqual(-1.0f);

        // Test valid range
        signature.Ordinem = 0.5f;
        AssertFloat(signature.Ordinem).IsEqual(0.5f);
    }

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
}