using CardCleaner.Scripts.Features.Card.Components;
using Godot;

namespace CardCleaner.Scripts.Interfaces;


public interface ICardGenerator
{
    void GenerateCardRenderer(CardShaderRenderer renderer, CardSignature signature, CardTemplate template);
}