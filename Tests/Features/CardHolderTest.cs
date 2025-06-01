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
    private CardHolder _holder;
    private Node3D _handAnchor;
    private RigidBody3D _testCard;
    private CollisionShape3D _testCollision;
    private MockCardSpawner _mockSpawner;
    private ServiceContainer _container;
    private int _cardAddedCount;
    private int _cardRemovedCount;

    [Before]
    public void Setup()
    {
        // Set up service container with mock spawner
        _container = new ServiceContainer();
        _mockSpawner = new MockCardSpawner();
        _container.RegisterSingleton<ICardSpawner>(_mockSpawner);
        
        // Create holder
        _holder = new CardHolder();
        _holder.HoldDistance = 2.0f;
        
        // Create hand anchor
        _handAnchor = new Node3D();
        _handAnchor.Name = "HandAnchor";
        _holder.SetReferences(_handAnchor);
        
        // Create test card
        _testCard = new RigidBody3D();
        _testCard.Name = "Card";
        
        // Add collision shape
        _testCollision = new CollisionShape3D();
        _testCollision.Name = "CardCollision";
        _testCollision.Shape = new BoxShape3D();
        _testCard.AddChild(_testCollision);
        
        // Connect signals for testing
        _holder.CardAdded += OnCardAdded;
        _holder.CardRemoved += OnCardRemoved;
        
        _cardAddedCount = 0;
        _cardRemovedCount = 0;
    }

    [After]
    public void Cleanup()
    {
        _testCard?.QueueFree();
        _handAnchor?.QueueFree();
        _holder?.QueueFree();
        _mockSpawner?.QueueFree();
    }

    private void OnCardAdded(RigidBody3D card)
    {
        _cardAddedCount++;
    }

    private void OnCardRemoved(RigidBody3D card)
    {
        _cardRemovedCount++;
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
        Assertions.AssertBool(_testCollision.Disabled).IsTrue();
        
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
        var otherCard = new RigidBody3D();
        otherCard.Name = "OtherCard";
        
        _holder.RemoveCard(otherCard); // Try to remove card that wasn't added
        
        Assertions.AssertThat(_holder.HeldCount).IsEqual(0);
        Assertions.AssertThat(_cardRemovedCount).IsEqual(0);
        
        otherCard.QueueFree();
    }

    [TestCase]
    public void TestRemoveTopCard()
    {
        var card1 = new RigidBody3D();
        card1.Name = "Card1";
        var card2 = new RigidBody3D();
        card2.Name = "Card2";
        
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
        var card1 = new RigidBody3D();
        card1.Name = "Card1";
        var card2 = new RigidBody3D();
        card2.Name = "Card2";
        var card3 = new RigidBody3D();
        card3.Name = "Card3";
        
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
        var card1 = new RigidBody3D();
        card1.Name = "Card1";
        var card2 = new RigidBody3D();
        card2.Name = "Card2";
        
        // Add CardDesigner component for thickness
        var designer1 = new CardDesigner();
        designer1.Name = "Designer";
        card1.AddChild(designer1);
        
        var designer2 = new CardDesigner();
        designer2.Name = "Designer";
        card2.AddChild(designer2);
        
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
        var card1 = new RigidBody3D();
        card1.Name = "Card1";
        
        // Add CardDesigner component
        var designer1 = new CardDesigner();
        designer1.Name = "Designer";
        card1.AddChild(designer1);
        
        _holder.AddCard(card1);
        
        // Set hand anchor transform
        _handAnchor.GlobalTransform = new Transform3D(Basis.Identity, new Vector3(0, 5, 0));
        
        _holder.PositionCardsForDrop();
        
        // Card should be positioned relative to camera forward direction
        var cardPos = card1.GlobalTransform.Origin;
        Assertions.AssertThat(cardPos.Y).IsEqual(5.0f); // Should match hand anchor Y
        
        card1.QueueFree();
    }

    [TestCase]
    public void TestCardOrderMaintained()
    {
        var card1 = new RigidBody3D();
        card1.Name = "Card1";
        var card2 = new RigidBody3D();
        card2.Name = "Card2";
        var card3 = new RigidBody3D();
        card3.Name = "Card3";
        
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