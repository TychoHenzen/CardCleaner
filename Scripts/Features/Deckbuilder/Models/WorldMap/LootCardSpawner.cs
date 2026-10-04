using System.Collections.Generic;
using CardCleaner.Scripts.Core.DependencyInjection;
using CardCleaner.Scripts.Core.Interfaces;
using CardCleaner.Scripts.Features.Card.Models;
using Godot;

namespace CardCleaner.Scripts.Features.Deckbuilder.Models;

/// <summary>
/// Spawns the loot cards of a finished session in a row above the card spawner.
/// </summary>
internal static class LootCardSpawner
{
    internal static void Spawn(List<CardSignature> lootSignatures)
    {
        ServiceLocator.Get<ICardSpawningService>(spawningService =>
        {
            for (var i = 0; i < lootSignatures.Count; i++)
            {
                var spawnPos = new Vector3(i * 0.5f, 1.5f, 2f);

                ServiceLocator.Get<ICardSpawner>(spawner =>
                {
                    var spawnTransform = Transform3D.Identity;
                    spawnTransform.Origin = spawnPos;
                    spawningService.SpawnCard(lootSignatures[i], spawnTransform, spawner.GetNode());
                });
            }

            ILog.Print($"Spawned {lootSignatures.Count} loot cards!");
        });
    }
}
