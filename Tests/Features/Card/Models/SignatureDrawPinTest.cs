using CardCleaner.Scripts.Core.Interfaces;
using CardCleaner.Scripts.Core.Randomness;
using CardCleaner.Scripts.Features.Card.Models;

namespace CardCleaner.Tests.Features.Card.Models;

/// <summary>
/// Pins the random draws behind <see cref="CardSignature" /> so moving the factory (#278) cannot change a drawn value,
/// the redraw loop or the magical-potential boundary. The goldens were captured from the code before the move.
/// </summary>
[TestSuite]
[RequireGodotRuntime]
public class SignatureDrawPinTest
{
    private const ulong Seed = 20241007;

    [TestCase]
    [TestCategory("Unit")]
    public static void ScriptedDrawsFillTheElementsInIndexOrderAndClamp()
    {
        var random = new ScriptedRandom(0.5f, -0.25f, 2f, -3f, 0f, 1f, -1f, 0.125f);

        var signature = Create(random);

        AssertThat(signature.Elements).IsEqual(new[] { 0.5f, -0.25f, 1f, -1f, 0f, 1f, -1f, 0.125f });
        AssertThat(random.DrawCount).IsEqual(8);
    }

    [TestCase]
    [TestCategory("Unit")]
    public static void SpecialRedrawsAnAllZeroSetAndConsumesSixteenDraws()
    {
        var random = new ScriptedRandom(
            0f, 0f, 0f, 0f, 0f, 0f, 0f, 0f,
            0.1f, 0f, 0f, 0f, 0f, 0f, 0f, 0f);

        var signature = CreateSpecial(random);

        AssertThat(signature.Elements).IsEqual(new[] { 0.1f, 0f, 0f, 0f, 0f, 0f, 0f, 0f });
        AssertThat(random.DrawCount).IsEqual(16);
    }

    [TestCase]
    [TestCategory("Unit")]
    public static void SpecialRedrawsASetBelowTheToleranceToo()
    {
        var random = new ScriptedRandom(
            9.9e-07f, 0f, 0f, 0f, 0f, 0f, 0f, 0f,
            0f, 0f, 0f, 0f, 0f, 0f, 0f, 1e-06f);

        var signature = CreateSpecial(random);

        AssertThat(signature.Elements).IsEqual(new[] { 0f, 0f, 0f, 0f, 0f, 0f, 0f, 1e-06f });
        AssertThat(random.DrawCount).IsEqual(16);
    }

    [TestCase]
    [TestCategory("Unit")]
    public static void SpecialKeepsAMagicalFirstSetAfterEightDraws()
    {
        var random = new ScriptedRandom(0f, 0f, 0.3f, 0f, 0f, 0f, 0f, 0f);

        var signature = CreateSpecial(random);

        AssertThat(signature.Elements).IsEqual(new[] { 0f, 0f, 0.3f, 0f, 0f, 0f, 0f, 0f });
        AssertThat(random.DrawCount).IsEqual(8);
    }

    [TestCase]
    [TestCategory("Unit")]
    public static void SeededStreamGivesTheGoldenSignatures()
    {
        var rng = RandomSources.Create(Seed);

        AssertThat(Create(rng).Elements).IsEqual(new[] { -0.795537233f, 0.495647311f, -0.871603727f, 0.541520834f,
            0.985523105f, 0.00482690334f, 0.265921116f, 0.152097583f });
        AssertThat(Create(rng).Elements).IsEqual(new[] { 0.434692383f, 0.570616722f, 0.654587865f, 0.987582922f,
            -0.331046402f, -0.158083737f, -0.09206599f, 0.386302948f });
        AssertThat(CreateSpecial(rng).Elements).IsEqual(new[] { 0.484570742f, 0.473117232f, -0.963312507f, 0.574622869f,
            0.953571916f, -0.677119255f, 0.567402959f, -0.687358022f });
    }

    [TestCategory("Unit")]
    [TestCase(0f, false)]
    [TestCase(9.9e-07f, false)]
    [TestCase(1e-06f, true)]
    [TestCase(-1e-06f, true)]
    [TestCase(1e-05f, true)]
    public static void MagicalPotentialBoundaryOfTheFirstElement(float value, bool expected)
    {
        var signature = new CardSignature(new[] { value, 0f, 0f, 0f, 0f, 0f, 0f, 0f });

        AssertThat(signature.HasMagicalPotential()).IsEqual(expected);
    }

    [TestCase]
    [TestCategory("Unit")]
    public static void MagicalPotentialBoundaryHoldsForTheLastElementToo()
    {
        var below = new CardSignature(new[] { 0f, 0f, 0f, 0f, 0f, 0f, 0f, -9.9e-07f });
        var at = new CardSignature(new[] { 0f, 0f, 0f, 0f, 0f, 0f, 0f, 1e-06f });

        AssertThat(below.HasMagicalPotential()).IsFalse();
        AssertThat(at.HasMagicalPotential()).IsTrue();
    }

    private static CardSignature Create(IRandom random) => CardSignatureHash.Create(random);

    private static CardSignature CreateSpecial(IRandom random) => CardSignatureHash.CreateSpecial(random);

    private sealed class ScriptedRandom(params float[] values) : IRandom
    {
        private int _next;

        public int DrawCount => _next;

        public ulong Seed { get; set; }

        public float RandfRange(float minimum, float maximum) => values[_next++];

        public uint Randi() => throw new System.NotSupportedException();

        public float Randf() => throw new System.NotSupportedException();

        public int RandiRange(int minimum, int maximum) => throw new System.NotSupportedException();

        public float Randfn(float mean, float deviation) => throw new System.NotSupportedException();

        public void Randomize()
        {
        }
    }
}
