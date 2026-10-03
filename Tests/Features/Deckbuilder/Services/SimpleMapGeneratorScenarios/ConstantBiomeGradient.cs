using CardCleaner.Scripts.Features.Card.Models;
using CardCleaner.Scripts.Features.Worldgen;
using Godot;

namespace CardCleaner.Tests.Features.Deckbuilder.Services.SimpleMapGeneratorScenarios;

internal sealed partial class ConstantBiomeGradient : BaselineGradient
{
    private readonly CardSignature _signature;

    public ConstantBiomeGradient(CardSignature signature)
    {
        _signature = signature;
    }

    public override CardSignature GetSignatureAt(Vector2I position, Vector2I mapSize) => _signature;
}
