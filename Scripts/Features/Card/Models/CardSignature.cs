using System;
using System.Globalization;
using System.Linq;
using CardCleaner.Scripts.Core.Enumeration;
using Newtonsoft.Json;

namespace CardCleaner.Scripts.Features.Card.Models;

public readonly struct CardSignature
{
    private const float ZeroTolerance = 1e-06f;

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

    public CardSignature With(int index, float value)
    {
        if ((uint)index >= 8)
            throw new ArgumentOutOfRangeException(nameof(index));

        var elements = Elements;
        elements[index] = value;
        return new CardSignature(elements);
    }

    public bool HasMagicalPotential() => Elements.Any(element => !IsNearZero(element));

    public float DistanceTo(CardSignature other)
    {
        var sum = 0f;
        for (var index = 0; index < 8; index++)
        {
            var difference = this[index] - other[index];
            sum += difference * difference;
        }

        return MathF.Sqrt(sum);
    }

    public CardSignature Subtract(CardSignature other)
    {
        var elements = new float[8];
        for (var index = 0; index < elements.Length; index++)
            elements[index] = this[index] - other[index];
        return new CardSignature(elements);
    }

    public float GetIntensity(Element element) => MathF.Abs(this[element]);

    public bool IsSignificant(Element element, float threshold = 0.1f) => GetIntensity(element) >= threshold;

    public string ToDebugString() =>
        $"Signature[{string.Join(", ", Elements.Select(element => element.ToString(
            "F2", CultureInfo.InvariantCulture)))}]";

    private static float Clamp(float value) => Math.Clamp(value, -1f, 1f);

    private static bool IsNearZero(float value) => MathF.Abs(value) < ZeroTolerance;
}
