using System;

namespace CardCleaner.Tests.Features.Card.Models;

/// <summary>Builds RGBA art buffers for the card effect tests; not a suite.</summary>
public static class CardEffectArtPainter
{
    public static byte[] Paint(int width, int height, Func<int, int, (byte R, byte G, byte B, byte A)> pixelAt)
    {
        var rgba = new byte[width * height * 4];
        for (var y = 0; y < height; y++)
        for (var x = 0; x < width; x++)
        {
            var (r, g, b, a) = pixelAt(x, y);
            var i = (y * width + x) * 4;
            rgba[i] = r;
            rgba[i + 1] = g;
            rgba[i + 2] = b;
            rgba[i + 3] = a;
        }

        return rgba;
    }
}
