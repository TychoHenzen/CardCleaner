using Godot;

namespace CardCleaner.Scripts.Interfaces;

public interface IBlacklightController : IPhysicsComponent
{
    float CalculateExposure(Vector3 cardPosition);
    void UpdateBlacklightEffect(ShaderMaterial material);
}