namespace CardCleaner.Scripts.Features.Card.Services;

public enum ModifierType
{
    Power, // Affects damage/effectiveness
    Cost, // Affects energy/mana cost
    Duration, // Affects how long effects last
    Range, // Affects area of effect/targeting range
    Healing, // Adds healing effects
    Speed, // Affects attack speed or movement
    Defense, // Adds defensive properties
    Special // Custom effect modifications
}
