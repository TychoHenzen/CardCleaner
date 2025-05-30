using Godot;

namespace CardCleaner.Scripts.Core.Interfaces;

public interface ICardComponent
{
    void Setup(RigidBody3D cardRoot);
}