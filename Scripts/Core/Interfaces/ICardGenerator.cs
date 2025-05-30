using Godot;

namespace CardCleaner.Scripts.Interfaces;


public interface ICardGenerator
{
    void GenerateCardRenderer(Features.Card.Components.CardShaderRenderer renderer, CardSignature signature);
    void Verify();
}