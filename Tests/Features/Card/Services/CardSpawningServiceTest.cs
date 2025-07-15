using System;
using CardCleaner.Scripts.Core.DependencyInjection;
using CardCleaner.Scripts.Core.Interfaces;
using CardCleaner.Scripts.Features.Card.Controllers;
using CardCleaner.Scripts.Features.Card.Models;
using CardCleaner.Scripts.Features.Card.Services;
using GdUnit4;
using Godot;
using NSubstitute;

namespace CardCleaner.Tests.Features.Card.Services;

// Mock implementations for testing

[TestSuite]
[RequireGodotRuntime]
public class CardSpawningServiceTest
{
    private CardSpawningService _service = null!;
    private ICardGenerator _mockGenerator = null!;
    private RandomNumberGenerator _rng = null!;

    private Node3D _cardParent = null!;
    private PackedScene _mockCardScene = null!;


    [BeforeTest]
    public void Setup()
    {
        // Create root node for scene tree context
        _cardParent = new Node3D();
        Assertions.AddNode(_cardParent);

        // Create dependencies using NSubstitute instead of custom mock
        _mockGenerator = Substitute.For<ICardGenerator>();
        _rng = new RandomNumberGenerator();
        _rng.Seed = 42; // Deterministic for testing

        // Register dependencies in ServiceLocator BEFORE creating the service
        ServiceLocator.Container.RegisterSingleton(_mockGenerator);
        ServiceLocator.Container.RegisterSingleton(_rng);

        // Create the service and add to scene tree
        _service = new CardSpawningService();
        Assertions.AddNode(_service);

        // Set up the card scene
        _mockCardScene = GD.Load<PackedScene>("res://Scenes/CardShader.tscn");
        _service.CardScene = _mockCardScene;

        // Force _Ready() to be called and wait for dependency resolution
        _service._Ready();
    }


    [AfterTest]
    public void TearDown()
    {
        ServiceLocator.ResetForTesting();
    }

    [TestCase]
    public void TestGetRandomOffset()
    {
        var offsetRange = new Vector3(1.0f, 2.0f, 3.0f);

        var offset = _service.GetRandomOffset(offsetRange);

        // Check that offset is within expected bounds
        Assertions.AssertThat(Mathf.Abs(offset.X)).IsLessEqual(offsetRange.X);
        Assertions.AssertThat(Mathf.Abs(offset.Y)).IsLessEqual(offsetRange.Y);
        Assertions.AssertThat(Mathf.Abs(offset.Z)).IsLessEqual(offsetRange.Z);
    }

    [TestCase]
    public void TestGetRandomOffsetDeterministic()
    {
        var offsetRange = new Vector3(1.0f, 1.0f, 1.0f);

        // Reset RNG to same seed
        _rng.Seed = 42;
        var offset1 = _service.GetRandomOffset(offsetRange);

        _rng.Seed = 42;
        var offset2 = _service.GetRandomOffset(offsetRange);

        Assertions.AssertThat(offset1).IsEqual(offset2);
    }

    [TestCase]
    [TestCategory("Unit")]
    public void TestSpawnRandomCardCreatesCard()
    {
        // Arrange
        var spawnTransform = Transform3D.Identity;
        spawnTransform.Origin = new Vector3(1, 2, 3);

        // Act
        var spawnedCard = _service.SpawnRandomCard(spawnTransform, _cardParent);

        // Assert
        Assertions.AssertThat(spawnedCard).IsNotNull();
        Assertions.AssertThat(spawnedCard.Name).IsEqual("Card");
        Assertions.AssertThat(spawnedCard.GetParent()).IsEqual(_cardParent);
        Assertions.AssertThat(spawnedCard.GlobalTransform.Origin).IsEqual(spawnTransform.Origin);
    }


    [TestCase]
    [TestCategory("Unit")]
    public void TestSpawnCardWithSpecificSignature()
    {
        // Arrange
        var signature = new CardSignature(new[] { 0.5f, -0.3f, 0.8f, -0.1f, 0.2f, -0.7f, 0.9f, -0.4f });
        var spawnTransform = Transform3D.Identity;

        // Act
        var spawnedCard = _service.SpawnCard(signature, spawnTransform, _cardParent);

        // Assert
        Assertions.AssertThat(spawnedCard).IsNotNull();
        Assertions.AssertThat(spawnedCard.Name).IsEqual("Card");
        Assertions.AssertThat(spawnedCard.GetParent()).IsEqual(_cardParent);

        if (spawnedCard is CardController controller) Assertions.AssertThat(controller.Signature).IsNotNull();
    }

    [TestCase]
    public void TestRandomOffsetRangeZero()
    {
        var offsetRange = Vector3.Zero;

        var offset = _service.GetRandomOffset(offsetRange);

        Assertions.AssertThat(offset).IsEqual(Vector3.Zero);
    }

    [TestCase]
    public void TestMultipleRandomOffsetsAreDifferent()
    {
        var offsetRange = new Vector3(1.0f, 1.0f, 1.0f);

        var offset1 = _service.GetRandomOffset(offsetRange);
        var offset2 = _service.GetRandomOffset(offsetRange);

        // With a range of 1.0, it's extremely unlikely they'd be exactly equal
        var areDifferent = Math.Abs(offset1.X - offset2.X) > 0.001f
                           || Math.Abs(offset1.Y - offset2.Y) > 0.001f
                           || Math.Abs(offset1.Z - offset2.Z) > 0.001f;
        Assertions.AssertBool(areDifferent).IsTrue();
    }

    [TestCase]
    public void TestSpawnCardHandlesNullScene()
    {
        _service.CardScene = null!;
        var signature = new CardSignature();
        var spawnTransform = Transform3D.Identity;

        var result = _service.SpawnCard(signature, spawnTransform, _cardParent);

        Assertions.AssertThat(result).IsNull();
    }
}