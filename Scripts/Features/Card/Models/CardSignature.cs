using System;
using System.Globalization;
using System.Linq;
using CardCleaner.Scripts.Core.Enumeration;
using CardCleaner.Scripts.Core.Interfaces;
using CardCleaner.Scripts.Core.Utilities;
using Godot;
using Newtonsoft.Json;

namespace CardCleaner.Scripts.Features.Card.Models;

public readonly struct CardSignature
{
    public CardSignature(float[] elements)
    {
        if (elements.Length != 8)
            throw new ArgumentException("Elements array must have exactly 8 values");

        Solidum = Clamp(elements[0]);
        Febris = Clamp(elements[1]);
        Ordinem = Clamp(elements[2]);
        Lumines = Clamp(elements[3]);
        Varias = Clamp(elements[4]);
        Inertiae = Clamp(elements[5]);
        Subsidium = Clamp(elements[6]);
        Spatium = Clamp(elements[7]);
    }

    public CardSignature(
        float solidum,
        float febris,
        float ordinem,
        float lumines,
        float varias,
        float inertiae,
        float subsidium,
        float spatium)
    {
        Solidum = Clamp(solidum);
        Febris = Clamp(febris);
        Ordinem = Clamp(ordinem);
        Lumines = Clamp(lumines);
        Varias = Clamp(varias);
        Inertiae = Clamp(inertiae);
        Subsidium = Clamp(subsidium);
        Spatium = Clamp(spatium);
    }

    public float Solidum { get; }
    public float Febris { get; }
    public float Ordinem { get; }
    public float Lumines { get; }
    public float Varias { get; }
    public float Inertiae { get; }
    public float Subsidium { get; }
    public float Spatium { get; }

    public float this[Element element] => this[(int)element];

    public float this[int index] => index switch
    {
        0 => Solidum,
        1 => Febris,
        2 => Ordinem,
        3 => Lumines,
        4 => Varias,
        5 => Inertiae,
        6 => Subsidium,
        7 => Spatium,
        _ => throw new ArgumentOutOfRangeException(nameof(index))
    };

    [JsonProperty("elements")]
    public float[] Elements => [Solidum, Febris, Ordinem, Lumines, Varias, Inertiae, Subsidium, Spatium];

    public CardSignature With(Element element, float value) => With((int)element, value);

    public CardSignature With(int index, float value) => index switch
    {
        0 => new(value, Febris, Ordinem, Lumines, Varias, Inertiae, Subsidium, Spatium),
        1 => new(Solidum, value, Ordinem, Lumines, Varias, Inertiae, Subsidium, Spatium),
        2 => new(Solidum, Febris, value, Lumines, Varias, Inertiae, Subsidium, Spatium),
        3 => new(Solidum, Febris, Ordinem, value, Varias, Inertiae, Subsidium, Spatium),
        4 => new(Solidum, Febris, Ordinem, Lumines, value, Inertiae, Subsidium, Spatium),
        5 => new(Solidum, Febris, Ordinem, Lumines, Varias, value, Subsidium, Spatium),
        6 => new(Solidum, Febris, Ordinem, Lumines, Varias, Inertiae, value, Spatium),
        7 => new(Solidum, Febris, Ordinem, Lumines, Varias, Inertiae, Subsidium, value),
        _ => throw new ArgumentOutOfRangeException(nameof(index))
    };

    public static CardSignature Random(IRandom random)
    {
        var elements = new float[8];
        for (var index = 0; index < elements.Length; index++)
            elements[index] = random.RandfRange(-1f, 1f);
        return new CardSignature(elements);
    }

    public static CardSignature RandomSpecial(IRandom random)
    {
        var signature = Random(random);
        while (!signature.HasMagicalPotential())
            signature = Random(random);
        return signature;
    }

    public bool HasMagicalPotential() => Elements.Any(element => !Mathf.IsZeroApprox(element));

    public float DistanceTo(CardSignature other)
    {
        var sum = 0f;
        for (var index = 0; index < 8; index++)
        {
            var difference = this[index] - other[index];
            sum += difference * difference;
        }

        return Mathf.Sqrt(sum);
    }

    public CardSignature Subtract(CardSignature other)
    {
        var elements = new float[8];
        for (var index = 0; index < elements.Length; index++)
            elements[index] = this[index] - other[index];
        return new CardSignature(elements);
    }

    public Aspect GetDominantAspect(Element element) => this[element] >= 0 ? element.Positive() : element.Negative();

    public float GetIntensity(Element element) => Mathf.Abs(this[element]);

    public bool IsSignificant(Element element, float threshold = 0.1f) => GetIntensity(element) >= threshold;

    public string ToDebugString() =>
        $"Signature[{string.Join(", ", Elements.Select(element => element.ToString("F2", CultureInfo.InvariantCulture)))}]";

    private static float Clamp(float value) => Mathf.Clamp(value, -1f, 1f);
}
