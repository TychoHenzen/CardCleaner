using Godot;

namespace CardCleaner.Scripts.Core.Interfaces;

public interface IBlacklightController : IPhysicsComponent
{
    void UpdateBlacklightEffect(ShaderMaterial material);
}