using CardCleaner.Scripts.Core.Interfaces;
using CardCleaner.Scripts.Features.Card.Models;

namespace CardCleaner.Tests.Mocking;

public class MockCardGenerator : ICardGenerator
{
    public bool GenerateCardRendererCalled { get; private set; }
    public CardSignature? LastSignature { get; private set; }
    
    public void GenerateCardRenderer(CardCleaner.Scripts.Features.Card.Components.CardShaderRenderer renderer, CardSignature signature, CardCleaner.Scripts.Core.Data.CardTemplate template)
    {
        GenerateCardRendererCalled = true;
        LastSignature = signature;
    }
}