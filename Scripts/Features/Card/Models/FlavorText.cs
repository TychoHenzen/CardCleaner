using System;
using System.Collections.Generic;
using System.Linq;
using CardCleaner.Scripts.Core.Enumeration;
using CardCleaner.Scripts.Core.Interfaces;
using CardCleaner.Scripts.Core.Randomness;

namespace CardCleaner.Scripts.Features.Card.Models;

/// <summary>
///     The one flavor sentence a card shows on its face. A mundane card gets a filler sentence that names no element;
///     a card with a signature gets a sentence built from its strongest element's word for its direction, and the
///     second element's word too when that one is at least half as strong. The choice is seeded like the card's
///     look (the look seed for a mundane card, the signature hash otherwise), so the same card always shows the same
///     sentence and nothing needs saving.
/// </summary>
public static class FlavorText
{
    /// <summary>A second element at least this fraction of the strongest one's magnitude adds its word.</summary>
    public const float SecondElementShare = 0.5f;

    public static IReadOnlyList<string> Filler { get; } =
    [
        "Just a card.",
        "A perfectly ordinary card.",
        "Nothing special. Probably.",
        "It smells faintly of cardboard.",
        "Mint condition, give or take.",
        "Somebody's favourite, once.",
        "Collect them all.",
        "Printed by the thousand.",
        "The ink is barely dry.",
        "One corner has a tiny crease."
    ];

    // One word list per element, the negative direction first. The rows follow Element's order.
    private static readonly string[][][] Words =
    [
        [["airy", "drifting", "wispy"], ["stony", "unyielding", "rock-hard"]],
        [["cold", "icy", "wintry"], ["hot", "burning", "smouldering"]],
        [["chaotic", "unravelling", "restless"], ["orderly", "measured", "precise"]],
        [["dark", "shadowed", "dim"], ["bright", "radiant", "gleaming"]],
        [["timeworn", "ancient", "fleeting"], ["vast", "boundless", "spacious"]],
        [["heavy", "leaden", "ponderous"], ["weightless", "nimble", "feathery"]],
        [["cruel", "baneful", "spiteful"], ["kind", "mending", "gentle"]],
        [["near", "close", "intimate"], ["distant", "faraway", "remote"]]
    ];

    private static readonly string[] OneWordTemplates =
    [
        "Something {0} stirs beneath the ink.",
        "It feels faintly {0} to the touch.",
        "A {0} hum lingers in the paper.",
        "Whoever held it last left it {0}."
    ];

    private static readonly string[] TwoWordTemplates =
    [
        "Something {0} and {1} stirs beneath the ink.",
        "It feels {0}, yet oddly {1}.",
        "A {0} card with a {1} edge.",
        "{0} at heart, {1} at the edges."
    ];

    public static IReadOnlyList<string> WordsFor(Element element, bool positive) =>
        Words[(int)element][positive ? 1 : 0];

    /// <summary>
    ///     The card's sentence. <paramref name="lookSeed" /> chooses a mundane card's filler; a card with a signature
    ///     ignores it, as its look does.
    /// </summary>
    public static string For(CardSignature signature, int? lookSeed)
    {
        var signatureSeed = CardSignatureHash.Of(signature);
        if (!signature.HasMagicalPotential())
        {
            var mundane = RandomSources.Create(lookSeed.HasValue ? unchecked((uint)lookSeed.Value) : signatureSeed);
            return Pick(mundane, Filler);
        }

        var rng = RandomSources.Create(signatureSeed);
        var ranked = Enumerable.Range(0, 8)
            .OrderByDescending(index => MathF.Abs(signature[index]))
            .ThenBy(index => index)
            .ToArray();
        var first = WordOf(rng, signature, ranked[0]);
        var strongest = MathF.Abs(signature[ranked[0]]);
        var secondStrength = MathF.Abs(signature[ranked[1]]);
        if (secondStrength <= 0f || secondStrength < SecondElementShare * strongest)
            return string.Format(Pick(rng, OneWordTemplates), first);

        var second = WordOf(rng, signature, ranked[1]);
        return Capitalised(string.Format(Pick(rng, TwoWordTemplates), first, second));
    }

    private static string WordOf(IRandom rng, CardSignature signature, int index) =>
        Pick(rng, WordsFor((Element)index, signature[index] > 0f));

    private static string Pick(IRandom rng, IReadOnlyList<string> options) =>
        options[rng.RandiRange(0, options.Count - 1)];

    private static string Capitalised(string sentence) =>
        sentence.Length == 0 ? sentence : char.ToUpperInvariant(sentence[0]) + sentence[1..];
}
