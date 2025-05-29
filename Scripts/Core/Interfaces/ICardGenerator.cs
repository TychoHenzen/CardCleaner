using Godot;

namespace CardCleaner.Scripts.Interfaces;


public interface ICardGenerator
{
    void GenerateCardRenderer(CardShaderRenderer renderer, CardSignature signature);
    void Verify();
}