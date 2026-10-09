using CardCleaner.Scripts.Features.Card.Components;

namespace CardCleaner.Scripts.Core.Interfaces;

public interface ICardGenerator
{
    void GenerateCardRenderer(CardShaderRenderer renderer, Features.Card.Models.CardSignature signature,
        Features.Card.Models.CardTemplate cardTemplate);
}