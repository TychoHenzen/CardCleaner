using System;
using System.Collections.Generic;
using CardCleaner.Scripts.Core.DependencyInjection;
using CardCleaner.Scripts.Core.Interfaces;
using CardCleaner.Scripts.Features.Card.Models;
using CardCleaner.Scripts.Features.Card.Services;
using Godot;
using NSubstitute;

namespace CardCleaner.Tests.Features.Card.Services;

/// <summary>
///     Key 4 spawns a special card, so the backoffice seam can be tested without opening 512 cards. A scene that
///     spawns its own cards (the effect comparison) switches the keys off.
/// </summary>
[TestSuite]
[RequireGodotRuntime]
public class CardSpawnerSpecialCardTest
{
    private ICardSpawningService _spawning = null!;
    private IInputService _input = null!;
    private CardSpawner _spawner = null!;
    private Dictionary<(string, Key), Action> _callbacks = null!;

    [BeforeTest]
    public void Setup()
    {
        ServiceLocator.ResetForTesting();
        _spawning = Substitute.For<ICardSpawningService>();
        _input = Substitute.For<IInputService>();
        _callbacks = new Dictionary<(string, Key), Action>();
        // Installed before the spawner enters the tree, so every registration's callback is kept as it is made.
        _input.When(input => input.RegisterAction(
                Arg.Any<object>(), Arg.Any<string>(), Arg.Any<Key>(), Arg.Any<Action>()))
            .Do(call => _callbacks[(call.ArgAt<string>(1), call.ArgAt<Key>(2))] = call.ArgAt<Action>(3));
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
    public void KeyFourSpawnsExactlyOneSpecialCard()
    {
        var spawnSpecial = RegisteredCallback("spawn_special", Key.Key4);

        spawnSpecial();
        // Random cards the key queues spawn only in _Process, so run a frame before checking none spawned.
        _spawner._Process(0);

        _spawning.Received(1).SpawnCard(Arg.Is<CardSignature>(signature => signature.HasMagicalPotential()),
            Arg.Any<Transform3D>(), Arg.Any<Node3D>());
        _spawning.Received(1).SpawnCard(Arg.Any<CardSignature>(), Arg.Any<Transform3D>(), Arg.Any<Node3D>());
        _spawning.DidNotReceive().SpawnRandomCard(Arg.Any<Transform3D>(), Arg.Any<Node3D>());
    }

    [TestCase]
    [TestCategory("Unit")]
    public void KeyOneSpawnsExactlyOneRandomCard()
    {
        RegisteredCallback("spawn_one", Key.Key1)();

        AssertRandomCardsSpawnedAfterDraining(1);
    }

    [TestCase]
    [TestCategory("Unit")]
    public void KeyTwoSpawnsExactlyTenRandomCards()
    {
        RegisteredCallback("spawn_ten", Key.Key2)();

        AssertRandomCardsSpawnedAfterDraining(10);
    }

    [TestCase]
    [TestCategory("Unit")]
    public void KeyThreeSpawnsExactlyOneHundredRandomCards()
    {
        RegisteredCallback("spawn_hundred", Key.Key3)();

        AssertRandomCardsSpawnedAfterDraining(100);
    }

    /// <summary>The action the spawner registered under this name and key, as captured by the hook in Setup.</summary>
    private Action RegisteredCallback(string actionName, Key key)
    {
        _input.Received(1).RegisterAction(_spawner, actionName, key, Arg.Any<Action>());
        AssertBool(_callbacks.TryGetValue((actionName, key), out var callback)).IsTrue();
        return callback!;
    }

    /// <summary>Runs one frame past the expected count, so a card queued beyond it is spawned and caught.</summary>
    private void AssertRandomCardsSpawnedAfterDraining(int expected)
    {
        for (var frame = 0; frame <= expected; frame++)
            _spawner._Process(0);

        _spawning.Received(expected).SpawnRandomCard(Arg.Any<Transform3D>(), Arg.Any<Node3D>());
        _spawning.DidNotReceive().SpawnCard(Arg.Any<CardSignature>(), Arg.Any<Transform3D>(), Arg.Any<Node3D>());
    }

    [TestCase]
    [TestCategory("Unit")]
    public void ASpawnerWithItsKeysSwitchedOffRegistersNone()
    {
        var quiet = new CardSpawner { SpawnKeys = false };

        AddNode(quiet);

        _input.DidNotReceive().RegisterAction(quiet, Arg.Any<string>(), Arg.Any<Key>(), Arg.Any<Action>());
    }
}
