using System.Collections.Generic;
using System.Linq;
using CardCleaner.Scripts.Core.Enumeration;
using CardCleaner.Scripts.Core.Randomness;
using CardCleaner.Scripts.Features.Card.Models;

namespace CardCleaner.Tests.Features.Card.Models;

[TestSuite]
public class FlavorTextTest
{
    private static bool ContainsAnyWord(string sentence, IEnumerable<string> words) =>
        words.Any(word => sentence.Contains(word, System.StringComparison.OrdinalIgnoreCase));

    [TestCase]
    public static void AMundaneCardGetsAFillerSentence()
    {
        for (var seed = 0; seed < 50; seed++)
            AssertThat(FlavorText.Filler.Contains(FlavorText.For(new CardSignature(), seed))).IsTrue();

        AssertThat(FlavorText.Filler.Contains(FlavorText.For(new CardSignature(), null))).IsTrue();
    }

    [TestCase]
    public static void TwoMundaneCardsChooseTheirFillerIndependently()
    {
        var sentences = Enumerable.Range(0, 100).Select(seed => FlavorText.For(new CardSignature(), seed)).ToHashSet();

        AssertThat(sentences.Count).IsGreater(1);
    }

    [TestCase]
    public static void NoFillerSentenceNamesAnElementWord()
    {
        var allWords = Enumerable.Range(0, 8)
            .SelectMany(e => FlavorText.WordsFor((Element)e, false).Concat(FlavorText.WordsFor((Element)e, true)));

        foreach (var filler in FlavorText.Filler)
            AssertThat(ContainsAnyWord(filler, allWords)).OverrideFailureMessage(filler).IsFalse();
    }

    [TestCase]
    public static void TheStrongestElementsWordAppearsForItsDirection()
    {
        var hot = new CardSignature().With(Element.Febris, 0.8f).With(Element.Lumines, 0.1f);
        var cold = new CardSignature().With(Element.Febris, -0.8f).With(Element.Lumines, 0.1f);

        AssertThat(ContainsAnyWord(FlavorText.For(hot, null), FlavorText.WordsFor(Element.Febris, true))).IsTrue();
        AssertThat(ContainsAnyWord(FlavorText.For(cold, null), FlavorText.WordsFor(Element.Febris, false))).IsTrue();
        // A second element under half as strong adds no word.
        AssertThat(ContainsAnyWord(FlavorText.For(hot, null), FlavorText.WordsFor(Element.Lumines, true))).IsFalse();
    }

    [TestCase]
    public static void ASecondElementAtLeastHalfAsStrongAddsItsWord()
    {
        var signature = new CardSignature().With(Element.Febris, 0.8f).With(Element.Lumines, -0.4f);

        var sentence = FlavorText.For(signature, null);

        AssertThat(ContainsAnyWord(sentence, FlavorText.WordsFor(Element.Febris, true))).IsTrue();
        AssertThat(ContainsAnyWord(sentence, FlavorText.WordsFor(Element.Lumines, false))).IsTrue();
    }

    [TestCase]
    public static void AFaintCardUsesItsElementWordNotAFiller()
    {
        var rng = RandomSources.Create(77UL);
        for (var sample = 0; sample < 100; sample++)
        {
            var faint = SignatureRandom.CreateFaint(rng);
            var strongest = Enumerable.Range(0, 8).OrderByDescending(i => System.MathF.Abs(faint[i])).First();
            var sentence = FlavorText.For(faint, sample);

            AssertThat(FlavorText.Filler.Contains(sentence)).IsFalse();
            AssertThat(ContainsAnyWord(sentence, FlavorText.WordsFor((Element)strongest, faint[strongest] > 0f)))
                .OverrideFailureMessage(sentence).IsTrue();
        }
    }

    [TestCase]
    public static void TheSameInputsGiveTheSameSentence()
    {
        var signature = new CardSignature(new[] { 0.3f, -0.7f, 0.1f, 0.5f, 0f, 0f, -0.2f, 0.9f });

        AssertThat(FlavorText.For(signature, null)).IsEqual(FlavorText.For(signature, null));
        AssertThat(FlavorText.For(new CardSignature(), 12)).IsEqual(FlavorText.For(new CardSignature(), 12));
    }

    [TestCase]
    public static void ASignatureCardIgnoresItsLookSeed()
    {
        var signature = new CardSignature().With(Element.Spatium, 0.6f);

        AssertThat(FlavorText.For(signature, 1)).IsEqual(FlavorText.For(signature, 2));
    }

    [TestCase]
    public static void EverySentenceIsPlainAscii()
    {
        var rng = RandomSources.Create(5UL);
        var sentences = Enumerable.Range(0, 200).Select(_ => FlavorText.For(SignatureRandom.Create(rng), null))
            .Concat(FlavorText.Filler);

        foreach (var sentence in sentences)
        {
            AssertThat(sentence.All(c => c is >= ' ' and <= '~')).OverrideFailureMessage(sentence).IsTrue();
            AssertThat(char.IsUpper(sentence[0])).OverrideFailureMessage(sentence).IsTrue();
        }
    }
}
