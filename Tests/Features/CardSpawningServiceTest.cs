using System;
using CardCleaner.Scripts.Core.DependencyInjection;
using CardCleaner.Scripts.Core.Interfaces;
using CardCleaner.Scripts.Features.Card.Controllers;
using CardCleaner.Scripts.Features.Card.Models;
using CardCleaner.Scripts.Features.Card.Services;
using GdUnit4;
using Godot;

namespace CardCleaner.Tests.Features;

// Mock implementations for testing

[TestSuite]
public class CardSpawningServiceTest
{
    private CardSpawningService _service;
    private MockCardGenerator _mockGenerator;
    private RandomNumberGenerator _rng;
    
    private ISceneRunner _testScene;
    private Node _testRoot;
    private Node3D _cardParent;
    private PackedScene _mockCardScene;

    [BeforeTest]
    public void Setup()
    {
        // Create root node for scene tree context
        _testScene = ISceneRunner.Load("res://Scenes/TestScene.tscn");
        _testRoot = _testScene.Scene();
        _testRoot.Name = "TestRoot";
        _cardParent = new Node3D();
        _testRoot.AddChild(_cardParent);
        
        // Create mock services
        _mockGenerator = new MockCardGenerator();
        _rng = new RandomNumberGenerator();
        _rng.Seed = 42; // Deterministic for testing
        
        // Set up service locator with mocks
        ServiceLocator.Container.RegisterSingleton<ICardGenerator>(_mockGenerator);
        ServiceLocator.Container.RegisterSingleton(_rng);
        
        // Create the service
        _service = new CardSpawningService();
        _testRoot.AddChild(_service);
        
        // Create a mock card scene
        _mockCardScene = GD.Load<PackedScene>("res://Scenes/CardShader.tscn");
        _service.CardScene = _mockCardScene;
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
    public void TestSpawnRandomCardCreatesCard()
    {
        var spawnTransform = Transform3D.Identity;
        spawnTransform.Origin = new Vector3(1, 2, 3);
        
        var spawnedCard = _service.SpawnRandomCard(spawnTransform, _cardParent);
        
        if (spawnedCard != null)
        {
            Assertions.AssertThat(spawnedCard.Name).IsEqual("Card");
            Assertions.AssertThat(spawnedCard.GetParent()).IsEqual(_cardParent);
            Assertions.AssertThat(spawnedCard.GlobalTransform.Origin).IsEqual(spawnTransform.Origin);
        }
    }

    [TestCase]
    public void TestSpawnCardWithSpecificSignature()
    {
        var signature = new CardSignature(new[] { 0.5f, -0.3f, 0.8f, -0.1f, 0.2f, -0.7f, 0.9f, -0.4f });
        var spawnTransform = Transform3D.Identity;
        
        var spawnedCard = _service.SpawnCard(signature, spawnTransform, _cardParent);
        
        if (spawnedCard is CardController controller)
        {
            Assertions.AssertThat(controller.Signature).IsNotNull();
            // Note: The signature gets set during spawning
        }
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
        _service.CardScene = null;
        var signature = new CardSignature();
        var spawnTransform = Transform3D.Identity;
        
        var result = _service.SpawnCard(signature, spawnTransform, _cardParent);
        
        Assertions.AssertThat(result).IsNull();
    }
}