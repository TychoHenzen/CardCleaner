using System;
using CardCleaner.Scripts.Core.Interfaces;
using CardCleaner.Scripts.Features.Shop.Models;
using Godot;

namespace CardCleaner.Scripts.Features.Shop.Components;

/// <summary>The <c>QueueSlot</c> markers under a checkout counter, where waiting customers stand.</summary>
public sealed class CheckoutQueueSpots(Node3D counter)
{
    private const string SpotPrefix = "QueueSlot";

    private Marker3D[] _spots = Array.Empty<Marker3D>();

    /// <summary>How many spots the counter has.</summary>
    public int Count => _spots.Length;

    /// <summary>Finds the markers under the counter and logs an error when there are none.</summary>
    public void Find()
    {
        _spots = CheckoutCounterLayout.MarkersNamed(counter, SpotPrefix);
        if (_spots.Length == 0)
            ILog.Error($"{counter.Name} has no {SpotPrefix} markers; customers cannot queue.");
    }

    /// <summary>The world position of spot <paramref name="spot" />.</summary>
    public QueueSpot PositionOf(int spot)
    {
        var position = _spots[spot].GlobalPosition;
        return new QueueSpot(position.X, position.Y, position.Z);
    }
}
