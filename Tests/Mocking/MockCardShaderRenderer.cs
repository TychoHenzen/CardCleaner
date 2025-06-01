using CardCleaner.Scripts.Features.Card.Components;
using Godot;

namespace CardCleaner.Tests.Features;

public partial class MockCardShaderRenderer : CardShaderRenderer
{
    public int SetGemEmissionCallCount { get; private set; }
    public (int index, Color color, float strength) LastGemEmission { get; private set; }
    
    public new void SetGemEmission(int index, Color color, float strength)
    {
        SetGemEmissionCallCount++;
        LastGemEmission = (index, color, strength);
    }
}