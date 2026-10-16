using System.Collections.Generic;
using CardCleaner.Scripts.Core.Interfaces;
using CardCleaner.Scripts.Features.Card.Models;
using CardCleaner.Scripts.Features.Deckbuilder.Services.Session;

namespace CardCleaner.Scripts.Features.Deckbuilder.Services;

/// <summary>Builds the per-session exploration and combat objects; holds no state.</summary>
public static class SessionFactories
{
    public static IExplorationAI CreateExplorationAI(
        IMapData mapData,
        int? startCell = null,
        IVisibilityChecker? visibilityChecker = null) =>
        new ExplorationAI(mapData, startCell, visibilityChecker);

    public static ICombatSystem CreateCombat(List<CardSignature> playerAbilities, CardSignature enemySeed, IRandom rng) =>
        new SimpleCombatSystem(playerAbilities, enemySeed, rng);

    internal static ISessionWorld CreateWorld() => new SessionWorld();

    internal static IExplorationSession CreateExploration(ISessionWorld world) => new ExplorationSession(world);
}
