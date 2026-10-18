using System.Collections.Generic;
using CardCleaner.Scripts.Features.Card.Models;

namespace CardCleaner.Scripts.Core.Interfaces;

/// <summary>
/// Creates the combat system of one fight. Stateless: each call returns a new, independent instance.
/// </summary>
public interface ICombatSystemFactory
{
    ICombatSystem Create(List<CardSignature> playerAbilities, CardSignature enemySeed, IRandom rng);
}
