using CardCleaner.Scripts.Features.Card.Components;
using CardCleaner.Scripts.Features.Card.Models;

namespace CardCleaner.Scripts.Features.Card.Services;

public interface ICardGenerator
{
    /// <summary>
    ///     <paramref name="lookSeed" /> is the look of a card with no magical potential; it is ignored for a card
    ///     that has some.
    /// </summary>
    void GenerateCardRenderer(
        CardShaderRenderer renderer,
        CardSignature signature,
        int? lookSeed,
        CardTemplate cardTemplate);
}