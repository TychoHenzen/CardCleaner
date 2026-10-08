using System.Collections.Generic;

namespace CardCleaner.Scripts.Features.Card.Models;

/// <summary>
///     Axiom2d <c>segment.rs</c>: grows a region from each unvisited opaque pixel, taking neighbours within
///     <see cref="CardEffectRegionSegmenter.ColorThreshold" /> of the region's running average colour, so a
///     gradient stays one region. The push order (left, right, up, down) decides which pixels join before the
///     average moves, so it is part of the result.
/// </summary>
internal sealed class CardEffectRegionFlood
{
    private const float ByteToUnit = 1f / 255f;

    private readonly byte[] _rgba;
    private readonly int _width;
    private readonly int _height;
    private readonly bool[] _visited;
    private readonly Stack<int> _pending = new();

    private CardEffectRegionFlood(byte[] rgba, int width, int height)
    {
        _rgba = rgba;
        _width = width;
        _height = height;
        _visited = new bool[width * height];
    }

    public static List<CardEffectRegion> Run(byte[] rgba, int width, int height)
    {
        return new CardEffectRegionFlood(rgba, width, height).FindRegions();
    }

    private List<CardEffectRegion> FindRegions()
    {
        var regions = new List<CardEffectRegion>();
        for (var start = 0; start < _visited.Length; start++)
        {
            if (_visited[start]) continue;
            if (IsTransparent(start))
            {
                _visited[start] = true;
                continue;
            }

            regions.Add(Grow(start));
        }

        return regions;
    }

    private CardEffectRegion Grow(int start)
    {
        const float limit = CardEffectRegionSegmenter.ColorThreshold * CardEffectRegionSegmenter.ColorThreshold;
        var region = new CardEffectRegion();
        var seed = ColorAt(start);
        _pending.Push(start);

        while (_pending.Count > 0)
        {
            var index = _pending.Pop();
            if (_visited[index] || IsTransparent(index)) continue;

            var color = ColorAt(index);
            var reference = region.Pixels.Count > 0 ? region.RunningAverage : seed;
            if (color.DistanceSquaredTo(reference) > limit) continue;

            _visited[index] = true;
            region.Add(index, color);
            PushNeighbours(index);
        }

        region.Color = region.RunningAverage;
        return region;
    }

    private void PushNeighbours(int index)
    {
        var x = index % _width;
        var y = index / _width;
        if (x > 0) _pending.Push(index - 1);
        if (x + 1 < _width) _pending.Push(index + 1);
        if (y > 0) _pending.Push(index - _width);
        if (y + 1 < _height) _pending.Push(index + _width);
    }

    private bool IsTransparent(int index)
    {
        return _rgba[index * 4 + 3] < CardEffectRegionSegmenter.AlphaThreshold;
    }

    private CardEffectRgb ColorAt(int index)
    {
        var i = index * 4;
        return new CardEffectRgb(_rgba[i] * ByteToUnit, _rgba[i + 1] * ByteToUnit, _rgba[i + 2] * ByteToUnit);
    }
}
