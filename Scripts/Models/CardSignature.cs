using System;
using System.Linq;
using Godot;

namespace CardCleaner.Scripts;

[Tool]
[GlobalClass]
public partial class CardSignature : Resource
{
    private float[] _elements = new float[8];

    [Export] public float Solidum { get => _elements[0]; set => _elements[0] = Mathf.Clamp(value, -1f, 1f); }
    [Export] public float Febris { get => _elements[1]; set => _elements[1] = Mathf.Clamp(value, -1f, 1f); }
    [Export] public float Ordinem { get => _elements[2]; set => _elements[2] = Mathf.Clamp(value, -1f, 1f); }
    [Export] public float Lumines { get => _elements[3]; set => _elements[3] = Mathf.Clamp(value, -1f, 1f); }
    [Export] public float Varias { get => _elements[4]; set => _elements[4] = Mathf.Clamp(value, -1f, 1f); }
    [Export] public float Inertiae { get => _elements[5]; set => _elements[5] = Mathf.Clamp(value, -1f, 1f); }
    [Export] public float Subsidium { get => _elements[6]; set => _elements[6] = Mathf.Clamp(value, -1f, 1f); }
    [Export] public float Spatium { get => _elements[7]; set => _elements[7] = Mathf.Clamp(value, -1f, 1f); }

    public float this[Element element]
    {
        get => _elements[(int)element];
        set => _elements[(int)element] = Mathf.Clamp(value, -1f, 1f);
    }

    public float this[int index]
    {
        get => _elements[index];
        set => _elements[index] = Mathf.Clamp(value, -1f, 1f);
    }

    public float[] Elements => (float[])_elements.Clone();

    public CardSignature()
    {
        // Initialize with all zeros
    }

    public CardSignature(float[] elements)
    {
        if (elements.Length != 8)
            throw new ArgumentException("Elements array must have exactly 8 values");
        
        for (int i = 0; i < 8; i++)
            _elements[i] = Mathf.Clamp(elements[i], -1f, 1f);
    }

    public static CardSignature Random(RandomNumberGenerator rng)
    {
        var signature = new CardSignature();
        for (int i = 0; i < 8; i++)
            signature[i] = rng.RandfRange(-1f, 1f);
        return signature;
    }

    public float DistanceTo(CardSignature other)
    {
        float sum = 0f;
        for (int i = 0; i < 8; i++)
        {
            float diff = _elements[i] - other._elements[i];
            sum += diff * diff;
        }
        return Mathf.Sqrt(sum);
    }

    public CardSignature Subtract(CardSignature other)
    {
        var result = new CardSignature();
        for (int i = 0; i < 8; i++)
            result[i] = _elements[i] - other._elements[i];
        return result;
    }

    public Aspect GetDominantAspect(Element element)
    {
        float value = this[element];
        return value >= 0 ? element.Positive() : element.Negative();
    }

    public float GetIntensity(Element element)
    {
        return Mathf.Abs(this[element]);
    }

    public bool IsSignificant(Element element, float threshold = 0.1f)
    {
        return GetIntensity(element) >= threshold;
    }

    public override string ToString()
    {
        return $"Signature[{string.Join(", ", _elements.Select(e => e.ToString("F2")))}]";
    }
}