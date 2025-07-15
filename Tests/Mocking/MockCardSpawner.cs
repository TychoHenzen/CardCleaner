using CardCleaner.Scripts.Core.Interfaces;
using Godot;

namespace CardCleaner.Tests.Mocking;

public partial class MockCardSpawner : Node3D, ICardSpawner
{
    public Node3D GetNode()
    {
        return this;
    }
}