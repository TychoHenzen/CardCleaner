using System.Linq;
using System.Runtime.CompilerServices;
using CardCleaner.Scripts.Core.Enumeration;
using CardCleaner.Scripts.Core.Randomness;
using Godot;
using CardSignature = CardCleaner.Scripts.Features.Card.Models.CardSignature;

namespace CardCleaner.Tests.Features.Card.Models.CardSignatureScenarios;

/// <summary>
///     CardSignature construction, clamping, element accessor and indexer scenarios split out of CardSignatureTest.
/// </summary>
[TestSuite]
[RequireGodotRuntime]
public class CardSignatureConstructionTest
{
    [TestCase]
    [TestCategory("Unit")]
    public static void SignatureIsAReadonlyValueWithoutSetters()
    {
        AssertBool(typeof(CardSignature).IsValueType).IsTrue();
        AssertBool(typeof(CardSignature).IsDefined(typeof(IsReadOnlyAttribute), false)).IsTrue();
        AssertBool(typeof(CardSignature).GetProperties().All(property => property.SetMethod == null)).IsTrue();
    }

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
    [TestCategory("Unit")]
    public void TestSignatureClampingWithChanges()
    {
        var signature = new CardSignature();

        // Test clamping upper bound
        signature = signature.With(Element.Solidum, 2.0f);
        AssertFloat(signature.Solidum).IsEqual(1.0f);

        // Test clamping lower bound
        signature = signature.With(Element.Febris, -2.0f);
        AssertFloat(signature.Febris).IsEqual(-1.0f);

        // Test valid range
        signature = signature.With(Element.Ordinem, 0.5f);
        AssertFloat(signature.Ordinem).IsEqual(0.5f);
    }

    [TestCase]
    public void TestArrayConstructorWithInvalidLength()
    {
        AssertThrown(() => new CardSignature(new[] { 0.5f, 0.3f }))
            .IsInstanceOf<System.ArgumentException>()
            .HasMessage("Elements array must have exactly 8 values");
    }

    [TestCase]
    [TestCategory("Unit")]
    public void TestArrayConstructorClampsValues()
    {
        var signature = new CardSignature(new[] { 2.0f, -2.0f, 0.5f, 0.0f, 0.0f, 0.0f, 0.0f, 0.0f });

        AssertFloat(signature[0]).IsEqual(1.0f);
        AssertFloat(signature[1]).IsEqual(-1.0f);
        AssertFloat(signature[2]).IsEqual(0.5f);
    }

    [TestCase]
    public void TestRandomGeneratesValidSignature()
    {
        var rng = RandomSources.Create();
        rng.Seed = 12345;

        var signature = CardSignature.Random(rng);

        for (var i = 0; i < 8; i++)
        {
            AssertFloat(signature[i]).IsBetween(-1f, 1f);
        }
    }

    [TestCase]
    public void TestRandomIsDeterministicWithSeed()
    {
        var rng1 = RandomSources.Create();
        rng1.Seed = 42;
        var sig1 = CardSignature.Random(rng1);

        var rng2 = RandomSources.Create();
        rng2.Seed = 42;
        var sig2 = CardSignature.Random(rng2);

        for (var i = 0; i < 8; i++)
        {
            AssertFloat(sig1[i]).IsEqualApprox(sig2[i], 0.0001f);
        }
    }

    [TestCase]
    public void TestToDebugString()
    {
        var signature = new CardSignature(new[] { 0.5f, -0.25f, 1.0f, 0.0f, -1.0f, 0.33f, 0.67f, -0.5f });

        var debugString = signature.ToDebugString();

        AssertString(debugString).Contains("Signature[");
        AssertString(debugString).Contains("0.50");
        AssertString(debugString).Contains("-0.25");
    }

    [TestCase]
    public void TestElementsPropertyGetter()
    {
        var original = new[] { 0.1f, 0.2f, 0.3f, 0.4f, 0.5f, 0.6f, 0.7f, 0.8f };
        var signature = new CardSignature(original);

        var elements = signature.Elements;

        for (var i = 0; i < 8; i++)
        {
            AssertFloat(elements[i]).IsEqual(original[i]);
        }
    }

    [TestCase]
    public void TestElementsPropertyReturnsClone()
    {
        var signature = new CardSignature(new[] { 0.5f, 0.5f, 0.5f, 0.5f, 0.5f, 0.5f, 0.5f, 0.5f });

        var elements = signature.Elements;
        elements[0] = 0.0f;

        AssertFloat(signature[0]).IsEqual(0.5f);
    }

    [TestCase]
    public void TestIndexerWithIntegerIndex()
    {
        var signature = new CardSignature();

        signature = signature.With(3, 0.75f);

        AssertFloat(signature[3]).IsEqual(0.75f);
        AssertFloat(signature.Lumines).IsEqual(0.75f);
    }

    [TestCase]
    public void TestIndexerWithElementEnum()
    {
        var signature = new CardSignature();

        signature = signature.With(Element.Spatium, -0.5f);

        AssertFloat(signature[Element.Spatium]).IsEqual(-0.5f);
        AssertFloat(signature.Spatium).IsEqual(-0.5f);
    }

    [TestCase]
    public void TestAllElementPropertySettersClamp()
    {
        var signature = new CardSignature();

        signature = signature.With(Element.Lumines, 5.0f);
        signature = signature.With(Element.Varias, -3.0f);
        signature = signature.With(Element.Inertiae, 1.5f);
        signature = signature.With(Element.Subsidium, -1.5f);
        signature = signature.With(Element.Spatium, 0.5f);

        AssertFloat(signature.Lumines).IsEqual(1.0f);
        AssertFloat(signature.Varias).IsEqual(-1.0f);
        AssertFloat(signature.Inertiae).IsEqual(1.0f);
        AssertFloat(signature.Subsidium).IsEqual(-1.0f);
        AssertFloat(signature.Spatium).IsEqual(0.5f);
    }

    [TestCase]
    public void TestExactBoundaryValues()
    {
        var signature = new CardSignature();

        signature = signature.With(Element.Solidum, 1.0f);
        signature = signature.With(Element.Febris, -1.0f);

        AssertFloat(signature.Solidum).IsEqual(1.0f);
        AssertFloat(signature.Febris).IsEqual(-1.0f);
    }
}
