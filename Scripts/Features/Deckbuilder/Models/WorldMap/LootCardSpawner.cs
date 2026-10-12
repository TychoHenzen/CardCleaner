using System.Collections.Generic;
using CardCleaner.Scripts.Core.DependencyInjection;
using CardCleaner.Scripts.Core.Interfaces;
using CardCleaner.Scripts.Features.Card.Models;
using Godot;

namespace CardCleaner.Scripts.Features.Deckbuilder.Models;

/// <summary>
/// Spawns the loot cards of a finished session in a row that starts at the screen's loot point and runs along
/// that point's X axis.
/// </summary>
internal static class LootCardSpawner
{
    private const float CardSpacing = 0.5f;

    internal static void Spawn(List<CardSignature> lootSignatures, Transform3D lootPoint)
    {
        var rowDirection = lootPoint.Basis.Orthonormalized().X;

        ServiceLocator.Get<ICardSpawningService>(spawningService =>
            ServiceLocator.Get<ICardSpawner>(spawner =>
            {
                for (var i = 0; i < lootSignatures.Count; i++)
                {
                    var spawnTransform = Transform3D.Identity;
                    spawnTransform.Origin = lootPoint.Origin + rowDirection * (i * CardSpacing);
                    spawningService.SpawnCard(lootSignatures[i], spawnTransform, spawner.GetNode());
                }

                ILog.Print($"Spawned {lootSignatures.Count} loot cards!");
            }));
    }
}
