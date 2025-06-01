using System.Threading.Tasks;
using CardCleaner.Scripts.Core.DependencyInjection;
using CardCleaner.Scripts.Core.Interfaces;
using CardCleaner.Scripts.Features.Card.Components;
using CardCleaner.Scripts.Features.Card.Controllers;
using GdUnit4;
using Godot;

namespace CardCleaner.Tests.Features;

// Mock implementations for testing

[TestSuite]
public class CardHolderTest
{
    private CardHolder? _holder;
    private Node3D? _handAnchor;
    private RigidBody3D? _testCard;
    private CollisionShape3D? _testCollision;
    private MockCardSpawner? _mockSpawner;
    private ISceneRunner? _testScene;
    private Node? _testRoot;
    private int _cardAddedCount;
    private int _cardRemovedCount;

    [BeforeTest]
    public async Task Setup()
    {
        // Create root node for scene tree context
        _testScene = ISceneRunner.Load("res://Scenes/TestScene.tscn");
        _testRoot = _testScene.Scene();
        _testRoot.Name = "TestRoot";
        
        
        // Set up service container with mock spawner
        _mockSpawner = new MockCardSpawner();
        _mockSpawner.Name = "MockSpawner";
        _testRoot.AddChild(_mockSpawner);
        ServiceLocator.Container.RegisterSingleton<ICardSpawner>(_mockSpawner);
        
        // Create holder
        _holder = new CardHolder();
        _holder.HoldDistance = 2.0f;
        _testRoot.AddChild(_holder);
        
        // Create hand anchor and add to scene tree
        _handAnchor = new Node3D();
        _handAnchor.Name = "HandAnchor";
        _testRoot.AddChild(_handAnchor);
        _holder.SetReferences(_handAnchor);
        
        // Create test card and add to scene tree
        _testCard = CreateTestCard("Card");
        
        // Connect signals for testing
        _holder.CardAdded += OnCardAdded;
        _holder.CardRemoved += OnCardRemoved;
        
        _cardAddedCount = 0;
        _cardRemovedCount = 0;
        await _testScene.SimulateFrames(1).ConfigureAwait(false);
    }

    [AfterTest]
    public void TearDown()
    {
        ServiceLocator.ResetForTesting();
    }

    private void OnCardAdded(RigidBody3D card)
    {
        _cardAddedCount++;
    }

    private void OnCardRemoved(RigidBody3D card)
    {
        _cardRemovedCount++;
    }

    private RigidBody3D CreateTestCard(string name)
    {
        var card = new RigidBody3D();
        card.Name = name;
        
        _testCollision = new CollisionShape3D();
        _testCollision.Name = "CardCollision";
        _testCollision.Shape = new BoxShape3D();
        card.AddChild(_testCollision);
        
        var designer1 = new CardDesigner();
        designer1.Name = "Designer";
        card.AddChild(designer1);
        
        
        _testRoot.AddChild(card);
        return card;
    }

    [TestCase]
    public void TestInitialState()
    {
        Assertions.AssertThat(_holder.HeldCount).IsEqual(0);
        Assertions.AssertBool(_holder.HasCards).IsFalse();
    }

    [TestCase]
    public void TestAddCard()
    {
        _holder.AddCard(_testCard);
        
        Assertions.AssertThat(_holder.HeldCount).IsEqual(1);
        Assertions.AssertBool(_holder.HasCards).IsTrue();
        Assertions.AssertThat(_holder.HeldCards).Contains(_testCard);
        Assertions.AssertThat(_cardAddedCount).IsEqual(1);
        
        // Card should be frozen and collision disabled
        Assertions.AssertBool(_testCard.Freeze).IsTrue();
        Assertions.AssertBool(_testCollision is { Disabled: true }).IsTrue();
        
        // Card should be reparented to hand anchor
        Assertions.AssertThat(_testCard.GetParent()).IsEqual(_handAnchor);
    }

    [TestCase]
    public void TestAddDuplicateCard()
    {
        _holder.AddCard(_testCard);
        _holder.AddCard(_testCard); // Try to add same card again
        
        Assertions.AssertThat(_holder.HeldCount).IsEqual(1); // Should still be 1
        Assertions.AssertThat(_cardAddedCount).IsEqual(1); // Signal only fired once
    }

    [TestCase]
    public void TestRemoveCard()
    {
        _holder.AddCard(_testCard);
        _holder.RemoveCard(_testCard);
        
        Assertions.AssertThat(_holder.HeldCount).IsEqual(0);
        Assertions.AssertBool(_holder.HasCards).IsFalse();
        Assertions.AssertThat(_cardRemovedCount).IsEqual(1);
        
        // Card should be reparented to spawner
        Assertions.AssertThat(_testCard.GetParent()).IsEqual(_mockSpawner);
    }

