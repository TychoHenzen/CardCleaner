using Godot;

namespace CardCleaner.Scripts.Interfaces;

public interface ICardGenerator
{
    void RandomizeCardRenderer(CardShaderRenderer cardInstance);
    void Verify();
}