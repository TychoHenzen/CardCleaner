using CardCleaner.Scripts.Core.Interfaces;
using Godot;

namespace CardCleaner.Tests.Features.Card.Controllers.CardControllerScenarios;

// Mock components for testing
public partial class MockCardComponent : Node, ICardComponent
{
    public bool SetupCalled { get; private set; }
    public Node? CardRoot { get; private set; }

    public void Setup(Node cardRoot)
    {
        SetupCalled = true;
        CardRoot = cardRoot;
    }
}
