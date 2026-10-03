using CardCleaner.Scripts.Core.Interfaces;
using Godot;

namespace CardCleaner.Tests.Features.Card.Controllers.CardControllerScenarios;

public partial class MockPhysicsComponent : Node, IPhysicsComponent
{
    public bool SetupCalled { get; private set; }
    public Node? CardRoot { get; private set; }
    public bool IntegrateForcesWasCalled { get; private set; }
    public bool PhysicsProcessWasCalled { get; private set; }

    public void Setup(Node cardRoot)
    {
        SetupCalled = true;
        CardRoot = cardRoot;
    }

    public void IntegrateForces(PhysicsDirectBodyState3D state)
    {
        IntegrateForcesWasCalled = true;
    }

    public void PhysicsProcess(double delta)
    {
        PhysicsProcessWasCalled = true;
    }
}