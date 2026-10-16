using System.Collections.Generic;
using CardCleaner.Scripts.Core.Interfaces;
using CardCleaner.Scripts.Features.Card.Models;

namespace CardCleaner.Scripts.Features.Deckbuilder.Services;

public static class CombatSystems
{
    public static ICombatSystem Create(List<CardSignature> playerAbilities, CardSignature enemySeed, IRandom rng) =>
        new SimpleCombatSystem(playerAbilities, enemySeed, rng);
}
