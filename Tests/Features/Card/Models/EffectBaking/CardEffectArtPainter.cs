using System;
using Godot;

namespace CardCleaner.Tests.Features.Card.Models.EffectBaking;

/// <summary>Builds RGBA art buffers for the card effect tests; not a suite.</summary>
public static class CardEffectArtPainter
{
    public static byte[] Paint(int width, int height, Func<int, int, Color> colorAt)
    {
        var rgba = new byte[width * height * 4];
        for (var y = 0; y < height; y++)
        for (var x = 0; x < width; x++)
        {
            var color = colorAt(x, y);
            var i = (y * width + x) * 4;
            rgba[i] = (byte)color.R8;
            rgba[i + 1] = (byte)color.G8;
            rgba[i + 2] = (byte)color.B8;
            rgba[i + 3] = (byte)color.A8;
        }

        return rgba;
    }

    public static byte[] Fill(int width, int height, Color color)
    {
        return Paint(width, height, (_, _) => color);
    }

    /// <summary>The left half (x below half the width) in one colour, the right half in another.</summary>
    public static byte[] TwoHalves(int width, int height, Color left, Color right)
    {
        return Paint(width, height, (x, _) => x < width / 2 ? left : right);
    }
}
