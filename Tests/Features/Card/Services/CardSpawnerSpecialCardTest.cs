using System;
using CardCleaner.Scripts.Core.DependencyInjection;
using CardCleaner.Scripts.Core.Interfaces;
using CardCleaner.Scripts.Features.Card.Models;
using CardCleaner.Scripts.Features.Card.Services;
using Godot;
using NSubstitute;

namespace CardCleaner.Tests.Features.Card.Services;

/// <summary>Key 4 spawns a special card, so the backoffice seam can be tested without opening 512 cards.</summary>
[TestSuite]
[RequireGodotRuntime]
public class CardSpawnerSpecialCardTest
{
    private ICardSpawningService _spawning = null!;
    private IInputService _input = null!;
    private CardSpawner _spawner = null!;

    [BeforeTest]
    public void Setup()
    {
        ServiceLocator.ResetForTesting();
        _spawning = Substitute.For<ICardSpawningService>();
        _input = Substitute.For<IInputService>();
        ServiceLocator.Container.RegisterSingleton(_spawning);
        ServiceLocator.Container.RegisterSingleton(_input);
        ServiceLocator.Container.RegisterSingleton(new RandomNumberGenerator { Seed = 99 });
        _spawner = new CardSpawner();
        AddNode(_spawner);
    }

    [AfterTest]
    public static void TearDown()
    {
        ServiceLocator.ResetForTesting();
    }

    [TestCase]
    [TestCategory("Unit")]
    public void SpawnSpecialCardSpawnsExactlyOneCardWithMagicalPotential()
    {
        _spawner.SpawnSpecialCard();

        _spawning.Received(1).SpawnCard(Arg.Is<CardSignature>(signature => signature.HasMagicalPotential()),
            Arg.Any<Transform3D>(), Arg.Any<Node3D>());
    }

    [TestCase]
    [TestCategory("Unit")]
    public void KeyFourIsRegisteredForSpecialCardsAndKeysOneToThreeStillSpawnRandomCards()
    {
        _input.Received(1).RegisterAction(_spawner, "spawn_special", Key.Key4, Arg.Any<Action>());
        _input.Received(1).RegisterAction(_spawner, "spawn_one", Key.Key1, Arg.Any<Action>());
        _input.Received(1).RegisterAction(_spawner, "spawn_ten", Key.Key2, Arg.Any<Action>());
        _input.Received(1).RegisterAction(_spawner, "spawn_hundred", Key.Key3, Arg.Any<Action>());
    }
}
