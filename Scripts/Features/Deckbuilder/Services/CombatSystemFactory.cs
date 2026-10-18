using System.Collections.Generic;
using CardCleaner.Scripts.Core.DependencyInjection;
using CardCleaner.Scripts.Core.Interfaces;
using CardCleaner.Scripts.Features.Card.Models;

namespace CardCleaner.Scripts.Features.Deckbuilder.Services;

/// <summary>
/// Builds the combat system of one fight; the container finds it by its [Service] attribute.
/// </summary>
[Service(typeof(ICombatSystemFactory))]
internal sealed class CombatSystemFactory : ICombatSystemFactory
{
    public ICombatSystem Create(List<CardSignature> playerAbilities, CardSignature enemySeed, IRandom rng) =>
        new SimpleCombatSystem(playerAbilities, enemySeed, rng);
}
