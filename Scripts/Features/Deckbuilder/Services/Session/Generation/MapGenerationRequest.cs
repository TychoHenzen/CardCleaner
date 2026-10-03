using System;
using CardCleaner.Scripts.Features.Card.Models;
using Godot;

namespace CardCleaner.Scripts.Features.Deckbuilder.Services.Session.Generation;

/// <summary>
/// Everything one map generation attempt needs, derived once from the seed cards.
/// </summary>
internal sealed class MapGenerationRequest
{
    internal required GenerationRun Run { get; init; }
    internal required RandomNumberGenerator Rng { get; init; }
    internal required ulong Seed { get; init; }
    internal required Vector2I MapSize { get; init; }
    internal required CardSignature[] MapSeeds { get; init; }
    internal required IProgress<float> Progress { get; init; }
}
