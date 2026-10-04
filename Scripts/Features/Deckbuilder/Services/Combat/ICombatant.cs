namespace CardCleaner.Scripts.Features.Deckbuilder.Services.Combat;

/// <summary>
/// Interface for combat participants that can take damage and heal.
/// </summary>
public interface ICombatant
{
    string Name { get; }
    int Health { get; }
    int MaxHealth { get; }
    bool IsAlive { get; }

    void TakeDamage(int damage);
    void Heal(int amount);
}
