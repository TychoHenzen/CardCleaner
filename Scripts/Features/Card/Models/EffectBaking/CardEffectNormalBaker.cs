using System;

namespace CardCleaner.Scripts.Features.Card.Models.EffectBaking;

/// <summary>
///     Bakes raster art into a bevel map: every colour region gets its own bevel, standing in for Axiom2d's
///     per-shape UVs. Texel channels, in the art's own pixel space (x right, y down, like UV):
///     R, G = the bevel normal's x and y as 0.5 + 0.5 * n; flat is 128. B = bevel strength, 255 on the edge
///     falling to 0 at the bevel width. A = 255 where the art is opaque. z is rebuilt as sqrt(1 - x^2 - y^2).
/// </summary>
public static class CardEffectNormalBaker
{
    /// <summary>How far the normal leans at the edge, as xy length over z (Axiom2d bevel_strength * 0.8).</summary>
    public const float MaxTilt = 0.8f;

    // Bevel width as a fraction of the region's shorter bounding-box side (Axiom2d bevel_width 0.15 in UV).
    private const float BevelWidthFraction = 0.15f;

    private const byte FlatChannel = 128;

    public static CardEffectNormalMap Bake(byte[] rgba, int width, int height)
    {
        var regions = CardEffectRegionSegmenter.Segment(rgba, width, height);
        var texels = new byte[regions.Width * regions.Height * 4];
        for (var i = 0; i < texels.Length; i += 4)
        {
            texels[i] = FlatChannel;
            texels[i + 1] = FlatChannel;
        }

        var bounds = BoundsOf(regions);
        for (var id = 0; id < bounds.Length; id++)
            BevelRegion(regions, id, bounds[id], texels);

        return new CardEffectNormalMap(regions.Width, regions.Height, texels);
    }

    private static RegionBounds[] BoundsOf(CardEffectRegionMap regions)
    {
        var bounds = new RegionBounds[regions.RegionCount];
        Array.Fill(bounds, new RegionBounds(int.MaxValue, int.MaxValue, -1, -1));
        for (var i = 0; i < regions.Labels.Length; i++)
        {
            var id = regions.Labels[i];
            if (id < 0) continue;

            var x = i % regions.Width;
            var y = i / regions.Width;
            var seen = bounds[id];
            bounds[id] = new RegionBounds(
                Math.Min(seen.MinX, x), Math.Min(seen.MinY, y), Math.Max(seen.MaxX, x), Math.Max(seen.MaxY, y));
        }

        return bounds;
    }

    private static void BevelRegion(CardEffectRegionMap regions, int id, RegionBounds bounds, byte[] texels)
    {
        // One outside pixel all round, so the art's own edge bevels like any other region edge.
        var width = bounds.Width + 2;
        var height = bounds.Height + 2;
        var inside = new bool[width * height];
        for (var y = 0; y < bounds.Height; y++)
        for (var x = 0; x < bounds.Width; x++)
        {
            var label = regions.Labels[(bounds.MinY + y) * regions.Width + bounds.MinX + x];
            inside[(y + 1) * width + x + 1] = label == id;
        }

        var field = new CardEffectDistanceField(inside, width, height);
        var bevelWidth = MathF.Max(1f, BevelWidthFraction * MathF.Min(bounds.Width, bounds.Height));
        for (var y = 0; y < bounds.Height; y++)
        for (var x = 0; x < bounds.Width; x++)
        {
            var local = (y + 1) * width + x + 1;
            if (!inside[local]) continue;

            var texel = ((bounds.MinY + y) * regions.Width + bounds.MinX + x) * 4;
            WriteTexel(texels, texel, field, local, bevelWidth);
        }
    }

    private static void WriteTexel(byte[] texels, int at, CardEffectDistanceField field, int local, float bevelWidth)
    {
        // The distance is to the centre of the nearest outside pixel, half a pixel more than to the edge itself.
        var length = field.DistanceAt(local);
        var strength = 1f - Smoothstep((length - 0.5f) / bevelWidth);
        var tilt = strength * MaxTilt;
        var tiltX = field.OffsetXAt(local) / length * tilt;
        var tiltY = field.OffsetYAt(local) / length * tilt;
        var normalLength = MathF.Sqrt(tiltX * tiltX + tiltY * tiltY + 1f);

        texels[at] = Encode(tiltX / normalLength * 0.5f + 0.5f);
        texels[at + 1] = Encode(tiltY / normalLength * 0.5f + 0.5f);
        texels[at + 2] = Encode(strength);
        texels[at + 3] = byte.MaxValue;
    }

    private static float Smoothstep(float t)
    {
        var x = Math.Clamp(t, 0f, 1f);
        return x * x * (3f - 2f * x);
    }

    private static byte Encode(float unit)
    {
        return (byte)(Math.Clamp(unit, 0f, 1f) * 255f + 0.5f);
    }

    /// <summary>The tight pixel bounding box of one region.</summary>
    private readonly record struct RegionBounds(int MinX, int MinY, int MaxX, int MaxY)
    {
        internal int Width => MaxX - MinX + 1;

        internal int Height => MaxY - MinY + 1;
    }
}
