using Godot;

namespace CardCleaner.Scripts.Interfaces;

public interface IRenderComponent : ICardComponent
{
    void Bake();
}