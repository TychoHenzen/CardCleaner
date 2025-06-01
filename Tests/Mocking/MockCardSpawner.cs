using CardCleaner.Scripts.Core.Interfaces;
using Godot;

namespace CardCleaner.Tests.Features;

public partial class MockCardSpawner : Node3D, ICardSpawner
{
    public Node3D GetNode() => this;
}