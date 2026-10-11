using CardCleaner.Scripts.Features.Card.Components;
using CardCleaner.Scripts.Features.Card.Models;

namespace CardCleaner.Scripts.Features.Card.Services;

public interface ICardGenerator
{
    void GenerateCardRenderer(CardShaderRenderer renderer, CardSignature signature, CardTemplate cardTemplate);
}