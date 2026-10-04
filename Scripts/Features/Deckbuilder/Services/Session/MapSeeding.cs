using System;
using System.Collections.Generic;
using CardCleaner.Scripts.Features.Card.Models;
using Godot;

namespace CardCleaner.Scripts.Features.Deckbuilder.Services.Session;

/// <summary>
/// Derives deterministic map parameters from the seed cards.
/// </summary>
internal static class MapSeeding
{
    private const int BaseMapSize = 50;
    private const int MapSizeVariation = 25;

    /// <summary>
    /// Computes a deterministic seed from a list of card signatures.
    /// Same cards in same order always produce the same seed.
    /// </summary>
    internal static ulong ComputeSeedFromCards(IReadOnlyList<CardSignature> cards)
    {
        var hash = 17UL;
        foreach (var card in cards)
        {
            for (var i = 0; i < 8; i++)
            {
                // Use BitConverter for deterministic float to bits conversion
                hash = hash * 31 + BitConverter.ToUInt32(BitConverter.GetBytes(card[i]), 0);
            }
        }
        return hash;
    }

    /// <summary>
    /// Larger maps for more complex signatures.
    /// </summary>
    internal static Vector2I CalculateMapSize(CardSignature signature)
    {
        var complexity = 0f;
        for (var i = 0; i < 8; i++) complexity += Mathf.Abs(signature[i]);
        complexity /= 8f;

        var size = BaseMapSize + Mathf.RoundToInt(complexity * MapSizeVariation);
        return new Vector2I(size, size);
    }
}
