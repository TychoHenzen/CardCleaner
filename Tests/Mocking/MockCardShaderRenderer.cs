using System.Collections.Generic;
using CardCleaner.Scripts.Features.Card.Components;
using Godot;

namespace CardCleaner.Tests.Mocking;

public partial class MockCardShaderRenderer : CardShaderRenderer
{
    public int SetGemEmissionCallCount { get; private set; }

    public List<(int index, Color color, float strength)> LastGemEmission { get; } = new();

    public override void SetGemEmission(int index, Color color, float strength)
    {
        SetGemEmissionCallCount++;
        LastGemEmission.Add((index, color, strength));
    }
}