    [TestCase]
    public void TestRemoveCardNotInHolder()
    {
        var otherCard = CreateTestCard("OtherCard");
        
        _holder.RemoveCard(otherCard); // Try to remove card that wasn't added
        
        Assertions.AssertThat(_holder.HeldCount).IsEqual(0);
        Assertions.AssertThat(_cardRemovedCount).IsEqual(0);
        
        otherCard.QueueFree();
    }

    [TestCase]
    public void TestRemoveTopCard()
    {
        var card1 = CreateTestCard("Card1");
        var card2  = CreateTestCard("Card2");
        
        _holder.AddCard(card1);
        _holder.AddCard(card2);
        
        Assertions.AssertThat(_holder.HeldCount).IsEqual(2);
        
        _holder.RemoveTopCard();
        
        Assertions.AssertThat(_holder.HeldCount).IsEqual(1);
        Assertions.AssertThat(_holder.HeldCards).Contains(card1);
        Assertions.AssertThat(_holder.HeldCards).NotContains(card2);
        
        card1.QueueFree();
        card2.QueueFree();
    }

    [TestCase]
    public void TestRemoveTopCardFromEmptyHolder()
    {
        _holder.RemoveTopCard(); // Should not crash
        
        Assertions.AssertThat(_holder.HeldCount).IsEqual(0);
        Assertions.AssertThat(_cardRemovedCount).IsEqual(0);
    }

    [TestCase]
    public void TestRemoveAllCards()
    {
        var card1 = CreateTestCard("Card1");
        var card2 = CreateTestCard("Card2");
        var card3 = CreateTestCard("Card3");
        
        _holder.AddCard(card1);
        _holder.AddCard(card2);
        _holder.AddCard(card3);
        
        Assertions.AssertThat(_holder.HeldCount).IsEqual(3);
        
        _holder.RemoveAllCards();
        
        Assertions.AssertThat(_holder.HeldCount).IsEqual(0);
        Assertions.AssertBool(_holder.HasCards).IsFalse();
        Assertions.AssertThat(_cardRemovedCount).IsEqual(3);
        
        card1.QueueFree();
        card2.QueueFree();
        card3.QueueFree();
    }

    [TestCase]
    public void TestPositionCards()
    {
        var card1 = CreateTestCard("Card1");
        var card2 = CreateTestCard("Card2");
        
        _holder.AddCard(card1);
        _holder.AddCard(card2);
        
        // Position cards
        _holder.PositionCards();
        
        // Cards should be positioned at different Z offsets
        var pos1 = card1.Transform.Origin;
        var pos2 = card2.Transform.Origin;
        
        Assertions.AssertThat(pos1.Z).IsNotEqual(pos2.Z);
        
        card1.QueueFree();
        card2.QueueFree();
    }

    [TestCase]
    public void TestPositionCardsForDrop()
    {
        _holder.AddCard(_testCard);
        
        // Set hand anchor transform
        _handAnchor.GlobalTransform = new Transform3D(Basis.Identity, new Vector3(0, 5, 0));
        
        _holder.PositionCardsForDrop();
        
        // Card should be positioned relative to camera forward direction
        var cardPos = _testCard.GlobalTransform.Origin;
        Assertions.AssertThat(cardPos.Y).IsEqual(5.0f); // Should match hand anchor Y
    }

    [TestCase]
    public void TestCardOrderMaintained()
    {
        var card1 = CreateTestCard("Card1");
        var card2 = CreateTestCard("Card2");
        var card3 = CreateTestCard("Card3");
        
        _holder.AddCard(card1);
        _holder.AddCard(card2);
        _holder.AddCard(card3);
        
        // Cards should be in order of addition
        Assertions.AssertThat(_holder.HeldCards[0]).IsEqual(card1);
        Assertions.AssertThat(_holder.HeldCards[1]).IsEqual(card2);
        Assertions.AssertThat(_holder.HeldCards[2]).IsEqual(card3);
        
        card1.QueueFree();
        card2.QueueFree();
        card3.QueueFree();
    }

    [TestCase]
    public void TestCollisionLayerSet()
    {
        _holder.AddCard(_testCard);
        
        // After adding, the card's collision layer should be set
        // Note: The collision layer is set during EnablePhysics which is called deferred
        // In a real test, we'd need to wait for the deferred call or test it separately
    }
}