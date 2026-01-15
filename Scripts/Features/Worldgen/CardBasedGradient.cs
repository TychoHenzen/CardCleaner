using CardCleaner.Scripts.Features.Card.Models;
using Godot;

namespace CardCleaner.Scripts.Features.Worldgen;

/// <summary>
/// Grid-based gradient that uses CardGradientCore for signature generation.
/// Provides the BaselineGradient interface for grid-based map systems.
/// </summary>
[Tool]
[GlobalClass]
public partial class CardBasedGradient : BaselineGradient
{
    private CardSignature[] _inputCards = [];
    private RandomNumberGenerator _rng = new();
    private CardGradientCore? _core;

    public CardBasedGradient()
    {
    }

    public CardBasedGradient(CardSignature[] inputCards, RandomNumberGenerator rng)
    {
        _inputCards = inputCards;
        _rng = rng;
        _core = new CardGradientCore(inputCards, rng);
    }

    public override CardSignature GetSignatureAt(Vector2I position, Vector2I mapSize)
    {
        _core ??= new CardGradientCore(_inputCards, _rng);

        if (mapSize.X == 0 || mapSize.Y == 0)
            return new CardSignature();

        var normalizedX = (float)position.X / mapSize.X;
        var normalizedY = (float)position.Y / mapSize.Y;

        return _core.GetSignatureAtNormalized(normalizedX, normalizedY);
    }

    public void SetInputCards(CardSignature[] cards)
    {
        _inputCards = cards;
        _core = new CardGradientCore(cards, _rng);
    }
}
