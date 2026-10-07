using System;
using System.Collections.Generic;

namespace CardCleaner.Scripts.Features.Card.Models;

/// <summary>The tight pixel bounding box of one region in a <see cref="CardEffectRegionMap" />.</summary>
internal readonly record struct CardEffectRegionBounds(int Id, int MinX, int MinY, int MaxX, int MaxY)
{
    public int Width => MaxX - MinX + 1;

    public int Height => MaxY - MinY + 1;

    /// <summary>The bounds of every region, indexed by region id.</summary>
    public static CardEffectRegionBounds[] Of(CardEffectRegionMap map)
    {
        var minX = new int[map.RegionCount];
        var minY = new int[map.RegionCount];
        var maxX = new int[map.RegionCount];
        var maxY = new int[map.RegionCount];
        Array.Fill(minX, int.MaxValue);
        Array.Fill(minY, int.MaxValue);
        Array.Fill(maxX, -1);
        Array.Fill(maxY, -1);

        for (var i = 0; i < map.Labels.Length; i++)
        {
            var id = map.Labels[i];
            if (id < 0) continue;

            var x = i % map.Width;
            var y = i / map.Width;
            minX[id] = Math.Min(minX[id], x);
            minY[id] = Math.Min(minY[id], y);
            maxX[id] = Math.Max(maxX[id], x);
            maxY[id] = Math.Max(maxY[id], y);
        }

        var bounds = new List<CardEffectRegionBounds>(map.RegionCount);
        for (var id = 0; id < map.RegionCount; id++)
            bounds.Add(new CardEffectRegionBounds(id, minX[id], minY[id], maxX[id], maxY[id]));

        return bounds.ToArray();
    }
}
